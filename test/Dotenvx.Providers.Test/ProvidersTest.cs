using Dotenvx.Interop.DotenvxJs.Api;
using Dotenvx.Interop.DotenvxJs.Helpers;

using Shouldly;

namespace Dotenvx.Providers.Test;

/// <remarks>Reference source: @dotenvx/providers 0.3.5 (src/*.js). No real program, store or network is touched.</remarks>
[Collection("providers")]
public sealed class ProvidersTest : IDisposable
{
    private static readonly string PrivateKey = Primitives.Primitives.Keypair().PrivateKey;
    private static readonly string PublicKey = Primitives.Primitives.Derive(PrivateKey);
    private readonly string _dir = Directory.CreateTempSubdirectory("providers-test").FullName;
    private readonly List<ProcessRequest> _calls = [];

    public ProvidersTest()
    {
        NativeProvider.Platform = "linux";
    }

    public void Dispose()
    {
        ProcessRunner.ResetToDefaults();
        ArmorHttp.ResetToDefaults();
        NativeProvider.Platform = OperatingSystem.IsWindows() ? "win32" : OperatingSystem.IsMacOS() ? "darwin" : "linux";
        Directory.Delete(_dir, true);
    }

    private ProviderOptions Options(Dictionary<string, string>? env = null, Action<ProviderOptions>? _ = null)
    {
        Dictionary<string, string> merged = new() { ["DOTENVX_CONFIG"] = _dir };
        foreach ((string key, string value) in env ?? new Dictionary<string, string>())
        {
            merged[key] = value;
        }

        return new ProviderOptions { ProcessEnv = merged, Cwd = _dir };
    }

    private void Run(Func<ProcessRequest, ProcessOutcome> handler)
    {
        ProcessRunner.Run = request =>
        {
            _calls.Add(request);

            return handler(request);
        };
    }

    private static ProcessOutcome Ok(string stdout)
    {
        return new ProcessOutcome(0, stdout, "");
    }

    private void Settings(string content)
    {
        File.WriteAllText(Path.Combine(_dir, ".env"), content);
    }

    [Fact]
    public void WindowsScriptMatchesProvidersPackage()
    {
        // sha256 of Buffer.from(script, 'utf16le').toString('base64') in @dotenvx/providers 0.3.5
        string encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(WindowsCredentialScript.Script));

