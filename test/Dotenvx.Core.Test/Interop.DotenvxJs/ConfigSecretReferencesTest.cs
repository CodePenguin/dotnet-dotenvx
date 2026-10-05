using Dotenvx.Interop.DotenvxJs;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs;

/// <remarks>
/// Reference source: dotenvx/tests/lib/config-onepassword.test.js and config-bitwarden.test.js. Upstream writes fake
/// `op`/`bw` scripts onto PATH (`.cmd` files on Windows, which Node's spawn cannot even resolve - verified live: ENOENT);
/// here `SecretCli.Exec` is stubbed instead, so the same scenarios run on every OS. The real-process path is covered by
/// SecretReferenceParityTest.
/// </remarks>
public class ConfigSecretReferencesTest : EnvironmentTestBase
{
    private const string ItemId = "7ac9cae8-5067-4faf-b6ab-acfd00e2c328";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dotenvx-secrets-" + Guid.NewGuid().ToString("N"));

    public ConfigSecretReferencesTest()
    {
        Directory.CreateDirectory(_dir);
        Environment.SetEnvironmentVariable("DOTENVX_NO_1PASSWORD", null);
        Environment.SetEnvironmentVariable("DOTENVX_NO_BITWARDEN", null);
        Environment.SetEnvironmentVariable("BW_SESSION", "test-session");
        SecretCli.Exec = (_, _, _) => throw new InvalidOperationException("should not call the CLI");
    }

    protected override void TearDown()
    {
        base.TearDown();
        Directory.Delete(_dir, true);
    }

    private string WriteEnv(string content)
    {
        string path = Path.Combine(_dir, ".env");
        File.WriteAllText(path, content);

        return path;
    }

    private static ConfigResult Config(string path, Dictionary<string, string> processEnv, bool strict,
        bool no1Password = false, bool noBitwarden = false)
    {
        return Main.Config(new ConfigOptions
        {
            Path = [path],
            ProcessEnv = processEnv,
            Quiet = true,
            Strict = strict,
            No1Password = no1Password,
            NoBitwarden = noBitwarden
        });
    }

    /// <remarks>Refer to "config resolves op:// values through the 1Password CLI"</remarks>
    [Fact]
    public void TestConfigResolvesOnePasswordValues()
    {
        SecretCli.Exec = (command, _, _) => command == "op"
            ? new SecretCliResult(0, "super-secret")
            : throw new InvalidOperationException(command);
        var processEnv = new Dictionary<string, string>();

        ConfigResult result = Config(WriteEnv("SECRET=op://vault/item/password\nPLAIN=value\n"), processEnv, true);

        result.Parsed["SECRET"].ShouldBe("super-secret");
        processEnv["SECRET"].ShouldBe("super-secret");
        processEnv["PLAIN"].ShouldBe("value");
    }

    /// <remarks>Refer to "config leaves op:// values unresolved with no1Password"</remarks>
    [Fact]
    public void TestConfigLeavesOnePasswordValuesUnresolvedWithNo1Password()
    {
        var processEnv = new Dictionary<string, string>();

        ConfigResult result = Config(WriteEnv("SECRET=op://vault/item/password\n"), processEnv, true,
            no1Password: true);

        result.Parsed["SECRET"].ShouldBe("op://vault/item/password");
        processEnv["SECRET"].ShouldBe("op://vault/item/password");
    }

    /// <remarks>Refer to "config leaves op:// values unresolved with DOTENVX_NO_1PASSWORD"</remarks>
    [Fact]
    public void TestConfigLeavesOnePasswordValuesUnresolvedWithEnvVar()
    {
        Environment.SetEnvironmentVariable("DOTENVX_NO_1PASSWORD", "true");
        var processEnv = new Dictionary<string, string>();

        ConfigResult result = Config(WriteEnv("SECRET=op://vault/item/password\n"), processEnv, true);

        result.Parsed["SECRET"].ShouldBe("op://vault/item/password");
        processEnv["SECRET"].ShouldBe("op://vault/item/password");
    }

