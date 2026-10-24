using Dotenvx.Core.Test;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

using Shouldly;

namespace Dotenvx.Configuration.Test;

public class DotenvxConfigurationProviderTest : EnvironmentTestBase
{
    private readonly string _directory = Directory.CreateTempSubdirectory("dotenvx-config-").FullName;

    public DotenvxConfigurationProviderTest()
    {
        ClearEnvironmentExceptPath();
    }

    protected override void TearDown()
    {
        Directory.Delete(_directory, true);
    }

    private string Write(string content)
    {
        string path = Path.Combine(_directory, ".env");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void TestKeysAreCaseInsensitive()
    {
        string path = Write("Hello=world\n");

        IConfigurationRoot configuration = new ConfigurationBuilder().AddDotenvx(options => options.Path = [path]).Build();

        configuration["hello"].ShouldBe("world");
        configuration["HELLO"].ShouldBe("world");
    }

    [Fact]
    public void TestDoubleUnderscoreIsTheHierarchySeparator()
    {
        string path = Write("Logging__LogLevel__Default=Debug\n");

        IConfigurationRoot configuration = new ConfigurationBuilder().AddDotenvx(options => options.Path = [path]).Build();

        configuration["Logging:LogLevel:Default"].ShouldBe("Debug");
        configuration.GetSection("Logging:LogLevel")["Default"].ShouldBe("Debug");
    }

    [Fact]
    public void TestDoesNotWriteTheProcessEnvironment()
    {
        string path = Write("DOTENVX_CONFIG_PROVIDER_TEST=value\n");

        IConfigurationRoot configuration = new ConfigurationBuilder().AddDotenvx(options => options.Path = [path]).Build();

        configuration["DOTENVX_CONFIG_PROVIDER_TEST"].ShouldBe("value");
        Environment.GetEnvironmentVariable("DOTENVX_CONFIG_PROVIDER_TEST").ShouldBeNull();
    }

    [Fact]
    public void TestIsSilentUnlessALoggerIsGiven()
    {
        string path = Write("QUIET_TEST=1\n");
        StringWriter output = new();
        StringWriter error = new();
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            new ConfigurationBuilder().AddDotenvx(options => options.Path = [path]).Build();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        output.ToString().ShouldBeEmpty();
        error.ToString().ShouldBeEmpty();
    }

    [Fact]
    public void TestExistingEnvironmentVariablesWinUnlessOverloaded()
    {
        string path = Write("PRE_EXISTING=file\n");
        Environment.SetEnvironmentVariable("PRE_EXISTING", "process");

        new ConfigurationBuilder().AddDotenvx(options => options.Path = [path]).Build()["PRE_EXISTING"].ShouldBe("process");
        new ConfigurationBuilder().AddDotenvx(options =>
        {
            options.Path = [path];
            options.Overload = true;
        }).Build()["PRE_EXISTING"].ShouldBe("file");
        Environment.GetEnvironmentVariable("PRE_EXISTING").ShouldBe("process");
    }

    [Fact]
    public void TestLaterSourcesOverrideTheDotenvxValues()
    {
        string path = Write("ORDER_TEST=file\n");

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddDotenvx(options => options.Path = [path])
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ORDER_TEST"] = "memory" })
            .Build();

        configuration["ORDER_TEST"].ShouldBe("memory");
    }

    [Fact]
    public void TestReloadsWhenTheFileChanges()
    {
        string path = Write("RELOAD_TEST=one\n");
        IConfigurationRoot configuration = new ConfigurationBuilder().AddDotenvx(source =>
        {
            source.ConfigureOptions = options => options.Path = [path];
            source.ReloadOnChange = true;
            source.ReloadDelay = 10;
        }).Build();
        configuration["RELOAD_TEST"].ShouldBe("one");

        using ManualResetEventSlim reloaded = new();
        using IDisposable registration = ChangeToken.OnChange(configuration.GetReloadToken, reloaded.Set);
        File.WriteAllText(path, "RELOAD_TEST=two\n");

        reloaded.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        configuration["RELOAD_TEST"].ShouldBe("two");
        ((IDisposable)configuration.Providers.Single()).Dispose();
    }

    [Fact]
    public void TestDoesNotWatchTheFileWithoutReloadOnChange()
    {
        string path = Write("NO_RELOAD_TEST=one\n");
        IConfigurationRoot configuration = new ConfigurationBuilder().AddDotenvx(options => options.Path = [path]).Build();
        bool reloaded = false;
        using IDisposable registration = ChangeToken.OnChange(configuration.GetReloadToken, () => reloaded = true);

        File.WriteAllText(path, "NO_RELOAD_TEST=two\n");
        Thread.Sleep(500);

        reloaded.ShouldBeFalse();
        configuration["NO_RELOAD_TEST"].ShouldBe("one");
    }
}
