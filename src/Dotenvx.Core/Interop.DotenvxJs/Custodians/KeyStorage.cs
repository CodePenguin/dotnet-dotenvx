using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs.Api;
using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Custodians;

/// <summary>The options `selectKeyStorage` reads: the custodian switches, whether keys may be created, and the keys file.</summary>
internal sealed class KeyStorageOptions
{
    public KeyAccessOptions Access { get; init; } = new();
    public bool NoCreate { get; init; }
    public string? Fk { get; init; }
}

/// <summary>A key kept in a remote Armor enclave: the team, service, connection and resource that hold it.</summary>
internal sealed record EnclaveSelection(string Team, string Service, string ConnectionId, JsonNode ResourceId);

/// <summary>The custody chosen for a key: a custodian id (`'file'`, `'armored'`, ...) or, for `enclaved`, where the enclave is.</summary>
internal sealed record StorageChoice(string Id, EnclaveSelection? Enclave = null)
{
    public static implicit operator StorageChoice(string id)
    {
        return new StorageChoice(id);
    }
}

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/{selectKeyStorage,storeKeyStorage,selectConnection,selectConnectionResource}.js.
/// Without a terminal (or in CI, or when no key may be created) the key goes to the file; on a terminal the user picks the
/// custody in a menu (local, Armor - armored or enclaved - and password managers).
/// </remarks>
internal static class KeyStorage
{
    private static readonly Dictionary<string, string> NativeNames = new()
    {
        ["darwin"] = "macOS Keychain",
        ["win32"] = "Windows Credential Manager",
        ["linux"] = "Linux Secret Service"
    };

    private static readonly Regex PublicKeyPattern = new("^(?:0[23][0-9a-fA-F]{64}|04[0-9a-fA-F]{128})$", RegexOptions.Compiled);

    private static readonly Action<TimeSpan> DefaultDelay = Thread.Sleep;

    private static readonly Prompts.Choice Back = new("← back", ArmorCustodian.BackValue);

    /// <summary>`process.stdin.isTTY && process.stderr.isTTY`.</summary>
    public static Func<bool> IsInteractive { get; internal set; } = () => SecretCli.IsInteractive();

    /// <summary>Waiting between connection-setup polls (swappable so tests do not wait).</summary>
    public static Action<TimeSpan> Delay { get; internal set; } = DefaultDelay;

    /// <summary>`Date.now()`.</summary>
    public static Func<DateTimeOffset> Now { get; internal set; } = () => DateTimeOffset.UtcNow;

    internal static void ResetToDefaults()
    {
        IsInteractive = () => SecretCli.IsInteractive();
        Delay = DefaultDelay;
        Now = () => DateTimeOffset.UtcNow;
    }

    private static string Text(JsonNode? node, string name)
    {
        JsonNode? value = node?[name];

        return value is null ? "" : value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : value.ToString();
    }

    private static string Pick(string title, IReadOnlyList<Prompts.Choice> choices, string? prefix, string? footer = null)
    {
        return Prompts.Select(new Prompts.SelectRequest(title, choices)
        {
            Navigation = true,
            BackValue = ArmorCustodian.BackValue,
            Prefix = prefix,
            Footer = footer
        }) ?? throw new PromptCancelledException();
    }

    private static string? TokenOf(KeyStorageOptions options, Session session)
    {
        return !string.IsNullOrEmpty(options.Access.Token) ? options.Access.Token : session.Token();
    }

