using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;

using NSubstitute;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Db;

/// <remarks>
/// Reference source: dotenvx/src/db/session.js and db/device.js. Upstream swaps `Conf` for a fake to assert it is "not
/// constructed"; here the observable equivalent is asserted instead - the settings file is never created.
/// </remarks>
public class SessionTest : EnvironmentTestBase
{
    private readonly INativeSecretStore _native;

    public SessionTest()
    {
        // beforeEach: native get -> null, set/delete -> "native secret store unavailable"
        _native = Substitute.For<INativeSecretStore>();
        _native.Get(Arg.Any<string>()).Returns((string?)null);
        _native.When(store => store.Set(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()))
            .Do(_ => throw new InvalidOperationException("native secret store unavailable"));
        _native.When(store => store.Delete(Arg.Any<string>()))
            .Do(_ => throw new InvalidOperationException("native secret store unavailable"));
        NativeStore.Current = _native;
    }

    private string ConfigDir => Environment.GetEnvironmentVariable("DOTENVX_CONFIG")!;

    private string ConfigFile => Path.Combine(ConfigDir, ".env");

    /// <remarks>Refer to "Session stores login settings in dotenvx config"</remarks>
    [Fact]
    public void TestSessionStoresLoginSettingsInDotenvxConfig()
    {
        var sesh = new Session();

        sesh.Hostname().ShouldBe("https://armor.dotenvx.com");
        sesh.Username().ShouldBeNull();
        sesh.Token().ShouldBeNull();
        sesh.Login("https://armor.example.com", "user-id", "scott", "token-123").ShouldBe("token-123");
        sesh.Hostname().ShouldBe("https://armor.example.com");
        sesh.Username().ShouldBe("scott");
        sesh.Token().ShouldBe("token-123");
        sesh.Status().ShouldBe("on");
        sesh.On().ShouldBeTrue();
        sesh.Off().ShouldBeFalse();
        sesh.StorePath().ShouldNotBeNullOrEmpty();
        string config = File.ReadAllText(ConfigFile);
        config.ShouldContain("DOTENVX_ARMOR_TOKEN=\"token-123\"");
        config.ShouldContain("DOTENVX_ARMOR_ON=\"true\"");
    }

    /// <remarks>Refer to "Session turns armor off and on in settings"</remarks>
    [Fact]
    public void TestSessionTurnsArmorOffAndOnInSettings()
    {
        var sesh = new Session();
        sesh.Login("https://armor.example.com", "user-id", "scott", "token-123");

        sesh.TurnOff().ShouldBe("false");
        sesh.On().ShouldBeFalse();
        sesh.Off().ShouldBeTrue();
        sesh.Status().ShouldBe("off");
        File.ReadAllText(ConfigFile).ShouldContain("DOTENVX_ARMOR_ON=\"false\"");

        sesh.TurnOn().ShouldBe("true");
        sesh.On().ShouldBeTrue();
        sesh.Off().ShouldBeFalse();
        sesh.Status().ShouldBe("on");
        File.ReadAllText(ConfigFile).ShouldContain("DOTENVX_ARMOR_ON=\"true\"");
    }

    /// <remarks>Refer to "Session stores login token in native secret store first"</remarks>
    [Fact]
    public void TestSessionStoresLoginTokenInNativeSecretStoreFirst()
    {
        // a working native store (the fixture's default one throws on set; NSubstitute callbacks cannot be un-set)
        INativeSecretStore working = Substitute.For<INativeSecretStore>();
        working.Get("DOTENVX_ARMOR_TOKEN").Returns("token-123");
        NativeStore.Current = working;

        var sesh = new Session();

        sesh.Login("https://armor.example.com", "user-id", "scott", "token-123").ShouldBe("token-123");
        working.Received(1).Set("DOTENVX_ARMOR_TOKEN", "token-123", "DOTENVX_ARMOR_TOKEN");
        sesh.Token().ShouldBe("token-123");
        working.Received().Get("DOTENVX_ARMOR_TOKEN");
        File.ReadAllText(ConfigFile).ShouldNotContain("DOTENVX_ARMOR_TOKEN");
    }

    /// <remarks>Refer to "Session validates login settings before saving"</remarks>
    [Fact]
    public void TestSessionValidatesLoginSettingsBeforeSaving()
    {
        var sesh = new Session();

        Should.Throw<Exception>(() => sesh.Login(null, null, null, null)).Message.ShouldContain("DOTENVX_ARMOR_HOSTNAME");
        Should.Throw<Exception>(() => sesh.Login("https://armor.example.com", null, null, null)).Message
            .ShouldContain("DOTENVX_ARMOR_USER");
        Should.Throw<Exception>(() => sesh.Login("https://armor.example.com", "user-id", null, null)).Message
            .ShouldContain("DOTENVX_ARMOR_USERNAME");
        Should.Throw<Exception>(() => sesh.Login("https://armor.example.com", "user-id", "scott", null)).Message
            .ShouldContain("DOTENVX_ARMOR_TOKEN");
    }

