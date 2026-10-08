namespace Dotenvx.ParityTest;

/// <summary>
/// Copies test/Dotenvx.FakeSecretCli's apphost into a directory under the names of the tools it fakes (`op`, `bw`,
/// `powershell`, `secret-tool`). An apphost looks for the dll named at build time next to itself, so the dll and its
/// json files are copied unchanged and only the executable is duplicated per name.
/// </summary>
public static class FakeCliInstaller
{
    public static void Install(string binDirectory, params string[] tools) => InstallFrom(binDirectory, "Dotenvx.FakeSecretCli", tools);

    /// <summary>The same for another fake project (for example Dotenvx.FakeSecurity).</summary>
    public static void InstallFrom(string binDirectory, string fakeProject, params string[] tools)
    {
        Directory.CreateDirectory(binDirectory);
        string suffix = OperatingSystem.IsWindows() ? ".exe" : "";

        foreach (string file in Directory.GetFiles(AppContext.BaseDirectory, fakeProject + ".*"))
        {
            string name = Path.GetFileName(file);
            if (name.EndsWith(".exe") || name == fakeProject)
            {
                foreach (string tool in tools)
                {
                    File.Copy(file, Path.Combine(binDirectory, tool + suffix), true);
                }
            }
            else
            {
                File.Copy(file, Path.Combine(binDirectory, name), true);
            }
        }
    }
}