        WindowsCredentialScript.Script.Length.ShouldBe(2583);
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(encoded)))
            .ShouldBe("0a9801189e3bca27d50489ae4080fcbc30a9a45b2db247cc09d605616a350b29");
    }

    [Fact]
    public void FileProviderFindsMatchingKeyInDotenvKeys()
    {
        File.WriteAllText(Path.Combine(_dir, ".env.keys"), $"DOTENV_PRIVATE_KEY=\"nope,{PrivateKey}\"\n");

        new ProviderChain(Options()).GetSync(PublicKey).ShouldBe(new Dictionary<string, string> { [PublicKey] = PrivateKey });
    }

    [Fact]
    public async Task ChainReportsLookupOrderAndEndsEmpty()
    {
        List<string> seen = [];
        List<string> ended = [];
        ProviderOptions options = new()
        {
            ProcessEnv = new Dictionary<string, string> { ["DOTENVX_CONFIG"] = _dir, ["CI"] = "1", ["DOTENVX_NO_ARMOR"] = "true" },
            Cwd = _dir,
            OnLookup = seen.Add,
            OnLookupEnd = ended.Add
        };

        (await new ProviderChain(options).Get(PublicKey)).ShouldBeEmpty();
        seen.ShouldBe(ProviderChain.Order);
        ended.ShouldBe(ProviderChain.Order);
    }

    [Fact]
    public void NativeLinuxReadsSecretTool()
    {
        Run(_ => Ok($"{PrivateKey}\n"));

        new NativeProvider(Options()).GetSync(PublicKey).ShouldBe(new Dictionary<string, string> { [PublicKey] = PrivateKey });
        _calls.Single().File.ShouldBe("secret-tool");
        _calls.Single().Args.ShouldBe(["lookup", "service", "dotenvx", "public-key", PublicKey]);
    }

    [Fact]
    public void NativeMacosReadsKeychain()
    {
        NativeProvider.Platform = "darwin";
        Run(_ => Ok($"{PrivateKey}\n"));

        new NativeProvider(Options()).GetSync(PublicKey).ShouldContainKey(PublicKey);
        _calls.Single().File.ShouldBe("/usr/bin/security");
        _calls.Single().Args.ShouldBe(["find-generic-password", "-s", "dotenvx", "-a", PublicKey, "-w"]);
    }

    [Theory]
    [InlineData("CI")]
    [InlineData("DOTENVX_NO_NATIVE")]
    public void NativeIsOffInCiAndWhenDisabled(string variable)
    {
        Run(_ => Ok(PrivateKey));

        new NativeProvider(Options(new Dictionary<string, string> { [variable] = variable == "CI" ? "1" : "true" }))
            .GetSync(PublicKey).ShouldBeEmpty();
        _calls.ShouldBeEmpty();
    }

    [Fact]
    public void NativeFailuresAreSwallowed()
    {
        Run(_ => new ProcessOutcome(null, "", "", NotFound: true));

        new NativeProvider(Options()).GetSync(PublicKey).ShouldBeEmpty();
    }

    [Fact]
    public void OnePasswordReadsConfiguredReference()
    {
        string account = new('A', 26);
        string reference = $"op://{new string('b', 26)}/{new string('c', 26)}/private_key";
        Settings($"DOTENVX_ONEPASSWORD_{PublicKey}={account}|{reference}\n");
        Run(_ => Ok(PrivateKey));

        new OnePasswordProvider(Options()).GetSync(PublicKey).ShouldContainKey(PublicKey);
        _calls.Single().File.ShouldBe("op");
        _calls.Single().Args.ShouldBe(["read", reference, "--no-newline", $"--account={account}"]);
    }

    [Fact]
    public void OnePasswordRejectsMismatchedKey()
    {
        Settings($"DOTENVX_ONEPASSWORD_{PublicKey}={new string('A', 26)}|op://{new string('b', 26)}/{new string('c', 26)}/password\n");
        Run(_ => Ok(Primitives.Primitives.Keypair().PrivateKey));

        ProviderException error = Should.Throw<ProviderException>(() => new OnePasswordProvider(Options()).GetSync(PublicKey));
        error.Code.ShouldBe("1PASSWORD_FAILED");
    }

    [Fact]
    public void OnePasswordRejectsInvalidReference()
    {
        Settings($"DOTENVX_ONEPASSWORD_{PublicKey}=nope\n");

        Should.Throw<ProviderException>(() => new OnePasswordProvider(Options()).GetSync(PublicKey)).Code
            .ShouldBe("1PASSWORD_FAILED");
    }

    [Fact]
    public void BitwardenChecksStatusThenReadsPassword()
    {
        string item = "11111111-1111-1111-1111-111111111111";
        string user = "22222222-2222-2222-2222-222222222222";
        string reference = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            $"{{\"item\":\"{item}\",\"userId\":\"{user}\",\"serverUrl\":\"https://vault.bitwarden.com\"}}"));
        Settings($"DOTENVX_BITWARDEN_{PublicKey}={reference}\n");
        Run(request => request.Args[0] == "status"
            ? Ok($"{{\"status\":\"unlocked\",\"userId\":\"{user}\",\"serverUrl\":\"https://vault.bitwarden.com\"}}")
            : Ok(PrivateKey));

        new BitwardenProvider(Options(new Dictionary<string, string> { ["BW_SESSION"] = "s" })).GetSync(PublicKey)
            .ShouldContainKey(PublicKey);
        _calls.Select(call => string.Join(' ', call.Args)).ShouldBe(
            ["status --nointeraction", $"get password {item} --nointeraction"]);
    }

    [Fact]
    public void BitwardenNeedsSession()
    {
        string reference = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            "{\"item\":\"11111111-1111-1111-1111-111111111111\",\"userId\":\"22222222-2222-2222-2222-222222222222\",\"serverUrl\":\"\"}"));
        Settings($"DOTENVX_BITWARDEN_{PublicKey}={reference}\n");

        Should.Throw<ProviderException>(() => new BitwardenProvider(Options()).GetSync(PublicKey)).Code
            .ShouldBe("BITWARDEN_FAILED");
    }

    [Fact]
    public void ArmorIsOffWithoutSignIn()
    {
        new ArmorProvider(Options(new Dictionary<string, string> { ["CI"] = "1" })).GetSync(PublicKey).ShouldBeEmpty();
    }

    [Fact]
    public void ArmorPostsKeyringWithDeviceKey()
    {
        string? url = null;
        string? body = null;
        ArmorHttp.Request = (u, _, _, b) =>
        {
            url = u;
            body = b;

            return new HttpResult(200, $"{{\"{PublicKey}\":\"{PrivateKey}\"}}");
        };
        ProviderOptions options = Options();
        ProviderOptions signedIn = new()
        {
            ProcessEnv = options.ProcessEnv, Cwd = _dir, Token = "tok", Hostname = "https://h.example",
            DevicePublicKey = "device", CliVersion = "9.9.9"
        };

        new ArmorProvider(signedIn).GetSync(PublicKey).ShouldBe(new Dictionary<string, string> { [PublicKey] = PrivateKey });
        url.ShouldBe("https://h.example/api/armor/keyring");
        body.ShouldBe($"{{\"device_public_key\":\"device\",\"cli_version\":\"9.9.9\",\"public_key\":\"{PublicKey}\"}}");
    }

    [Fact]
    public void ChainDefersArmorErrorToCallback()
    {
        ArmorHttp.Request = (_, _, _, _) => new HttpResult(401, "{\"error\":{\"code\":\"UNAUTHORIZED\",\"message\":\"no\"}}");
        Exception? reported = null;
        ProviderOptions options = new()
        {
            ProcessEnv = new Dictionary<string, string> { ["DOTENVX_CONFIG"] = _dir, ["CI"] = "1" }, Cwd = _dir, Token = "t",
            Hostname = "https://h", DevicePublicKey = "d", OnArmorError = error => reported = error
        };

        new ProviderChain(options).GetSync(PublicKey).ShouldBeEmpty();
        reported.ShouldBeOfType<ApiException>().Message.ShouldBe("[UNAUTHORIZED] no");
        Should.Throw<ApiException>(() => new ProviderChain(new ProviderOptions
        {
            ProcessEnv = options.ProcessEnv, Cwd = _dir, Token = "t", Hostname = "https://h", DevicePublicKey = "d"
        }).GetSync(PublicKey));
    }

    [Fact]
    public void ArmorDecryptWithoutSignInIsNotConfigured()
    {
        Should.Throw<ProviderException>(() => new ArmorProvider(Options()).DecryptSync("src")).Code
            .ShouldBe("ARMOR_NOT_CONFIGURED");
    }
}
