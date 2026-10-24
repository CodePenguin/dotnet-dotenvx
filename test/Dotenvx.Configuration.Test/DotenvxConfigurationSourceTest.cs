using Microsoft.Extensions.Configuration;

using Shouldly;

namespace Dotenvx.Configuration.Test;

public class DotenvxConfigurationSourceTest
{
    [Fact]
    public void TestBuildReturnsDotenvxConfigurationProvider()
    {
        DotenvxConfigurationSource source = new();
        ConfigurationBuilder builder = new();

        IConfigurationProvider provider = source.Build(builder);

        provider.ShouldBeOfType<DotenvxConfigurationProvider>();
    }

    [Fact]
    public void TestConfigureOptionsStoresAction()
    {
        bool actionCalled = false;
        DotenvxConfigurationSource source = new() { ConfigureOptions = _ => actionCalled = true };

        source.ConfigureOptions.ShouldNotBeNull();
        source.ConfigureOptions!(new ConfigOptions());
        actionCalled.ShouldBeTrue();
    }

    [Fact]
    public void TestDefaults()
    {
        DotenvxConfigurationSource source = new();

        source.ConfigureOptions.ShouldBeNull();
        source.ReloadOnChange.ShouldBeFalse();
        source.ReloadDelay.ShouldBe(250);
    }

    [Fact]
    public void TestAddDotenvxConfiguresTheSource()
    {
        ConfigurationBuilder builder = new();

        builder.AddDotenvx(source =>
        {
            source.ReloadOnChange = true;
            source.ReloadDelay = 10;
        });

        DotenvxConfigurationSource source = builder.Sources.OfType<DotenvxConfigurationSource>().Single();
        source.ReloadOnChange.ShouldBeTrue();
        source.ReloadDelay.ShouldBe(10);
    }
}
