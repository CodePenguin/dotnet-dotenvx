using Dotenvx.Interop.DotenvxJs.Types;

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
    public void TestOptionsPropertyStoresAction()
    {
        bool actionCalled = false;
        DotenvxConfigurationSource source = new(_ => actionCalled = true);

        source.Options.ShouldNotBeNull();
        ConfigOptions configOptions = new();
        source.Options!(configOptions);
        actionCalled.ShouldBeTrue();
    }

    [Fact]
    public void TestConstructorWithNullOptionsSetsOptionsToNull()
    {
        DotenvxConfigurationSource source = new();

        source.Options.ShouldBeNull();
    }
}