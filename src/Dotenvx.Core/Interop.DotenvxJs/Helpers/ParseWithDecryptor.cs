using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Api;
using Dotenvx.Primitives;
using Dotenvx.Providers;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/parseWithDecryptor.js. Keys are looked up in the process environment, then the
/// key files (`fk`), then the custom provider chain; every key found is attributed to the place it came from
/// (<see cref="Outcome{T}.KeySources" />). A server-side-only key is decrypted by the decryptor instead.
/// </remarks>
public static class ParseWithDecryptor
{
    private const string ServerSideDecryptionRequired = "SERVER_SIDE_DECRYPTION_REQUIRED";

    /// <summary>The parse result, the error that forced the key-less retry (`failedKeyAccessFallback`), and where each key came from.</summary>
    public record Outcome<T>(T Output, Exception? LeadingError, IReadOnlyDictionary<string, string> KeySources);

    private static DecryptorOptions DecryptOptions(Exception error)
    {
        JsonNode? meta = (error as ApiException)?.Meta;

        return new DecryptorOptions(meta?["public_key"]?.ToString(), meta?["grant_token"]?.ToString(), error);
    }

    /// <summary>The options of the parse: key files read through the file provider first, then the given provider.</summary>
    private sealed record Prepared(PrimitivesParseOptions Options, string[]? FallbackFk, Dictionary<string, string> KeySources);

    private static Prepared WithFileKeys(string src, PrimitivesParseOptions options, Func<string, string?>? sourceFor)
    {
        Dictionary<string, string> keySources = [];
        ProviderOptions fileOptions = new() { Fk = options.Fk };
        IProviderLookup keys = new FileLookup(fileOptions);
        Func<string, IDictionary<string, string>?>? fallback = options.Provider;
        string[] publicKeys = Primitives.Primitives.PublicKeys(src);

        // keys exported in the environment
        Dictionary<string, string> environment = Primitives.Primitives.KeyringSync(new KeyringOptions
        {
            ProcessEnv = options.ProcessEnv, Fk = [], Provider = null
        });
        foreach (string key in publicKeys)
        {
            if (environment.TryGetValue(key, out string? value) && !string.IsNullOrEmpty(value))
            {
                keySources[key] = "environment";
            }
        }

        IDictionary<string, string>? Found(string publicKey, IDictionary<string, string>? ring, string? source)
        {
            if (ring is not null && ring.TryGetValue(publicKey, out string? value) && !string.IsNullOrEmpty(value))
            {
                keySources[publicKey] = string.IsNullOrEmpty(source) ? "custom" : source;
            }

            return ring;
        }

        IDictionary<string, string>? Provider(string publicKey)
        {
            IDictionary<string, string> ring = keys.GetSync(publicKey);
            if (ring.TryGetValue(publicKey, out string? value) && !string.IsNullOrEmpty(value))
            {
                return Found(publicKey, ring, ".env.keys");
            }

            return Found(publicKey, fallback is null ? new Dictionary<string, string>() : fallback(publicKey),
                fallback is not null ? sourceFor?.Invoke(publicKey) : null);
        }

        PrimitivesParseOptions prepared = new()
        {
            Overload = options.Overload,
            Ik = options.Ik,
            Ek = options.Ek,
            ProcessEnv = options.ProcessEnv,
            // Provider lookup requires a public key. Keep legacy key discovery for encrypted files that do not declare one,
            // and for error recovery.
            Fk = publicKeys.Length > 0 ? [] : options.Fk,
            Provider = Provider,
            ReadFile = options.ReadFile
        };

        return new Prepared(prepared, options.Fk, keySources);
    }

    // the file provider, narrowed to the part used here
    private interface IProviderLookup
    {
        IDictionary<string, string> GetSync(string publicKey);
    }

    private sealed class FileLookup(ProviderOptions options) : IProviderLookup
    {
        public IDictionary<string, string> GetSync(string publicKey)
        {
            return new FileProvider(options).GetSync(publicKey);
        }
    }

    private static PrimitivesParseOptions WithoutProvider(Prepared prepared)
    {
        PrimitivesParseOptions options = prepared.Options;

        return new PrimitivesParseOptions
        {
            Overload = options.Overload,
            Ik = options.Ik,
            Ek = options.Ek,
            ProcessEnv = options.ProcessEnv,
            Fk = prepared.FallbackFk,
            ReadFile = options.ReadFile
        };
    }

    private static Outcome<T> ParseWith<T>(Func<string, PrimitivesParseOptions, T> parser, string src,
        PrimitivesParseOptions given, Decryptor? decryptor, Func<string, string?>? sourceFor)
    {
        Prepared prepared = WithFileKeys(src, given, sourceFor);
        try
        {
            return new Outcome<T>(parser(src, prepared.Options), null, prepared.KeySources);
        }
        catch (Exception error)
        {
            if (error is not ApiException { Code: ServerSideDecryptionRequired } || decryptor is null)
            {
                return new Outcome<T>(parser(src, WithoutProvider(prepared)), error, new Dictionary<string, string>());
            }

            try
            {
                DecryptorOptions decryptOptions = DecryptOptions(error);
                DecryptorResult result = decryptor(src, decryptOptions);

                return new Outcome<T>(parser(result.Src, WithoutProvider(prepared)), null,
                    new Dictionary<string, string> { [decryptOptions.PublicKey ?? "undefined"] = "armor" });
            }
            catch (Exception decryptorError)
            {
                return new Outcome<T>(parser(src, WithoutProvider(prepared)), decryptorError, new Dictionary<string, string>());
            }
        }
    }

    private static readonly Func<string, PrimitivesParseOptions, PrimitivesParseOutput> DefaultParse =
        (src, options) => Primitives.Primitives.ParseSync(src, options);

    private static readonly Func<string, PrimitivesParseOptions, PrimitivesParseArraysOutput> DefaultParseArrays =
        (src, options) => Primitives.Primitives.ParseArraysSync(src, options);

    /// <summary>The primitives `parseSync` (upstream tests swap the package with a stub).</summary>
    public static Func<string, PrimitivesParseOptions, PrimitivesParseOutput> Parser { get; internal set; } = DefaultParse;

    /// <summary>The primitives `parsearraysSync`.</summary>
    public static Func<string, PrimitivesParseOptions, PrimitivesParseArraysOutput> ArraysParser { get; internal set; } =
        DefaultParseArrays;

    internal static void ResetToDefaults()
    {
        Parser = DefaultParse;
        ArraysParser = DefaultParseArrays;
    }

    public static Outcome<PrimitivesParseOutput> Sync(string src, PrimitivesParseOptions options, Decryptor? decryptor,
        Func<string, string?>? sourceFor = null)
    {
        return ParseWith(Parser, src, options, decryptor, sourceFor);
    }

    public static Outcome<PrimitivesParseArraysOutput> ArraysSync(string src, PrimitivesParseOptions options,
        Decryptor? decryptor, Func<string, string?>? sourceFor = null)
    {
        return ParseWith(ArraysParser, src, options, decryptor, sourceFor);
    }
}
