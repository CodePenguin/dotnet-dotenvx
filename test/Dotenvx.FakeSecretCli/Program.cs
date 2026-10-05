// Stand-in for the 1Password (`op`) and Bitwarden (`bw`) CLIs used by the parity tests. The parity fixture copies this
// app's apphost as `op[.exe]` and `bw[.exe]`; behavior is picked from the executable's own name and driven by files
// in $FAKE_SECRET_CLI_DIR so the Node and .NET dotenvx runs see exactly the same "vault":
//   op read <ref> --no-newline   -> prints the content of <dir>/op/<ref with non-alphanumerics replaced by _>, no newline
//   bw get <field> <item>        -> needs BW_SESSION; prints <dir>/bw/<item>_<field> plus "\n"
//   bw unlock --passwordenv V --raw -> prints <dir>/session when env V equals the content of <dir>/master
// A missing file exits 1. Every call is appended to <dir>/calls.log as `<tool> <args...>` (plus the BW_SESSION value
// for bw) so tests can assert how the CLIs were invoked.
using System.Text;
using System.Text.RegularExpressions;

string tool = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "op");
string dir = Environment.GetEnvironmentVariable("FAKE_SECRET_CLI_DIR") ?? ".";
string Sanitize(string value) => Regex.Replace(value, "[^A-Za-z0-9]", "_");

File.AppendAllText(Path.Combine(dir, "calls.log"),
    $"{tool} {string.Join(' ', args)}" +
    (tool == "bw" ? $" [BW_SESSION={Environment.GetEnvironmentVariable("BW_SESSION")}]" : "") + "\n");

Console.OutputEncoding = new UTF8Encoding(false);

if (tool == "op" && args is ["read", var reference, "--no-newline"])
{
    string file = Path.Combine(dir, "op", Sanitize(reference));
    if (!File.Exists(file))
    {
        Console.Error.WriteLine($"[ERROR] could not read secret '{reference}'");
        return 1;
    }

    Console.Out.Write(File.ReadAllText(file));
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

Console.Error.WriteLine($"fake {tool}: unsupported arguments");
return 2;
