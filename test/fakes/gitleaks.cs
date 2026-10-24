#:property PublishAot=false
// Stand-in for the external `gitleaks` program, used by the parity sweep so `ext scan` can be compared without installing it.
// FAKEGL_VERSION_EXIT, FAKEGL_EXIT, FAKEGL_OUT and FAKEGL_ERR script its behavior; every call is appended to FAKEGL_LOG.
Console.OutputEncoding = new System.Text.UTF8Encoding(false);
static string? Env(string key) => Environment.GetEnvironmentVariable(key);

if (Env("FAKEGL_LOG") is { Length: > 0 } log)
{
    File.AppendAllText(log, string.Join(' ', args) + "\n");
}

if (args.FirstOrDefault() == "version")
{
    Console.Out.Write("8.0.0\n");
    return int.TryParse(Env("FAKEGL_VERSION_EXIT"), out int versionExit) ? versionExit : 0;
}

Console.Out.Write(Env("FAKEGL_OUT") ?? "");
Console.Error.Write(Env("FAKEGL_ERR") ?? "");
return int.TryParse(Env("FAKEGL_EXIT"), out int exit) ? exit : 0;
