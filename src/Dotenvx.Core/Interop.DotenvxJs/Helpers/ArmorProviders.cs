using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Api;
using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <summary>`decryptOptions(error)` of parseWithDecryptor.js: what the Armor API reported about a server-side-only key.</summary>
public record DecryptorOptions(string? PublicKey, string? GrantToken, Exception Error);

/// <summary>`{ src }`: the source with its values decrypted server side.</summary>
public record DecryptorResult(string Src);

public delegate DecryptorResult Decryptor(string src, DecryptorOptions options);

/// <remarks>
/// Reference source: dotenvx/src/lib/providers/armor/index.js (the key provider) and decryptors/armor/index.js (the
/// server-side decryptor).
/// </remarks>
public static class ArmorProviders
{
    /// <summary>providers/armor: `{ publicKey: privateKey }` from the Armor keyring API; network failures are "nothing found".</summary>
    public static IDictionary<string, string> Provider(string publicKeyHex, IDotenvxLogger? logger = null)
    {
        var session = new Session(logger);
        var keyring = new ArmorKeyring(session.Hostname(), session.Token(), session.DevicePublicKey(), publicKeyHex);

        try
        {
            JsonNode json = keyring.Run();
            var result = new Dictionary<string, string>();
            if (json is JsonObject obj)
            {
                foreach ((string key, JsonNode? value) in obj)
                {
                    result[key] = value?.ToString() ?? "";
                }
            }

            return result;
        }
        catch (Exception error) when (ArmorHttp.IsNetworkError(error))
        {
            return new Dictionary<string, string>();
        }
    }

    /// <summary>decryptors/armor: posts the source to `/api/armor/decrypt` and returns the decrypted source.</summary>
    public static DecryptorResult DecryptSrc(string src, DecryptorOptions options, IDotenvxLogger? logger = null)
    {
        var session = new Session(logger);
        JsonNode json = ArmorApi.ArmorDecrypt(session.Hostname(), session.Token(), session.DevicePublicKey(),
            options.PublicKey, src, options.GrantToken);

        return new DecryptorResult(json["src"]?.ToString() ?? "");
    }
}

/// <remarks>Reference source: dotenvx/src/lib/helpers/parseWithDecryptor.js</remarks>
public static class ParseWithDecryptor
{
    private const string ServerSideDecryptionRequired = "SERVER_SIDE_DECRYPTION_REQUIRED";

    /// <summary>The parse result plus the error that forced the key-less retry (`failedKeyAccessFallback`), if any.</summary>
    public record Outcome<T>(T Output, Exception? LeadingError);

    private static DecryptorOptions DecryptOptions(Exception error)
    {
        JsonNode? meta = (error as ApiException)?.Meta;

        return new DecryptorOptions(meta?["public_key"]?.ToString(), meta?["grant_token"]?.ToString(), error);
    }

    private static Outcome<T> ParseWith<T>(Func<string, PrimitivesParseOptions, T> parser, string src,
        PrimitivesParseOptions options, Decryptor? decryptor)
    {
        try
        {
            return new Outcome<T>(parser(src, options), null);
        }
        catch (Exception error)
        {
            if (error is not ApiException { Code: ServerSideDecryptionRequired } || decryptor is null)
            {
                if (options.Provider is null)
                {
                    throw;
                }

                return new Outcome<T>(parser(src, options.WithoutProvider()), error);
            }

            try
            {
                DecryptorResult result = decryptor(src, DecryptOptions(error));

                return new Outcome<T>(parser(result.Src, options.WithoutProvider()), null);
            }
            catch (Exception decryptorError)
            {
                return new Outcome<T>(parser(src, options.WithoutProvider()), decryptorError);
            }
        }
    }

    public static Outcome<PrimitivesParseOutput> Sync(string src, PrimitivesParseOptions options, Decryptor? decryptor)
    {
        return ParseWith(Primitives.Primitives.ParseSync, src, options, decryptor);
    }

    public static Outcome<PrimitivesParseArraysOutput> ArraysSync(string src, PrimitivesParseOptions options,
        Decryptor? decryptor)
    {
        return ParseWith(Primitives.Primitives.ParseArraysSync, src, options, decryptor);
    }
}
