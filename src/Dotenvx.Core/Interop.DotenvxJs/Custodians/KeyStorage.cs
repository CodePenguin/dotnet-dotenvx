using Dotenvx.Interop.DotenvxJs.Helpers;

namespace Dotenvx.Interop.DotenvxJs.Custodians;

/// <summary>The options `selectKeyStorage` reads: the custodian switches, whether keys may be created, and the keys file.</summary>
internal sealed class KeyStorageOptions
{
    public KeyAccessOptions Access { get; init; } = new();
    public bool NoCreate { get; init; }
    public string? Fk { get; init; }
}

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/{selectKeyStorage,storeKeyStorage}.js. Without a terminal (or in CI, or when no
/// key may be created) the key goes to the OS secret store when enabled, else to the file; on a terminal the user picks the
/// custody in a menu (local, Armor, password managers).
/// </remarks>
internal static class KeyStorage
{
    private static readonly Dictionary<string, string> NativeNames = new()
    {
        ["darwin"] = "macOS Keychain",
        ["win32"] = "Windows Credential Manager",
        ["linux"] = "Linux Secret Service"
    };

    /// <summary>`process.stdin.isTTY && process.stderr.isTTY`.</summary>
    public static Func<bool> IsInteractive { get; internal set; } = () => SecretCli.IsInteractive();

    internal static void ResetToDefaults()
    {
        IsInteractive = () => SecretCli.IsInteractive();
    }

    public static string Select(KeyStorageOptions options)
    {
        string defaultStorage = Custodians.Get("native").Enabled(options.Access) ? "native" : "file";
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) || options.NoCreate || !IsInteractive())
        {
            return defaultStorage;
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
                    storage = Prompts.Select(new Prompts.SelectRequest(category == "local" ? "Choose local custody" : "Choose password manager",
                        [.. entries, new Prompts.Choice("← back", ArmorCustodian.BackValue)])
                    {
                        Prefix = category == "local" ? "⛉" : "⛊",
                        Navigation = true,
                        BackValue = ArmorCustodian.BackValue
                    }) ?? throw new PromptCancelledException();
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

                return storage;
            }
        }
        finally
        {
            Spinners.Resume();
        }
    }

    /// <summary>`storeKeyStorage(storage, publicKey, privateKey, options, context)`: the chosen storage and what storing changed.</summary>
    public static (string Storage, StoreResult Stored) Store(string? storage, string publicKey, string privateKey,
        KeyStorageOptions options, StoreContext context)
    {
        context.AllowCustodyBack = true;
        while (true)
        {
            storage ??= Select(options);
            try
            {
                return (storage, Custodians.Store(storage, publicKey, privateKey, context));
            }
            catch (KeyCustodyBackException)
            {
                storage = null;
            }
        }
    }
}
