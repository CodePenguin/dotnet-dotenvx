using System.Text;
using System.Text.Json;

using Dotenvx.Interop.DotenvxJs.Helpers;

using NSubstitute;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/{macosKeychain,linuxSecretService,windowsCredentialManager}.js and
/// dotenvx/src/lib/providers/native/index.js. Upstream stubs `child_process.execFileSync` and `process.platform`; here
/// `SecretCli` (the process seam) and `NativeStore.Platform` are swapped instead, and each assertion checks the command,
/// arguments and stdin the stores would have run.
/// </remarks>
public class NativeStoreTest : EnvironmentTestBase
{
    private readonly List<(string Command, string[] Args, string? Input)> _calls = [];

    public NativeStoreTest()
    {
        // EnvironmentTestBase installs a substitute store; these tests exercise the real per-platform stores
        NativeStore.ResetToDefaults();
    }

    private void Stub(SecretCliResult result)
    {
        SecretCli.Exec = (command, args, _) =>
        {
            _calls.Add((command, args, null));
            return result;
        };
        SecretCli.ExecWithInput = (command, args, input) =>
        {
            _calls.Add((command, args, input));
            return result;
        };
    }

    private static INativeSecretStore Store(string platform)
    {
        return NativeStore.ForPlatform(platform);
    }

    /// <remarks>Refer to "reads a generic password through security"</remarks>
    [Fact]
    public void TestMacosReadsAGenericPasswordThroughSecurity()
    {
        Stub(new SecretCliResult(0, "private-key\n"));

        Store("darwin").Get("public-key").ShouldBe("private-key");

        _calls[0].Command.ShouldBe("/usr/bin/security");
        _calls[0].Args.ShouldBe(["find-generic-password", "-s", "dotenvx", "-a", "public-key", "-w"]);
    }

    /// <remarks>Refer to "writes a generic password through security"</remarks>
    [Fact]
    public void TestMacosWritesAGenericPasswordThroughSecurity()
    {
        Stub(new SecretCliResult(0, ""));

        Store("darwin").Set("public-key", "private-key", "dotenvx (PUB LIC)");

        _calls[0].Command.ShouldBe("/usr/bin/security");
        _calls[0].Args.ShouldBe(["-i"]);
        _calls[0].Input.ShouldBe(
            "\"add-generic-password\" \"-U\" \"-s\" \"dotenvx\" \"-a\" \"public-key\" \"-l\" \"dotenvx (PUB LIC)\" \"-w\" \"private-key\"\n");
    }

    /// <remarks>Refer to "quotes input for the security parser without shell evaluation"</remarks>
    [Fact]
    public void TestMacosQuotesInputForTheSecurityParserWithoutShellEvaluation()
    {
        Stub(new SecretCliResult(0, ""));

        Store("darwin").Set("public-key", "secret\"\\value", "label ' $(ignored); value");

        _calls[0].Args.ShouldBe(["-i"]);
        _calls[0].Input.ShouldBe(
            "\"add-generic-password\" \"-U\" \"-s\" \"dotenvx\" \"-a\" \"public-key\" \"-l\" \"label ' $(ignored); value\" \"-w\" \"secret\\\"\\\\value\"\n");
    }

    /// <remarks>Refer to "rejects command injection and oversized input before spawning"</remarks>
    [Fact]
    public void TestMacosRejectsCommandInjectionAndOversizedInputBeforeSpawning()
    {
        Stub(new SecretCliResult(0, ""));

        foreach (string invalid in new[] { "bad\nhelp", "bad\rhelp", "bad\0value", new string('x', 4096), new string('é', 2048) })
        {
            for (int index = 0; index < 3; index++)
            {
                string[] args = ["public-key", "private-key", "label"];
                args[index] = invalid;

                var error = Should.Throw<global::Dotenvx.Providers.NativeStoreException>(() =>
                    Store("darwin").Set(args[0], args[1], args[2]));
                error.Code.ShouldBe("NATIVE_ACCESS_FAILED");
                error.Message.ShouldBe("failed to save private key to macOS Keychain");
            }
        }

        _calls.ShouldBeEmpty();
    }

    /// <remarks>Refer to "write failures are sanitized and preserve unavailable classification"</remarks>
    [Theory]
    [InlineData(true, "NATIVE_UNAVAILABLE")]
    [InlineData(false, "NATIVE_ACCESS_FAILED")]
    public void TestMacosWriteFailuresAreSanitizedAndPreserveUnavailableClassification(bool notFound, string code)
    {
        Stub(new SecretCliResult(-1, "", notFound) { Stderr = "private-key" });

        var error = Should.Throw<global::Dotenvx.Providers.NativeStoreException>(() =>
            Store("darwin").Set("public-key", "private-key", "label"));

        error.Code.ShouldBe(code);
        error.Message.ShouldBe("failed to save private key to macOS Keychain");
        error.Message.ShouldNotContain("private-key");
    }