    /// <remarks>Refer to "Session noArmor is false when native login is on"</remarks>
    [Fact]
    public void TestSessionNoArmorIsFalseWhenNativeLoginIsOn()
    {
        var sesh = new Session();
        sesh.Login("https://armor.example.com", "user-id", "scott", "token-123");

        sesh.NoArmor().ShouldBeFalse();
    }

    /// <remarks>Refer to "Session noArmor is true when DOTENVX_NO_ARMOR is true"</remarks>
    [Fact]
    public void TestSessionNoArmorIsTrueWhenDotenvxNoArmorIsTrue()
    {
        Environment.SetEnvironmentVariable("DOTENVX_NO_ARMOR", "true");
        var sesh = new Session();
        sesh.Login("https://armor.example.com", "user-id", "scott", "token-123");

        sesh.NoArmor().ShouldBeTrue();
    }

    /// <remarks>Refer to "Session logout clears login settings when config exists"</remarks>
    [Fact]
    public void TestSessionLogoutClearsLoginSettingsWhenConfigExists()
    {
        var sesh = new Session();
        sesh.Login("https://armor.example.com", "user-id", "scott", "token-123");
        sesh.Status().ShouldBe("on");

        sesh.Logout("https://armor.example.com", "user-id", "token-123").ShouldBeTrue();

        // the native delete is attempted first (it throws here and is ignored), then the settings file is cleared
        _native.Received(1).Delete("DOTENVX_ARMOR_TOKEN");
        sesh.Username().ShouldBeNull();
        sesh.Token().ShouldBeNull();
        sesh.Hostname().ShouldBe("https://armor.dotenvx.com");
        sesh.Status().ShouldBe("off");
    }

    /// <remarks>Refer to "Session validates logout settings before clearing"</remarks>
    [Fact]
    public void TestSessionValidatesLogoutSettingsBeforeClearing()
    {
        var sesh = new Session();

        Should.Throw<Exception>(() => sesh.Logout(null, null, null)).Message.ShouldContain("DOTENVX_ARMOR_HOSTNAME");
        Should.Throw<Exception>(() => sesh.Logout("https://armor.example.com", null, null)).Message
            .ShouldContain("DOTENVX_ARMOR_USER");
        Should.Throw<Exception>(() => sesh.Logout("https://armor.example.com", "user-id", null)).Message
            .ShouldContain("DOTENVX_ARMOR_TOKEN");
    }

    /// <remarks>Refer to "Session logout does not create config when config is absent"</remarks>
    [Fact]
    public void TestSessionLogoutDoesNotCreateConfigWhenConfigIsAbsent()
    {
        var sesh = new Session();

        sesh.Logout("https://armor.example.com", "user-id", "token-123").ShouldBeTrue();

        File.Exists(ConfigFile).ShouldBeFalse("config file is not created");
    }

    /// <remarks>Refer to "Session does not open config for status helpers when config is absent"</remarks>
    [Fact]
    public void TestSessionDoesNotOpenConfigForStatusHelpersWhenConfigIsAbsent()
    {
        var sesh = new Session();

        sesh.Status().ShouldBe("off");
        sesh.Hostname().ShouldBe("https://armor.dotenvx.com");
        sesh.Username().ShouldBeNull();
        sesh.Token().ShouldBeNull();
        sesh.OpenStore().ShouldBeNull();
        sesh.StorePath().ShouldBe(ConfigFile);
        sesh.NoArmor().ShouldBeTrue();
        File.Exists(ConfigFile).ShouldBeFalse("config file is not created");
    }

    /// <remarks>Refer to "Session notifyUpdate does not create config when config is absent"</remarks>
    [Fact]
    public void TestSessionNotifyUpdateDoesNotCreateConfigWhenConfigIsAbsent()
    {
        new Session().NotifyUpdate();

        File.Exists(ConfigFile).ShouldBeFalse("config file is not created");
    }

