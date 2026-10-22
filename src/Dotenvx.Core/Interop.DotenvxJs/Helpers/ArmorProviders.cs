using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Api;
using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Primitives;
using Dotenvx.Providers;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <summary>`decryptOptions(error)` of parseWithDecryptor.js: what the Armor API reported about a server-side-only key.</summary>
internal record DecryptorOptions(string? PublicKey, string? GrantToken, Exception? Error)
{
    public Action<string>? OnStatus { get; init; }
    public string? Token { get; init; }
    public string? Command { get; init; }
}

/// <summary>`{ src }`: the source with its values decrypted server side.</summary>
internal record DecryptorResult(string Src);

internal delegate DecryptorResult Decryptor(string src, DecryptorOptions options);

/// <remarks>Reference source: dotenvx/src/lib/decryptors/armor/index.js (the server-side decryptor).</remarks>
internal static class ArmorProviders
{
    private static readonly Func<IDotenvxLogger?, Session> DefaultSessionFactory = logger => new Session(logger);

    /// <summary>`new Session()` (upstream tests stub the Session class).</summary>
    public static Func<IDotenvxLogger?, Session> SessionFactory { get; internal set; } = DefaultSessionFactory;

    internal static void ResetToDefaults()
    {
        SessionFactory = DefaultSessionFactory;
    }

    /// <summary>decryptors/armor: posts the source to `/api/armor/decrypt` and returns the decrypted source.</summary>
    public static DecryptorResult DecryptSrc(string src, DecryptorOptions options, IDotenvxLogger? logger = null)
    {
        Session session = SessionFactory(logger);
        string? token = !string.IsNullOrEmpty(options.Token) ? options.Token : session.Token();
        JsonNode json = new ArmorProvider(new ProviderOptions
        {
            Hostname = session.Hostname(),
            Token = token,
            DevicePublicKey = session.DevicePublicKey(),
            CliVersion = PackageJson.Version
        }).DecryptSync(src, new ArmorDecryptContext(options.PublicKey, options.GrantToken));

        return new DecryptorResult(json["src"]?.ToString() ?? "");
    }
}
