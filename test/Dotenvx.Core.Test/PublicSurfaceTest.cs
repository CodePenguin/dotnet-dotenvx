using System.Reflection;

using Shouldly;

namespace Dotenvx.Core.Test;

/// <remarks>
/// The `Interop.*` namespaces hold ports of upstream internals and are not part of the public API (CONTRIBUTING.md, "Public surface").
/// This fails when one of their types becomes public, whether by accident or because a public member started exposing it.
/// </remarks>
public class PublicSurfaceTest
{
    public static IEnumerable<object[]> Assemblies()
    {
        yield return [typeof(global::Dotenvx.Dotenvx).Assembly];
        yield return [typeof(global::Dotenvx.Primitives.Primitives).Assembly];
        yield return [typeof(global::Dotenvx.Providers.ProviderChain).Assembly];
    }

    [Theory]
    [MemberData(nameof(Assemblies))]
    public void NoInteropTypeIsPublic(Assembly assembly)
    {
        string[] leaked = [.. assembly.GetExportedTypes()
            .Where(type => type.Namespace?.Contains("Interop", StringComparison.Ordinal) == true)
            .Select(type => type.FullName!)
            .Order(StringComparer.Ordinal)];

        leaked.ShouldBeEmpty();
    }
}