    /// <remarks>Refer to "Session notifyUpdate checks dotenvx VERSION endpoint and stores dotenvx keys"</remarks>
    [Fact]
    public void TestSessionNotifyUpdateChecksDotenvxVersionEndpointAndStoresDotenvxKeys()
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigFile, "DOTENVX_ARMOR_TOKEN=\"token-123\"\n");
        Session.NowMilliseconds = () => 1710000000000;
        PackageJson.Version = "1.0.0";
        var urls = new List<string>();
        ArmorHttp.Request = (url, _, _, _) =>
        {
            urls.Add(url);

            return new HttpResult(200, "9.9.9\n");
        };
        var output = new StringWriter();
        TextWriter original = Console.Error;
        Console.SetError(output);
        try
        {
            new Session().NotifyUpdate();
        }
        finally
        {
            Console.SetError(original);
        }

        urls.ShouldBe(["https://dotenvx.sh/VERSION"]);
        string config = File.ReadAllText(ConfigFile);
        config.ShouldContain("DOTENVX_VERSION=\"9.9.9\"");
        config.ShouldContain("DOTENVX_VERSION_LAST_CHECK=\"1710000000000\"");
        output.ToString().ShouldContain("⛆ update available [curl -sfS https://dotenvx.sh | sh]");
    }

    /// <remarks>Refer to "Session notifyUpdate skips check when dotenvx version was checked recently"</remarks>
    [Fact]
    public void TestSessionNotifyUpdateSkipsCheckWhenDotenvxVersionWasCheckedRecently()
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigFile, "DOTENVX_VERSION_LAST_CHECK=\"1709999999999\"\n");
        Session.NowMilliseconds = () => 1710000000000;
        PackageJson.Version = "1.0.0";
        int calls = 0;
        ArmorHttp.Request = (_, _, _, _) =>
        {
            calls++;

            return new HttpResult(200, "9.9.9");
        };

        new Session().NotifyUpdate();

        calls.ShouldBe(0);
    }

    /// <remarks>Refer to "Session supports default config path when DOTENVX_CONFIG is unset"</remarks>
    [Fact]
    public void TestSessionSupportsDefaultConfigPathWhenDotenvxConfigIsUnset()
    {
        Environment.SetEnvironmentVariable("DOTENVX_CONFIG", null);
        string defaultConfig = Path.Combine(Path.GetTempPath(), "dotenvx-default-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(defaultConfig);
        EnvPaths.ConfigOverride = () => defaultConfig;
        var sesh = new Session();

        sesh.StorePath().ShouldBe(Path.Combine(defaultConfig, ".env"));
        sesh.Hostname().ShouldBe("https://armor.dotenvx.com");
        File.Exists(Path.Combine(defaultConfig, ".env")).ShouldBeFalse("default config file is not created");
    }

    /// <remarks>Refer to "Session creates default store on login when DOTENVX_CONFIG is unset"</remarks>
    [Fact]
    public void TestSessionCreatesDefaultStoreOnLoginWhenDotenvxConfigIsUnset()
    {
        Environment.SetEnvironmentVariable("DOTENVX_CONFIG", null);
        string defaultConfig = Path.Combine(Path.GetTempPath(), "dotenvx-default-session-" + Guid.NewGuid().ToString("N"));
        EnvPaths.ConfigOverride = () => defaultConfig;
        var sesh = new Session();

        sesh.Login("https://armor.example.com", "user-id", "scott", "token-123").ShouldBe("token-123");

        // upstream asserts the Conf is built without a `cwd` (so the OS default applies); the equivalent is the default dir
        sesh.StorePath().ShouldBe(Path.Combine(defaultConfig, ".env"));
        File.Exists(Path.Combine(defaultConfig, ".env")).ShouldBeTrue();
    }

    /// <remarks>Refer to "Session reads existing config without login"</remarks>
    [Fact]
    public void TestSessionReadsExistingConfigWithoutLogin()
    {
        Directory.CreateDirectory(ConfigDir);
        File.WriteAllText(ConfigFile,
            "DOTENVX_ARMOR_HOSTNAME=\"https://armor.example.com\"\nDOTENVX_ARMOR_USERNAME=\"scott\"\nDOTENVX_ARMOR_TOKEN=\"token-123\"\n");

        var sesh = new Session();

        sesh.Hostname().ShouldBe("https://armor.example.com");
        sesh.Username().ShouldBe("scott");
        sesh.Token().ShouldBe("token-123");
        sesh.Status().ShouldBe("on");
    }

    /// <remarks>Refer to "Session returns device public key and system information"</remarks>
    [Fact]
    public void TestSessionReturnsDevicePublicKeyAndSystemInformation()
    {
        var sesh = new Session();

        System.Text.RegularExpressions.Regex.IsMatch(sesh.DevicePublicKey(), "^[0-9a-f]+$").ShouldBeTrue();
        SystemInformation.Info info = sesh.SystemInformation();
        info.SystemUuid.ShouldNotBeNull();
        info.OsPlatform.ShouldNotBeNullOrEmpty();
        info.OsArch.ShouldNotBeNullOrEmpty();
    }

    /// <remarks>Refer to "Device reuses existing private key"</remarks>
    [Fact]
    public void TestDeviceReusesExistingPrivateKey()
    {
        var device = new Device();

        string firstPrivateKey = device.PrivateKey();
        string secondPrivateKey = device.PrivateKey();

        secondPrivateKey.ShouldBe(firstPrivateKey);
        string encrypted = device.Encrypt("hello");
        device.Decrypt(encrypted).ShouldBe("hello");
        device.ConfigPath().ShouldNotBeNullOrEmpty();
    }

    /// <remarks>Refer to "Device supports default config path and empty private key branch"</remarks>
    [Fact]
    public void TestDeviceSupportsDefaultConfigPathAndEmptyPrivateKeyBranch()
    {
        Environment.SetEnvironmentVariable("DOTENVX_CONFIG", null);
        string defaultConfig = Path.Combine(Path.GetTempPath(), "dotenvx-default-device-" + Guid.NewGuid().ToString("N"));
        EnvPaths.ConfigOverride = () => defaultConfig;

        var device = new EmptyPrivateKeyDevice();

        device.ConfigPath().ShouldBe(Path.Combine(defaultConfig, ".device-key"));
        device.PublicKey().ShouldBe("");
    }

    private sealed class EmptyPrivateKeyDevice : Device
    {
        public override string PrivateKey()
        {
            return "";
        }
    }
}