    /// <remarks>Refer to "config reports a failed op:// value and still loads the rest of the file"</remarks>
    [Fact]
    public void TestConfigReportsAFailedOnePasswordValueAndStillLoadsTheRest()
    {
        SecretCli.Exec = (_, _, _) => new SecretCliResult(1, "");
        var processEnv = new Dictionary<string, string>();

        ConfigResult result = Config(WriteEnv("API_KEY=op://vault/missing/password\nPLAIN=value\n"), processEnv,
            false);

        result.Parsed["PLAIN"].ShouldBe("value");
        result.Parsed["API_KEY"].ShouldBe("op://vault/missing/password");
        processEnv["PLAIN"].ShouldBe("value");
        processEnv["API_KEY"].ShouldBe("op://vault/missing/password");
        result.Error.ShouldBeOfType<CustomException>().Code.ShouldBe("1PASSWORD_FAILED");
    }

    /// <remarks>Refer to "config resolves bw:// values through the Bitwarden Password Manager CLI"</remarks>
    [Fact]
    public void TestConfigResolvesBitwardenValues()
    {
        SecretCli.Exec = (command, _, _) => command == "bw"
            ? new SecretCliResult(0, "super-secret\n")
            : throw new InvalidOperationException(command);
        var processEnv = new Dictionary<string, string>();

        ConfigResult result = Config(WriteEnv($"PASSWORD=bw://{ItemId}/password\nPLAIN=value\n"), processEnv, true);

        result.Parsed["PASSWORD"].ShouldBe("super-secret");
        processEnv["PASSWORD"].ShouldBe("super-secret");
        processEnv["PLAIN"].ShouldBe("value");
    }

    /// <remarks>Refer to "config preserves spaces in a quoted human-readable Bitwarden item name"</remarks>
    [Fact]
    public void TestConfigPreservesSpacesInAQuotedBitwardenItemName()
    {
        SecretCli.Exec = (_, args, _) => args[2] == "My GitHub Account"
            ? new SecretCliResult(0, "super-secret\n")
            : new SecretCliResult(1, "");
        var processEnv = new Dictionary<string, string>();

        ConfigResult result = Config(WriteEnv("PASSWORD=\"bw://My GitHub Account/password\"\n"), processEnv, true);

        result.Parsed["PASSWORD"].ShouldBe("super-secret");
        processEnv["PASSWORD"].ShouldBe("super-secret");
    }

    /// <remarks>Refer to "config leaves bw:// values unresolved with noBitwarden"</remarks>
    [Fact]
    public void TestConfigLeavesBitwardenValuesUnresolvedWithNoBitwarden()
    {
        var processEnv = new Dictionary<string, string>();

        ConfigResult result = Config(WriteEnv($"PASSWORD=bw://{ItemId}/password\n"), processEnv, true,
            noBitwarden: true);

        result.Parsed["PASSWORD"].ShouldBe($"bw://{ItemId}/password");
        processEnv["PASSWORD"].ShouldBe($"bw://{ItemId}/password");
    }

    /// <remarks>Refer to "config leaves bw:// values unresolved with DOTENVX_NO_BITWARDEN"</remarks>
    [Fact]
    public void TestConfigLeavesBitwardenValuesUnresolvedWithEnvVar()
    {
        Environment.SetEnvironmentVariable("DOTENVX_NO_BITWARDEN", "true");
        var processEnv = new Dictionary<string, string>();

        ConfigResult result = Config(WriteEnv($"PASSWORD=bw://{ItemId}/password\n"), processEnv, true);

        result.Parsed["PASSWORD"].ShouldBe($"bw://{ItemId}/password");
        processEnv["PASSWORD"].ShouldBe($"bw://{ItemId}/password");
    }

    /// <remarks>Refer to "config reports a failed bw:// value and still loads the rest of the file"</remarks>
    [Fact]
    public void TestConfigReportsAFailedBitwardenValueAndStillLoadsTheRest()
    {
        SecretCli.Exec = (_, _, _) => new SecretCliResult(1, "");
        var processEnv = new Dictionary<string, string>();

        ConfigResult result = Config(WriteEnv($"API_KEY=bw://{ItemId}/password\nPLAIN=value\n"), processEnv, false);

        result.Parsed["PLAIN"].ShouldBe("value");
        result.Parsed["API_KEY"].ShouldBe($"bw://{ItemId}/password");
        processEnv["PLAIN"].ShouldBe("value");
        processEnv["API_KEY"].ShouldBe($"bw://{ItemId}/password");
        result.Error.ShouldBeOfType<CustomException>().Code.ShouldBe("BITWARDEN_FAILED");
    }
}
