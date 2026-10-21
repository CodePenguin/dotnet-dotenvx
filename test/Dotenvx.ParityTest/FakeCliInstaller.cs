namespace Dotenvx.ParityTest;

/// <summary>
/// Copies a fake program (a single-file app in test/fakes, published to fakes/&lt;name&gt; next to the test assembly) into a directory
/// under the names of the tools it fakes (`op`, `bw`, `powershell`, `secret-tool` for secretcli; `security`). An apphost looks for the
/// dll named at build time next to itself, so the dll and its json files are copied unchanged and only the executable is duplicated
/// per name.
/// </summary>
public static class FakeCliInstaller
{
    /// <summary>The secretcli fake (`op`, `bw`, `powershell`, `secret-tool`).</summary>
    public static void Install(string binDirectory, params string[] tools) => InstallFrom(binDirectory, "secretcli", tools);

    /// <summary>Another fake from test/fakes (for example "security").</summary>
    public static void InstallFrom(string binDirectory, string fake, params string[] tools)
    {
        Directory.CreateDirectory(binDirectory);
        string suffix = OperatingSystem.IsWindows() ? ".exe" : "";
        string source = Path.Combine(AppContext.BaseDirectory, "fakes", fake);

        // The events worker runs with a filtered environment that has no DOTNET_ROOT, so an apphost cannot find a runtime installed
        // outside the default location (for example ~/.dotnet). On Unix each tool then becomes a script that restores it.
        string? dotnetRoot = OperatingSystem.IsWindows() || fake != "secretcli" ? null : Environment.GetEnvironmentVariable("DOTNET_ROOT");

        foreach (string file in Directory.GetFiles(source))
        {
            string name = Path.GetFileName(file);
            if (name == fake + suffix)
            {
                if (dotnetRoot is not null)
                {
                    Replace(file, Path.Combine(binDirectory, fake));
                }

                foreach (string tool in tools)
                {
                    if (dotnetRoot is null)
                    {
                        Replace(file, Path.Combine(binDirectory, tool + suffix));
                        continue;
                    }

                    string script = Path.Combine(binDirectory, tool);
                    File.WriteAllText(script,
                        $"#!/bin/sh\nDOTNET_ROOT='{dotnetRoot.Replace("'", "'\\''")}' FAKE_TOOL_NAME='{tool}' exec \"$(dirname \"$0\")/{fake}\" \"$@\"\n");
                    if (!OperatingSystem.IsWindows())
                    {
                        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                                     UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                    }
                }
            }
            else
            {
                Replace(file, Path.Combine(binDirectory, name));
            }
        }
    }

    // copy beside the target and rename over it: writing straight into an apphost that just exited fails with "Text file busy" on Linux
    private static void Replace(string source, string destination)
    {
        string temporary = destination + ".tmp";
        File.Copy(source, temporary, true);
        // Windows refuses to replace a shim a just-finished (or still-exiting) process holds open: retry briefly
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(temporary, destination, true);

                return;
            }
            catch (Exception error) when (attempt < 50 && error is UnauthorizedAccessException or IOException)
            {
                Thread.Sleep(100);
            }
        }
    }
}