    /// <remarks>Refer to "rejects failed status, signals, and zero-exit diagnostics without leaking output"</remarks>
    [Theory]
    [InlineData(1, "")]
    [InlineData(137, "")] // killed by a signal: no exit status
    [InlineData(0, "private-key")]
    public void TestMacosRejectsFailedStatusSignalsAndZeroExitDiagnosticsWithoutLeakingOutput(int exitCode, string stderr)
    {
        Stub(new SecretCliResult(exitCode, "") { Stderr = stderr });

        var error = Should.Throw<global::Dotenvx.Providers.NativeStoreException>(() =>
            Store("darwin").Set("public-key", "private-key", "label"));

        error.Code.ShouldBe("NATIVE_ACCESS_FAILED");
        error.Message.ShouldBe("failed to save private key to macOS Keychain");
    }

    /// <remarks>Refer to "deletes a generic password through security"</remarks>
    [Fact]
    public void TestMacosDeletesAGenericPasswordThroughSecurity()
    {
        Stub(new SecretCliResult(0, ""));

        Store("darwin").Delete("public-key");

        _calls[0].Command.ShouldBe("/usr/bin/security");
        _calls[0].Args.ShouldBe(["delete-generic-password", "-s", "dotenvx", "-a", "public-key"]);
    }

    /// <remarks>Refer to "writes a secret through secret-tool with the private key on stdin"</remarks>
    [Fact]
    public void TestLinuxWritesASecretThroughSecretToolWithTheKeyOnStdin()
    {
        Stub(new SecretCliResult(0, ""));

        Store("linux").Set("public-key", "private-key-that-must-not-be-an-argument", "dotenvx (PUB LIC)");

        _calls[0].Command.ShouldBe("secret-tool");
        _calls[0].Args.ShouldBe(
            ["store", "--label=dotenvx (PUB LIC)", "service", "dotenvx", "public-key", "public-key"]);
        _calls[0].Input.ShouldBe("private-key-that-must-not-be-an-argument");
        string.Join(' ', _calls[0].Args).ShouldNotContain("private-key-that-must-not-be-an-argument");
    }

    /// <remarks>Refer to "reads a secret through secret-tool"</remarks>
    [Fact]
    public void TestLinuxReadsASecretThroughSecretTool()
    {
        Stub(new SecretCliResult(0, "private-key\n"));

        Store("linux").Get("public-key").ShouldBe("private-key");

        _calls[0].Command.ShouldBe("secret-tool");
        _calls[0].Args.ShouldBe(["lookup", "service", "dotenvx", "public-key", "public-key"]);
    }

    /// <remarks>Refer to "deletes a secret through secret-tool"</remarks>
    [Fact]
    public void TestLinuxDeletesASecretThroughSecretTool()
    {
        Stub(new SecretCliResult(0, ""));

        Store("linux").Delete("public-key");

        _calls[0].Command.ShouldBe("secret-tool");
        _calls[0].Args.ShouldBe(["clear", "service", "dotenvx", "public-key", "public-key"]);
    }

    /// <remarks>Refer to "sanitizes failed secret-tool writes"</remarks>
    [Fact]
    public void TestLinuxSanitizesFailedSecretToolWrites()
    {
        Stub(new SecretCliResult(1, ""));

        var error = Should.Throw<global::Dotenvx.Providers.NativeStoreException>(() =>
            Store("linux").Set("public-key", "private-key-that-must-not-leak", "label"));

        error.Message.ShouldBe("failed to save private key to Linux Secret Service");
        error.Message.ShouldNotContain("private-key-that-must-not-leak");
    }

    /// <remarks>Refer to "writes a generic credential through encoded PowerShell with the secret on stdin"</remarks>
    [Fact]
    public void TestWindowsWritesACredentialThroughEncodedPowerShell()
    {
        Stub(new SecretCliResult(0, ""));

        Store("win32").Set("public-key", "private-key-that-must-not-be-an-argument", "ignored");

        _calls[0].Command.ShouldBe("powershell.exe");
        _calls[0].Args[..3].ShouldBe(["-NoProfile", "-NonInteractive", "-EncodedCommand"]);
        string.Join(' ', _calls[0].Args).ShouldNotContain("private-key-that-must-not-be-an-argument");
        Encoding.Unicode.GetString(Convert.FromBase64String(_calls[0].Args[3])).ShouldContain("CredWriteW");

        using JsonDocument payload = JsonDocument.Parse(_calls[0].Input!);
        payload.RootElement.GetProperty("action").GetString().ShouldBe("write");
        payload.RootElement.GetProperty("target").GetString().ShouldBe("dotenvx:public-key");
        payload.RootElement.GetProperty("username").GetString().ShouldBe("public-key");
        payload.RootElement.GetProperty("secret").GetString().ShouldBe("private-key-that-must-not-be-an-argument");
    }

