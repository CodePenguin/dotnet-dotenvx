#:property PublishAot=false
// Stand-in for the 1Password (`op`) and Bitwarden (`bw`) CLIs used by the parity tests. The parity fixture copies this
// app's apphost as `op[.exe]` and `bw[.exe]`; behavior is picked from the executable's own name and driven by files
// in $FAKE_SECRET_CLI_DIR so the Node and .NET dotenvx runs see exactly the same "vault":
//   op read <ref> --no-newline   -> prints the content of <dir>/op/<ref with non-alphanumerics replaced by _>, no newline
//   bw get <field> <item>        -> needs BW_SESSION; prints <dir>/bw/<item>_<field> plus "\n"
//   bw unlock --passwordenv V --raw -> prints <dir>/session when env V equals the content of <dir>/master
// Native OS secret stores (dotenvx native / the default key provider), keyed by the sanitized target or public key in
// <dir>/native/:
//   powershell.exe ... -EncodedCommand <script>   -> reads the JSON payload {action, target, username, secret} from stdin
//                                                    (action read prints the secret, write stores it, delete removes it)
//   secret-tool lookup|store|clear service dotenvx public-key <pub>
// A missing file exits 1. Every call is appended to <dir>/calls.log as `<tool> <args...>` (plus the BW_SESSION value
// for bw) so tests can assert how the CLIs were invoked.
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

string tool = Environment.GetEnvironmentVariable("FAKE_TOOL_NAME") ?? Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "op");
// the delivery worker of the events subsystem gets a filtered environment without FAKE_SECRET_CLI_DIR: fall back to the vault beside the shim directory
string dir = Environment.GetEnvironmentVariable("FAKE_SECRET_CLI_DIR")
    ?? Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? ".", "..", "vault"));
string Sanitize(string value) => Regex.Replace(value, "[^A-Za-z0-9]", "_");

File.AppendAllText(Path.Combine(dir, "calls.log"),
    $"{tool} {string.Join(' ', args)}" +
    (tool == "bw" ? $" [BW_SESSION={Environment.GetEnvironmentVariable("BW_SESSION")}]" : "") + "\n");

Console.OutputEncoding = new UTF8Encoding(false);

// dotenvx's own bw calls always end with --nointeraction
args = [.. args.Where(arg => arg != "--nointeraction")];

// Key custody (dotenvx 1password / bitwarden): items created by the custodians live in <dir>/op-items and <dir>/bw-items. Behaviour switches are
// plain files in <dir>: op-missing, op-signedout (removed by `op signin`), op-signin-fails, op-create-badid, op-create-novault,
// op-read-wrong, op-account (account id), bw-missing, bw-status (unlocked|locked|unauthenticated|garbage), bw-user (user id),
// bw-server, bw-create-org, bw-create-garbage, bw-get-wrong.
bool Flag(string name) => File.Exists(Path.Combine(dir, name));
string Setting(string name, string fallback) => Flag(name) ? File.ReadAllText(Path.Combine(dir, name)).Trim() : fallback;
string NextId(string kind, Func<int, string> format)
{
    Directory.CreateDirectory(Path.Combine(dir, kind));
    return format(Directory.GetFiles(Path.Combine(dir, kind)).Length + 1);
}

if (tool == "op" && Flag("op-missing"))
{
    Console.Error.WriteLine("op: not found");
    return 127;
}

if (tool == "op" && args is ["--version"])
{
    Console.Out.WriteLine("2.30.0");
    return 0;
}

if (tool == "op" && args is ["whoami", ..])
{
    if (Flag("op-signedout"))
    {
        Console.Error.WriteLine("[ERROR] account is not signed in");
        return 1;
    }

    Console.Out.WriteLine($"{{\"account_uuid\":\"{Setting("op-account", "abcdefghijklmnopqrstuvwxyz")}\"}}");
    return 0;
}

if (tool == "op" && args is ["signin"])
{
    if (Flag("op-signin-fails"))
    {
        Console.Error.WriteLine("[ERROR] signin failed");
        return 1;
    }

    File.Delete(Path.Combine(dir, "op-signedout"));
    return 0;
}

if (tool == "op" && args is ["item", "create", "-", ..])
{
    string id = NextId("op-items", count => $"it{count:D24}");
    JsonElement created = JsonDocument.Parse(Console.In.ReadToEnd()).RootElement;
    string privateKey = created.GetProperty("fields").EnumerateArray().First(field => field.GetProperty("label").GetString() == "private_key")
        .GetProperty("value").GetString()!;
    File.WriteAllText(Path.Combine(dir, "op-items", id), privateKey);
    string reportedId = Flag("op-create-badid") ? "bad" : id;
    string vault = Flag("op-create-novault") ? "bad" : "vaultvaultvaultvaultvault0";
    Console.Out.WriteLine($"{{\"id\":\"{reportedId}\",\"vault\":{{\"id\":\"{vault}\"}}}}");
    return 0;
}

if (tool == "op" && args is ["item", "delete", var deleteId, ..])
{
    string file = Path.Combine(dir, "op-items", deleteId);
    if (!File.Exists(file))
    {
        Console.Error.WriteLine("[ERROR] item not found");
        return 1;
    }

    File.Delete(file);
    return 0;
}

