using Dotenvx.Core.Interop.DotenvxJs.Types;

using Microsoft.Extensions.Configuration;

using Shouldly;

namespace Dotenvx.Configuration.Test;

public class DotenvxConfigurationSourceTest
{
    [Fact]
    public void Build_ReturnsDotenvxConfigurationProvider()
    {
        DotenvxConfigurationSource source = new();
        ConfigurationBuilder builder = new();

        IConfigurationProvider provider = source.Build(builder);

        provider.ShouldBeOfType<DotenvxConfigurationProvider>();
    }

    [Fact]
    public void Options_Property_StoresAction()
    {
        bool actionCalled = false;
        DotenvxConfigurationSource source = new(_ => actionCalled = true);

        source.Options.ShouldNotBeNull();
        ConfigOptions configOptions = new();
        source.Options!(configOptions);
        actionCalled.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithNullOptions_SetsOptionsToNull()
    {
        DotenvxConfigurationSource source = new();

        source.Options.ShouldBeNull();
    }
}