    /// <remarks>Refer to "reads a generic credential through encoded PowerShell"</remarks>
    [Fact]
    public void TestWindowsReadsACredentialThroughEncodedPowerShell()
    {
        Stub(new SecretCliResult(0, "private-key\n"));

        Store("win32").Get("public-key").ShouldBe("private-key");

        using JsonDocument payload = JsonDocument.Parse(_calls[0].Input!);
        payload.RootElement.GetProperty("action").GetString().ShouldBe("read");
        payload.RootElement.GetProperty("target").GetString().ShouldBe("dotenvx:public-key");
    }

    /// <remarks>Refer to "deletes a generic credential through encoded PowerShell"</remarks>
    [Fact]
    public void TestWindowsDeletesACredentialThroughEncodedPowerShell()
    {
        Stub(new SecretCliResult(0, ""));

        Store("win32").Delete("public-key");

        Encoding.Unicode.GetString(Convert.FromBase64String(_calls[0].Args[3])).ShouldContain("CredDeleteW");
        using JsonDocument payload = JsonDocument.Parse(_calls[0].Input!);
        payload.RootElement.GetProperty("action").GetString().ShouldBe("delete");
        payload.RootElement.GetProperty("target").GetString().ShouldBe("dotenvx:public-key");
    }

    /// <remarks>Refer to "sanitizes failed PowerShell writes"</remarks>
    [Fact]
    public void TestWindowsSanitizesFailedPowerShellWrites()
    {
        Stub(new SecretCliResult(1, ""));

        var error = Should.Throw<global::Dotenvx.Providers.NativeStoreException>(() =>
            Store("win32").Set("public-key", "private-key-that-must-not-leak", "label"));

        error.Message.ShouldBe("failed to save private key to Windows Credential Manager");
        error.Message.ShouldNotContain("private-key-that-must-not-leak");
    }

    /// <remarks>Refer to "sanitizes failed PowerShell deletes"</remarks>
    [Fact]
    public void TestWindowsSanitizesFailedPowerShellDeletes()
    {
        Stub(new SecretCliResult(1, ""));

        Should.Throw<global::Dotenvx.Providers.NativeStoreException>(() => Store("win32").Delete("public-key"))
            .Message.ShouldBe("failed to delete private key from Windows Credential Manager");
    }

    /// <remarks>Refer to "native provider is disabled outside supported platforms"</remarks>
    [Fact]
    public void TestNativeProviderIsDisabledOutsideSupportedPlatforms()
    {
        Stub(new SecretCliResult(0, "private-key"));
        NativeStore.Platform = "freebsd";

        KeyProviders.NativeProvider("public-key").ShouldBeEmpty();

        _calls.ShouldBeEmpty();
    }

    /// <remarks>Refer to "native provider reads macOS Keychain on darwin"</remarks>
    [Fact]
    public void TestNativeProviderReadsMacosKeychainOnDarwin()
    {
        Stub(new SecretCliResult(0, "private-key\n"));
        NativeStore.Platform = "darwin";

        KeyProviders.NativeProvider("public-key").ShouldBe(new Dictionary<string, string> { ["public-key"] = "private-key" });

        _calls[0].Command.ShouldBe("/usr/bin/security");
    }

    /// <remarks>Refer to "native provider reads Windows Credential Manager on win32"</remarks>
    [Fact]
    public void TestNativeProviderReadsWindowsCredentialManagerOnWin32()
    {
        Stub(new SecretCliResult(0, "private-key\n"));
        NativeStore.Platform = "win32";

        KeyProviders.NativeProvider("public-key").ShouldBe(new Dictionary<string, string> { ["public-key"] = "private-key" });

        _calls[0].Command.ShouldBe("powershell.exe");
    }

    /// <remarks>Refer to "native provider reads Linux Secret Service on linux"</remarks>
    [Fact]
    public void TestNativeProviderReadsLinuxSecretServiceOnLinux()
    {
        Stub(new SecretCliResult(0, "private-key\n"));
        NativeStore.Platform = "linux";

        KeyProviders.NativeProvider("public-key").ShouldBe(new Dictionary<string, string> { ["public-key"] = "private-key" });

        _calls[0].Command.ShouldBe("secret-tool");
    }

