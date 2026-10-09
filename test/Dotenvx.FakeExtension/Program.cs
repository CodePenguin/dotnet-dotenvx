// Stand-in for a `dotenvx-<name>` extension program (dotenvx dispatches unknown commands to `dotenvx-<command>` on PATH), used by
// the parity sweep. The apphost is copied under each extension name. It prints what it was given, so both CLIs can be compared:
//   argv:<json array of its arguments>
//   env:<json of the FAKEEXT_ENV variable named in FAKEEXT_ENVVARS (comma separated), plus PATH's first entry>
//   stdin:<json of everything read from stdin> (only when FAKEEXT_STDIN is set)
// FAKEEXT_EXIT sets the exit code, FAKEEXT_ERR is written to stderr, FAKEEXT_OUT to stdout before the report.
using System.Text;
using System.Text.Json;

Console.OutputEncoding = new UTF8Encoding(false);
string name = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "dotenvx-fake");
string? Env(string key) => Environment.GetEnvironmentVariable(key);

if (Env("FAKEEXT_OUT") is { Length: > 0 } text)
{
    Console.Out.Write(text);
}

if (Env("FAKEEXT_ERR") is { Length: > 0 } err)
{
    Console.Error.Write(err);
}

Console.Out.WriteLine($"{name} argv:{JsonSerializer.Serialize(args)}");
string[] vars = (Env("FAKEEXT_ENVVARS") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
if (vars.Length > 0)
{
    var values = vars.ToDictionary(v => v, v => Env(v));
    Console.Out.WriteLine($"{name} env:{JsonSerializer.Serialize(values)}");
}

if (Env("FAKEEXT_STDIN") is not null)
{
    Console.Out.WriteLine($"{name} stdin:{JsonSerializer.Serialize(Console.In.ReadToEnd())}");
}

return int.TryParse(Env("FAKEEXT_EXIT"), out int code) ? code : 0;