if (tool == "op" && args is ["read", var reference, "--no-newline", ..])
{
    string file = Path.Combine(dir, "op", Sanitize(reference));
    string[] parts = reference.Split('/');
    string itemFile = parts.Length == 5 ? Path.Combine(dir, "op-items", parts[3]) : "";
    if (itemFile.Length > 0 && File.Exists(itemFile))
    {
        Console.Out.Write(Flag("op-read-wrong") ? "wrong" : File.ReadAllText(itemFile));
        return 0;
    }

    if (!File.Exists(file))
    {
        Console.Error.WriteLine($"[ERROR] could not read secret '{reference}'");
        return 1;
    }

    Console.Out.Write(File.ReadAllText(file));
    return 0;
}

if (tool == "bw" && Flag("bw-missing"))
{
    Console.Error.WriteLine("bw: not found");
    return 127;
}

if (tool == "bw" && args is ["--version"])
{
    Console.Out.WriteLine("2024.9.0");
    return 0;
}

if (tool == "bw" && args is ["status"])
{
    string status = Setting("bw-status", "unlocked");
    if (status == "garbage")
    {
        Console.Out.WriteLine("not json");
        return 0;
    }

    if (status == "locked" && Environment.GetEnvironmentVariable("BW_SESSION") is { Length: > 0 } session && Flag("session") &&
        session == File.ReadAllText(Path.Combine(dir, "session")).Trim())
    {
        status = "unlocked";
    }

    string server = Flag("bw-server") ? $"\"{Setting("bw-server", "")}\"" : "null";
    Console.Out.WriteLine($"{{\"serverUrl\":{server},\"userId\":\"{Setting("bw-user", "11111111-1111-1111-1111-111111111111")}\",\"status\":\"{status}\"}}");
    return 0;
}

if (tool == "bw" && args is ["create", "item"])
{
    if (Flag("bw-create-garbage"))
    {
        Console.Out.WriteLine("not json");
        return 0;
    }

    string id = NextId("bw-items", count => $"00000000-0000-0000-0000-{count:D12}");
    JsonElement created = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(Console.In.ReadToEnd().Trim()))).RootElement;
    File.WriteAllText(Path.Combine(dir, "bw-items", id), created.GetProperty("login").GetProperty("password").GetString()!);
    string organization = Flag("bw-create-org") ? "\"22222222-2222-2222-2222-222222222222\"" : "null";
    Console.Out.WriteLine($"{{\"id\":\"{id}\",\"organizationId\":{organization}}}");
    return 0;
}

if (tool == "bw" && args is ["get", "password", var itemId] && File.Exists(Path.Combine(dir, "bw-items", itemId)))
{
    Console.Out.Write(Flag("bw-get-wrong") ? "wrong" : File.ReadAllText(Path.Combine(dir, "bw-items", itemId)));
    return 0;
}

if (tool == "bw" && args is ["delete", "item", var removeId])
{
    string file = Path.Combine(dir, "bw-items", removeId);
    if (!File.Exists(file))
    {
        Console.Error.WriteLine("Not found.");
        return 1;
    }

    File.Delete(file);
    return 0;
}

if (tool == "bw" && args is ["get", var field, var item])
{
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BW_SESSION")))
    {
        Console.Error.WriteLine("Vault is locked.");
        return 1;
    }

    string file = Path.Combine(dir, "bw", $"{Sanitize(item)}_{field}");
    if (!File.Exists(file))
    {
        Console.Error.WriteLine("Not found.");
        return 1;
    }

    Console.Out.Write(File.ReadAllText(file) + "\n");
    return 0;
}

if (tool == "bw" && args is ["unlock", "--passwordenv", var variable, "--raw"])
{
    string? password = Environment.GetEnvironmentVariable(variable);
    string master = Path.Combine(dir, "master");
    if (!File.Exists(master) || password != File.ReadAllText(master))
    {
        Console.Error.WriteLine("Invalid master password.");
        return 1;
    }

    Console.Out.Write(File.ReadAllText(Path.Combine(dir, "session")) + "\n");
    return 0;
}

if (tool == "powershell")
{
    using JsonDocument payload = JsonDocument.Parse(Console.In.ReadToEnd());
    string action = payload.RootElement.GetProperty("action").GetString()!;
    string file = Path.Combine(dir, "native", Sanitize(payload.RootElement.GetProperty("target").GetString()!));
    Directory.CreateDirectory(Path.Combine(dir, "native"));

    switch (action)
    {
        case "read":
            if (File.Exists(file))
            {
                Console.Out.Write(File.ReadAllText(file));
            }

            return 0;
        case "write":
            File.WriteAllText(file, payload.RootElement.GetProperty("secret").GetString()!);
            return 0;
        case "delete":
            if (!File.Exists(file))
            {
                Console.Error.WriteLine("Element not found.");
                return 1;
            }

            File.Delete(file);
            return 0;
    }
}

if (tool == "secret-tool" && args.Length >= 1)
{
    string publicKey = args[^1];
    string file = Path.Combine(dir, "native", Sanitize("dotenvx:" + publicKey));
    Directory.CreateDirectory(Path.Combine(dir, "native"));

    switch (args[0])
    {
        case "lookup":
            if (!File.Exists(file))
            {
                return 1;
            }

            Console.Out.Write(File.ReadAllText(file));
            return 0;
        case "store":
            File.WriteAllText(file, Console.In.ReadToEnd());
            return 0;
        case "clear":
            File.Delete(file);
            return 0;
    }
}

Console.Error.WriteLine($"fake {tool}: unsupported arguments");
return 2;