    /// <remarks>Refer to "native provider writes macOS Keychain on darwin"</remarks>
    [Fact]
    public void TestNativeProviderWritesMacosKeychainOnDarwin()
    {
        Stub(new SecretCliResult(0, ""));
        NativeStore.Platform = "darwin";

        NativeStore.Set("public-key", "private-key", "dotenvx (PUB LIC)");

        _calls[0].Command.ShouldBe("/usr/bin/security");
        _calls[0].Args.ShouldBe(["-i"]);
        _calls[0].Input.ShouldContain("dotenvx (PUB LIC)");
    }

    /// <remarks>Refer to "native provider defaults secret label to key"</remarks>
    [Fact]
    public void TestNativeProviderDefaultsTheSecretLabelToTheKey()
    {
        Stub(new SecretCliResult(0, ""));
        NativeStore.Platform = "darwin";

        NativeStore.Set("DOTENVX_ARMOR_TOKEN", "token-123");

        _calls[0].Args.ShouldBe(["-i"]);
        _calls[0].Input.ShouldBe(
            "\"add-generic-password\" \"-U\" \"-s\" \"dotenvx\" \"-a\" \"DOTENVX_ARMOR_TOKEN\" \"-l\" \"DOTENVX_ARMOR_TOKEN\" \"-w\" \"token-123\"\n");
    }

    /// <remarks>Refer to "native provider writes Windows Credential Manager on win32"</remarks>
    [Fact]
    public void TestNativeProviderWritesWindowsCredentialManagerOnWin32()
    {
        Stub(new SecretCliResult(0, ""));
        NativeStore.Platform = "win32";

        NativeStore.Set("public-key", "private-key", "dotenvx (PUB LIC)");

        _calls[0].Command.ShouldBe("powershell.exe");
        using JsonDocument payload = JsonDocument.Parse(_calls[0].Input!);
        payload.RootElement.GetProperty("action").GetString().ShouldBe("write");
    }

    /// <remarks>Refer to "native provider writes Linux Secret Service on linux"</remarks>
    [Fact]
    public void TestNativeProviderWritesLinuxSecretServiceOnLinux()
    {
        Stub(new SecretCliResult(0, ""));
        NativeStore.Platform = "linux";

        NativeStore.Set("public-key", "private-key", "dotenvx (PUB LIC)");

        _calls[0].Command.ShouldBe("secret-tool");
        _calls[0].Args[0].ShouldBe("store");
    }

    /// <remarks>Refer to "native provider deletes macOS Keychain item on darwin"</remarks>
    [Fact]
    public void TestNativeProviderDeletesMacosKeychainItemOnDarwin()
    {
        Stub(new SecretCliResult(0, ""));
        NativeStore.Platform = "darwin";

        NativeStore.Delete("public-key");

        _calls[0].Args.ShouldBe(["delete-generic-password", "-s", "dotenvx", "-a", "public-key"]);
    }

    /// <remarks>Refer to "native provider deletes Windows Credential Manager item on win32"</remarks>
    [Fact]
    public void TestNativeProviderDeletesWindowsCredentialManagerItemOnWin32()
    {
        Stub(new SecretCliResult(0, ""));
        NativeStore.Platform = "win32";

        NativeStore.Delete("public-key");

        using JsonDocument payload = JsonDocument.Parse(_calls[0].Input!);
        payload.RootElement.GetProperty("action").GetString().ShouldBe("delete");
    }

    /// <remarks>Refer to "native provider deletes Linux Secret Service item on linux"</remarks>
    [Fact]
    public void TestNativeProviderDeletesLinuxSecretServiceItemOnLinux()
    {
        Stub(new SecretCliResult(0, ""));
        NativeStore.Platform = "linux";

        NativeStore.Delete("public-key");

        _calls[0].Args[0].ShouldBe("clear");
    }

    /// <remarks>Not from a JS test - useNative (providers/index.js): CI and --no-native turn the provider off.</remarks>
    [Theory]
    [InlineData("true", false, false)]
    [InlineData("", false, true)]
    [InlineData("", true, false)]
    public void TestUseNative(string ci, bool noNative, bool expected)
    {
        NativeStore.Platform = "linux";
        Environment.SetEnvironmentVariable("CI", ci);

        KeyProviders.UseNative(noNative).ShouldBe(expected);
    }

    /// <remarks>Not from a JS test - providers/native/index.js swallows every store failure into "nothing found".</remarks>
    [Fact]
    public void TestNativeProviderSwallowsStoreFailures()
    {
        NativeStore.Platform = "linux";
        NativeStore.Current = Substitute.For<INativeSecretStore>();
        NativeStore.Current.Get("k").Returns(_ => throw new InvalidOperationException("boom"));

        KeyProviders.NativeProvider("k").ShouldBeEmpty();
    }
}
