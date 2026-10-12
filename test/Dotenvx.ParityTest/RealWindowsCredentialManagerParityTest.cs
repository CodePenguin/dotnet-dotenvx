using Shouldly;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Not from a JS test - the `native` commands and the native key provider against the REAL Windows Credential Manager
/// (no fake tool on PATH), proving the PowerShell script, the target naming and the UTF-16 blob are interchangeable between
/// the Node and .NET CLIs. Windows only; each run uses a fresh keypair and removes its credential afterwards.
/// </remarks>
public class RealWindowsCredentialManagerParityTest(NodeParityFixture fixture) : ParityTestBase(fixture)
{
    private static readonly Dictionary<string, string> Env = new() { ["CI"] = "" };

    private async Task<string> CreateProject()
    {
        CliResult set = await Fixture.RunNodeAsync(WorkDir, Env, "set", "A", "hello", "--no-native", "--no-armor");
        set.ExitCode.ShouldBe(0, set.StandardError);

        return ReadFile(".env.keys");
    }

    private string PublicKey()
    {
        return System.Text.RegularExpressions.Regex.Match(ReadFile(".env"), "DOTENV_PUBLIC_KEY=\"([0-9a-f]+)\"").Groups[1].Value;
    }

    private async Task Cleanup(string publicKey)
    {
        if (publicKey.Length > 0)
        {
            await Fixture.RunNodeAsync(WorkDir, Env, "native", "down", "--no-armor");
            _ = publicKey;
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpByOneCliPullAndGetByTheOther(bool nodeStores)
    {
        if (!EnsureAvailable() || !OperatingSystem.IsWindows())
        {
            return;
        }

        string publicKey = "";
        try
        {
            string keys = await CreateProject();
            publicKey = PublicKey();
            Func<string[], Task<CliResult>> first = args => nodeStores
                ? Fixture.RunNodeAsync(WorkDir, Env, args)
                : Fixture.RunDotnetAsync(WorkDir, Env, args);
            Func<string[], Task<CliResult>> second = args => nodeStores
                ? Fixture.RunDotnetAsync(WorkDir, Env, args)
                : Fixture.RunNodeAsync(WorkDir, Env, args);

            (await first(["native", "up"])).ExitCode.ShouldBe(0);
            File.Exists(Path.Combine(WorkDir, ".env.keys")).ShouldBeFalse("the key moved into the store");

            // the other CLI finds the key through its native provider and decrypts
            CliResult get = await second(["get", "A", "--no-armor"]);
            get.StandardOutput.Trim().ShouldBe("hello");

            // ... and can pull it back into .env.keys, byte for byte
            (await second(["native", "pull"])).ExitCode.ShouldBe(0);
            ReadFile(".env.keys").ShouldContain(keys.Split('\n').First(line => line.StartsWith("DOTENV_PRIVATE_KEY=")));
        }
        finally
        {
            await Cleanup(publicKey);
        }
    }
}