    public static StorageChoice Select(KeyStorageOptions options)
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) || options.NoCreate || !IsInteractive())
        {
            return "file";
        }

        Dictionary<string, List<Prompts.Choice>> groups = [];
        Dictionary<string, string> setup = [];
        (string Group, string[] Ids)[] definitions =
            [("local", ["file", "native"]), ("passwords", ["onepassword", "bitwarden"]), ("armored", ["armored"])];
        foreach ((string group, string[] ids) in definitions)
        {
            List<Prompts.Choice> entries = [];
            foreach (string id in ids)
            {
                ICustodian custodian = Custodians.Get(id);
                if (!custodian.Enabled(options.Access))
                {
                    continue;
                }

                string name = id switch
                {
                    "native" => NativeNames.GetValueOrDefault(NativeStore.Platform, custodian.Name),
                    "file" => $"File {options.Fk ?? ".env.keys"}",
                    "armored" => "⛨ Dotenvx Armor",
                    _ => custodian.Name
                };
                if (!custodian.Available())
                {
                    string command = id == "onepassword" ? "op" : "bw";
                    name += " (install CLI)";
                    setup[id] = $"Install the {custodian.Name} CLI ({command}), then retry this command.";
                }
                else if (id == "armored" && !custodian.Configured(options.Access))
                {
                    name += " (sign in)";
                    setup[id] = "Sign in with [dotenvx login], then retry this command.";
                }

                entries.Add(new Prompts.Choice(name, id));
            }

            if (entries.Count > 0)
            {
                groups[group] = entries;
            }
        }

        Spinners.Pause();
        try
        {
            List<Prompts.Choice> choices =
            [
                new("⛉ Local", "local"), .. groups.GetValueOrDefault("armored") ?? [],
                .. groups.ContainsKey("passwords") ? [new Prompts.Choice("⛊ Password Managers", "passwords")] : (Prompts.Choice[])[]
            ];
            string initial = "local";
            while (true)
            {
                string category = Prompts.Select(new Prompts.SelectRequest("Choose private key custody", choices) { Navigation = true, Initial = initial }) ?? throw new PromptCancelledException();
                if (choices.All(choice => choice.Value != category))
                {
                    throw new Exception("Invalid private key storage selection");
                }

                string storage = category;
                if (category != "armored")
                {
                    List<Prompts.Choice> entries = groups[category];
                    storage = Pick(category == "local" ? "Choose local custody" : "Choose password manager", [.. entries, Back],
                        category == "local" ? "⛉" : "⛊");
                    if (storage == ArmorCustodian.BackValue)
                    {
                        initial = category;
                        continue;
                    }

                    if (entries.All(choice => choice.Value != storage))
                    {
                        throw new Exception("Invalid private key storage selection");
                    }
                }

                if (setup.TryGetValue(storage, out string? message))
                {
                    throw new Exception(message);
                }

                if (storage != "armored")
                {
                    return storage;
                }

                while (true)
                {
                    string keyType = Prompts.Select(new Prompts.SelectRequest("Choose key type",
                    [
                        new("⛨ Armored", "armored"), new("◊ Enclaved", "enclaved"), Back
                    ])
                    {
                        Prefix = "⛨",
                        Initial = "armored",
                        Navigation = true,
                        BackValue = ArmorCustodian.BackValue
                    }) ?? throw new PromptCancelledException();
                    if (keyType == ArmorCustodian.BackValue)
                    {
                        initial = "armored";
                        break;
                    }

                    if (keyType == "enclaved")
                    {
                        if (SelectEnclave(options) is { } enclave)
                        {
                            return enclave;
                        }

                        continue;
                    }

                    if (keyType != "armored")
                    {
                        throw new Exception("Invalid key type selection");
                    }

                    return storage;
                }
            }
        }
        finally
        {
            Spinners.Resume();
        }
    }

    /// <summary>The enclaved branch of selectKeyStorage: team, service, connection and resource; null when the user went back.</summary>
    private static StorageChoice? SelectEnclave(KeyStorageOptions options)
    {
        var session = new Session();
        string hostname = session.Hostname();
        string? token = TokenOf(options, session);

        JsonNode[] teams = ArmorApi.GetTeams(hostname, token);
        if (teams.Length == 0)
        {
            throw new Exception("No teams available for enclave keys");
        }

        List<Prompts.Choice> teamChoices = [.. teams.Select(team => new Prompts.Choice(Text(team, "slug"), Text(team, "slug")))];
        while (true)
        {
            string team = Pick("Select team", [.. teamChoices, Back], "◊");
            if (team == ArmorCustodian.BackValue)
            {
                return null;
            }

            if (teamChoices.All(choice => choice.Value != team))
            {
                throw new Exception("Invalid team selection");
            }

            JsonNode[] services =
                [.. ArmorApi.GetTeamServices(hostname, token, team)["services"]!.AsArray().Select(service => service!)];
            if (services.Length == 0)
            {
                throw new Exception("No services available for this team");
            }

            List<Prompts.Choice> serviceChoices =
                [.. services.Select(service => new Prompts.Choice(Text(service, "name"), Text(service, "slug")))];
            while (true)
            {
                string service = Pick("Select service", [.. serviceChoices, Back], "◊");
                if (service == ArmorCustodian.BackValue)
                {
                    break;
                }

                if (serviceChoices.All(choice => choice.Value != service))
                {
                    throw new Exception("Invalid service selection");
                }

                JsonNode selectedService = services.First(item => Text(item, "slug") == service);
                while (true)
                {
                    JsonNode? connection = SelectConnection(hostname, token, team, selectedService);
                    if (connection is null)
                    {
                        break;
                    }

                    JsonNode? resource = SelectConnectionResource(hostname, token, team, connection);
                    if (resource is null)
                    {
                        continue;
                    }

                    return new StorageChoice("enclaved",
                        new EnclaveSelection(team, service, Text(connection, "id"), resource["id"]!.DeepClone()));
                }
            }
        }
    }

    /// <summary>selectConnection.js: an existing connection, or a new one set up in the browser; null when the user went back.</summary>
    private static JsonNode? SelectConnection(string hostname, string? token, string team, JsonNode service)
    {
        string slug = Text(service, "slug");
        string serviceName = Text(service, "name");
        JsonNode[] connections = ArmorApi.GetTeamConnections(hostname, token, team, slug);
        string selected = Pick("Select connection",
        [
            .. connections.Select(connection => new Prompts.Choice(Text(connection, "name"), Text(connection, "id"))),
            new Prompts.Choice($"+ Connect to {serviceName}", "__connect"), Back
        ], "◊");
        if (selected == ArmorCustodian.BackValue)
        {
            return null;
        }

        if (selected != "__connect")
        {
            return connections.FirstOrDefault(connection => Text(connection, "id") == selected)
                ?? throw new Exception("Invalid connection selection");
        }

        JsonNode setup = ArmorApi.ConnectionSetupCreate(hostname, token, team, slug);
        var url = new Uri(new Uri(hostname), Text(setup, "setup_url"));
        if (url.Scheme is not ("http" or "https"))
        {
            throw new Exception("Invalid connection setup URL");
        }

        Prompts.PromptOutput.Write($"◇ waiting for {serviceName} connection…\n{url.AbsoluteUri}\n");
        try
        {
            OpenUrl.Open(url.AbsoluteUri);
        }
        catch
        {
            Prompts.PromptOutput.Write($"Open the URL above to connect {serviceName}.\n");
        }

        DateTimeOffset tenMinutes = Now().AddMinutes(10);
        DateTimeOffset deadline = DateTimeOffset.TryParse(Text(setup, "expires_at"), out DateTimeOffset expires)
            ? (expires < tenMinutes ? expires : tenMinutes)
            : DateTimeOffset.MinValue;
        while (Now() < deadline)
        {
            JsonNode result = ArmorApi.ConnectionSetupStatus(hostname, token, team, Text(setup, "id"));
            string status = Text(result, "status");
            if (status == "connected")
            {
                if (result["connection"] is not { } connection || Text(connection, "service") != slug)
                {
                    throw new Exception("Invalid connection response");
                }

                return connection;
            }

            if (status == "expired")
            {
                break;
            }

            if (status != "pending")
            {
                throw new Exception($"{serviceName} connection setup failed");
            }

            double interval = setup["interval"] is { } value && value.GetValueKind() == JsonValueKind.Number && value.GetValue<double>() != 0
                ? value.GetValue<double>()
                : 2;
            Delay(TimeSpan.FromMilliseconds(Math.Min(5000, Math.Max(1000, interval * 1000))));
        }

        throw new Exception($"{serviceName} connection setup expired. Select Connect to {serviceName} to try again.");
    }

    /// <summary>selectConnectionResource.js: a resource of the connection (searchable); null when the user went back.</summary>
    private static JsonNode? SelectConnectionResource(string hostname, string? token, string team, JsonNode connection)
    {
        string query = "";
        while (true)
        {
            JsonNode page = ArmorApi.GetConnectionResources(hostname, token, team, Text(connection, "id"), query);
            JsonNode[] resources = [.. page["resources"]!.AsArray().Select(resource => resource!)];
            string? footer = resources.Length == 0
                ? (query.Length > 0 ? $"No resources found for \"{query}\"." : "No resources available for this connection.")
                : (page["meta"]?["has_more"] is { } more && more.GetValueKind() == JsonValueKind.True
                    ? "Showing 5 resources. Search to narrow the results."
                    : null);
            JsonNode[] shown = [.. resources.Take(5)];
            string selected = Pick("Select resource",
            [
                .. shown.Select(resource =>
                {
                    string contextName = Text(connection, "name") is { Length: > 0 } name ? name : Text(resource, "organization");
                    string resourceName = Text(resource, "name") is { Length: > 0 } named ? named : Text(resource, "id");
                    string label = contextName.Length > 0
                        ? $"{contextName}{(resourceName.StartsWith('/') ? " " : " / ")}{resourceName}"
                        : resourceName;

                    return new Prompts.Choice(label, Text(resource, "id"));
                }),
                new Prompts.Choice(query.Length > 0 ? "Search again…" : "Search…", "__search"), Back
            ], "◊", footer);
            if (selected == ArmorCustodian.BackValue)
            {
                return null;
            }

            if (selected == "__search")
            {
                query = JsTrim(Prompts.InputText("Search resources (blank to reset)") ?? throw new PromptCancelledException());

                continue;
            }

            return shown.FirstOrDefault(resource => Text(resource, "id") == selected)
                ?? throw new Exception("Invalid resource selection");
        }
    }

    /// <summary>
    /// `storeKeyStorage(storage, publicKey, privateKey, options, context)`: the chosen storage, what storing changed and the
    /// public key. With no key given the keypair is made after the storage is chosen (an enclave makes its own).
    /// </summary>
    public static (StorageChoice Storage, StoreResult Stored, string PublicKey) Store(StorageChoice? storage, string? publicKey,
        string? privateKey, KeyStorageOptions options, StoreContext context)
    {
        context.AllowCustodyBack = true;
        while (true)
        {
            storage ??= Select(options);
            if (storage.Id == "enclaved")
            {
                string privateKeyName;
                Spinners.Pause();
                try
                {
                    privateKeyName = Prompts.InputWithInitial("Private key name", context.PrivateKeyName)
                        ?? throw new PromptCancelledException();
                }
                finally
                {
                    Spinners.Resume();
                }

                var session = new Session();
                EnclaveSelection enclave = storage.Enclave!;
                JsonNode result = ArmorApi.PostConnectionKeypair(session.Hostname(), TokenOf(options, session), enclave.Team,
                    enclave.ConnectionId, enclave.ResourceId, privateKeyName);
                string? remotePublicKey = result["keypair"]?["public_key"] is { } node && node.GetValueKind() == JsonValueKind.String
                    ? node.GetValue<string>()
                    : null;
                if (remotePublicKey is null || !PublicKeyPattern.IsMatch(remotePublicKey))
                {
                    throw new Exception("Invalid public key returned by Armor");
                }

                Encrypt("", remotePublicKey);

                return (storage, new StoreResult(), remotePublicKey);
            }

            if (publicKey is null)
            {
                (publicKey, privateKey) = Keypair();
            }

            try
            {
                return (storage, Custodians.Store(storage.Id, publicKey, privateKey!, context), publicKey);
            }
            catch (KeyCustodyBackException)
            {
                storage = null;
            }
        }
    }
}
