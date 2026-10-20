using NSubstitute;
using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Api;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Primitives;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/parseWithDecryptor.js. Upstream swaps `@dotenvx/primitives` for stubs;
/// here `ParseWithDecryptor.Parser` / `ArraysParser` are the seams. Errors that upstream attaches as `error.meta` /
/// `error.code` are ApiException instances; the "async" and "sync" variants share one synchronous implementation.
/// </remarks>
public class ParseWithDecryptorTest : EnvironmentTestBase
{
    private const string PublicKey = "02b106c30579baf896ae1fddf077cbcb4fef5e7d457932974878dcb51f42b45498";

    private static ApiException ServerSideDecryptionRequired()
    {
        return new ApiException("SERVER_SIDE_DECRYPTION_REQUIRED",
            "[SERVER_SIDE_DECRYPTION_REQUIRED] server-side decryption required")
        {
            Meta = System.Text.Json.Nodes.JsonNode.Parse("""{"public_key":"public-key","grant_token":"grant-token-1"}""")
        };
    }

    private static PrimitivesParseOutput Output(Dictionary<string, string> parsed, params PrimitivesError[] errors)
    {
        return new PrimitivesParseOutput(parsed, [], [], [.. errors]);
    }

    private static ApiException Permission(string message = "[PERMISSION_DENIED] denied")
    {
        return new ApiException("PERMISSION_DENIED", message);
    }

    /// <remarks>Refer to "parseWithDecryptor preserves a real encrypted parse when the Armor provider denies access"</remarks>
    [Fact]
    public void TestParseWithDecryptorPreservesARealEncryptedParseWhenTheArmorProviderDeniesAccess()
    {
        string encryptedValue = Primitives.Primitives.Encrypt("hello", PublicKey);
        string src = $"DOTENV_PUBLIC_KEY=\"{PublicKey}\"\nHELLO=\"{encryptedValue}\"\n";
        Exception permissionError = Permission("[PERMISSION_DENIED] ask an owner or admin for armored key access (02B 106)");
        int calls = 0;

        var result = ParseWithDecryptor.Sync(src, new PrimitivesParseOptions
        {
            Fk = [],
            ProcessEnv = new Dictionary<string, string>(),
            Provider = _ =>
            {
                calls++;

                throw permissionError;
            }
        }, null);

        result.Output.Parsed.ShouldBe(new Dictionary<string, string>
        {
            ["DOTENV_PUBLIC_KEY"] = PublicKey, ["HELLO"] = encryptedValue
        });
        result.LeadingError.ShouldBeSameAs(permissionError);
        result.Output.Errors[0].Code.ShouldBe("DECRYPTION_FAILED");
        calls.ShouldBe(1);
    }

    /// <remarks>Refer to "parseWithDecryptor delegates server-side decryption and reparses plaintext"</remarks>
    [Fact]
    public void TestParseWithDecryptorDelegatesServerSideDecryptionAndReparsesPlaintext()
    {
        ApiException error = ServerSideDecryptionRequired();
        var sources = new List<string>();
        var secondOptions = new List<PrimitivesParseOptions>();
        ParseWithDecryptor.Parser = (src, options) =>
        {
            sources.Add(src);
            if (sources.Count == 1)
            {
                throw error;
            }

            secondOptions.Add(options);

            return Output(new Dictionary<string, string> { ["HELLO"] = "World" });
        };
        DecryptorOptions? seen = null;
        string? decryptedSrc = null;

        var result = ParseWithDecryptor.Sync("HELLO=encrypted:ciphertext\n",
            new PrimitivesParseOptions { Provider = _ => null },
            (src, options) =>
            {
                decryptedSrc = src;
                seen = options;

                return new DecryptorResult("HELLO=World\n");
            });

        result.Output.Parsed.ShouldBe(new Dictionary<string, string> { ["HELLO"] = "World" });
        result.Output.Errors.ShouldBeEmpty();
        decryptedSrc.ShouldBe("HELLO=encrypted:ciphertext\n");
        seen!.PublicKey.ShouldBe("public-key");
        seen.GrantToken.ShouldBe("grant-token-1");
        seen.Error.ShouldBeSameAs(error);
        sources[1].ShouldBe("HELLO=World\n");
        secondOptions[0].Provider.ShouldBeNull();
    }

    /// <remarks>Refer to "parseWithDecryptor rethrows errors that do not request server-side decryption"</remarks>
    [Fact]
    public void TestParseWithDecryptorRethrowsErrorsThatDoNotRequestServerSideDecryption()
    {
        var error = new ApiException("NETWORK_ERROR", "network failed");
        ParseWithDecryptor.Parser = (_, _) => throw error;
        int decryptorCalls = 0;

        Should.Throw<ApiException>(() => ParseWithDecryptor.Sync("HELLO=encrypted:ciphertext\n",
            new PrimitivesParseOptions(), (_, _) =>
            {
                decryptorCalls++;

                return new DecryptorResult("");
            })).ShouldBeSameAs(error);

        decryptorCalls.ShouldBe(0);
    }

    /// <remarks>Refer to "parseWithDecryptor preserves encrypted src when a key provider fails"</remarks>
    [Fact]
    public void TestParseWithDecryptorPreservesEncryptedSrcWhenAKeyProviderFails()
    {
        Exception permissionError = Permission("[PERMISSION_DENIED] ask an owner or admin for armored key access (029 4B2)");
        var decryptionError = new PrimitivesError("DECRYPTION_FAILED", "could not decrypt HELLO");
        int calls = 0;
        PrimitivesParseOptions? second = null;
        ParseWithDecryptor.Parser = (_, options) =>
        {
            if (++calls == 1)
            {
                throw permissionError;
            }

            second = options;

            return Output(new Dictionary<string, string> { ["DOTENV_PUBLIC_KEY"] = "0294b2", ["HELLO"] = "encrypted:ciphertext" },
                decryptionError);
        };

        var result = ParseWithDecryptor.Sync("DOTENV_PUBLIC_KEY=0294b2\nHELLO=encrypted:ciphertext\n",
            new PrimitivesParseOptions { Provider = _ => null }, (_, _) => new DecryptorResult(""));

        result.Output.Parsed["HELLO"].ShouldBe("encrypted:ciphertext");
        result.LeadingError.ShouldBeSameAs(permissionError);
        result.Output.Errors.ShouldBe([decryptionError]);
        second!.Provider.ShouldBeNull();
    }

    /// <remarks>Refer to "parseWithDecryptor preserves encrypted src and the error when the decryptor is offline"</remarks>
    [Fact]
    public void TestParseWithDecryptorPreservesEncryptedSrcAndTheErrorWhenTheDecryptorIsOffline()
    {
        var networkError = new HttpRequestFailedException("ENETUNREACH", "connect ENETUNREACH armor.dotenvx.com");
        var sources = new List<string>();
        ParseWithDecryptor.Parser = (src, _) =>
        {
            sources.Add(src);
            if (sources.Count == 1)
            {
                throw ServerSideDecryptionRequired();
            }

            return Output(new Dictionary<string, string> { ["HELLO"] = "encrypted:ciphertext" },
                new PrimitivesError("DECRYPTION_FAILED", "could not decrypt HELLO"));
        };

        var result = ParseWithDecryptor.Sync("HELLO=encrypted:ciphertext\n", new PrimitivesParseOptions(),
            (_, _) => throw networkError);

        result.Output.Parsed["HELLO"].ShouldBe("encrypted:ciphertext");
        result.LeadingError.ShouldBeSameAs(networkError);
        result.Output.Errors[0].Code.ShouldBe("DECRYPTION_FAILED");
        sources[1].ShouldBe("HELLO=encrypted:ciphertext\n");
    }

    /// <remarks>Refer to "parseWithDecryptor preserves encrypted src and the permission error when Armor access is denied"</remarks>
    [Fact]
    public void TestParseWithDecryptorPreservesEncryptedSrcAndThePermissionErrorWhenArmorAccessIsDenied()
    {
        Exception permissionError = Permission("[PERMISSION_DENIED] ask an owner or admin for armored key access (029 4B2)");
        var decryptionError = new PrimitivesError("DECRYPTION_FAILED", "could not decrypt HELLO");
        int calls = 0;
        ParseWithDecryptor.Parser = (_, _) =>
        {
            if (++calls == 1)
            {
                throw ServerSideDecryptionRequired();
            }

            return Output(new Dictionary<string, string> { ["DOTENV_PUBLIC_KEY"] = "0294b2", ["HELLO"] = "encrypted:ciphertext" },
                decryptionError);
        };

        var result = ParseWithDecryptor.Sync("DOTENV_PUBLIC_KEY=0294b2\nHELLO=encrypted:ciphertext\n",
            new PrimitivesParseOptions(), (_, _) => throw permissionError);

        result.LeadingError.ShouldBeSameAs(permissionError);
        result.Output.Errors.ShouldBe([decryptionError]);
    }

    /// <remarks>Refer to "parseWithDecryptor.sync delegates through a synchronous decryptor"</remarks>
    [Fact]
    public void TestParseWithDecryptorSyncDelegatesThroughASynchronousDecryptor()
    {
        int calls = 0;
        ParseWithDecryptor.Parser = (_, _) => ++calls == 1
            ? throw ServerSideDecryptionRequired()
            : Output(new Dictionary<string, string> { ["HELLO"] = "World" });
        int decryptorCalls = 0;

        var result = ParseWithDecryptor.Sync("HELLO=encrypted:ciphertext\n", new PrimitivesParseOptions(),
            (_, _) =>
            {
                decryptorCalls++;

                return new DecryptorResult("HELLO=World\n");
            });

        result.Output.Parsed["HELLO"].ShouldBe("World");
        decryptorCalls.ShouldBe(1);
    }

    /// <remarks>Refer to "parseWithDecryptor.sync preserves encrypted src when a key provider fails"</remarks>
    [Fact]
    public void TestParseWithDecryptorSyncPreservesEncryptedSrcWhenAKeyProviderFails()
    {
        Exception permissionError = Permission();
        int calls = 0;
        ParseWithDecryptor.Parser = (_, _) => ++calls == 1
            ? throw permissionError
            : Output(new Dictionary<string, string> { ["HELLO"] = "encrypted:ciphertext" });

        var result = ParseWithDecryptor.Sync("HELLO=encrypted:ciphertext\n",
            new PrimitivesParseOptions { Provider = _ => null }, (_, _) => new DecryptorResult(""));

        result.Output.Parsed["HELLO"].ShouldBe("encrypted:ciphertext");
        result.LeadingError.ShouldBeSameAs(permissionError);
    }

    /// <remarks>Refer to "parseWithDecryptor.sync preserves encrypted src and the permission error when Armor access is denied"</remarks>
    [Fact]
    public void TestParseWithDecryptorSyncPreservesEncryptedSrcAndThePermissionErrorWhenArmorAccessIsDenied()
    {
        Exception permissionError = Permission();
        int calls = 0;
        ParseWithDecryptor.Parser = (_, _) => ++calls == 1
            ? throw ServerSideDecryptionRequired()
            : Output(new Dictionary<string, string> { ["HELLO"] = "encrypted:ciphertext" });

        var result = ParseWithDecryptor.Sync("HELLO=encrypted:ciphertext\n", new PrimitivesParseOptions(),
            (_, _) => throw permissionError);

        result.Output.Parsed["HELLO"].ShouldBe("encrypted:ciphertext");
        result.LeadingError.ShouldBeSameAs(permissionError);
    }

    /// <remarks>Refer to "parseWithDecryptor.arrays preserves duplicate assignments: ${scenario}"</remarks>
    [Theory]
    [InlineData("local")]
    [InlineData("remote")]
    [InlineData("provider-denied")]
    [InlineData("decryptor-offline")]
    public void TestParseWithDecryptorArraysPreservesDuplicateAssignments(string scenario)
    {
        (string publicKey, string privateKey) = Primitives.Primitives.Keypair();
        string first = Primitives.Primitives.Encrypt("first", publicKey);
        string second = Primitives.Primitives.Encrypt("second", publicKey);
        string src = $"DOTENV_PUBLIC_KEY={publicKey}\nHELLO={first}\nHELLO={second}\n";
        ApiException denied = Permission("access denied");
        var offline = new HttpRequestFailedException("ENETUNREACH", "offline");
        var processEnv = new Dictionary<string, string>();
        var options = new PrimitivesParseOptions { Fk = [], ProcessEnv = processEnv };
        Decryptor? decryptor = null;

        if (scenario == "local")
        {
            processEnv["DOTENV_PRIVATE_KEY"] = privateKey;
        }
        else if (scenario == "provider-denied")
        {
            options = new PrimitivesParseOptions { Fk = [], ProcessEnv = processEnv, Provider = _ => throw denied };
        }
        else
        {
            options = new PrimitivesParseOptions
            {
                Fk = [], ProcessEnv = processEnv, Provider = _ => throw ServerSideDecryptionRequired()
            };
            decryptor = (_, _) => scenario == "decryptor-offline"
                ? throw offline
                : new DecryptorResult("HELLO=first\nHELLO=second\n");
        }

        var result = ParseWithDecryptor.ArraysSync(src, options, decryptor);

        bool failed = scenario is "provider-denied" or "decryptor-offline";
        result.Output.Parsed["HELLO"].ShouldBe(failed ? [first, second] : ["first", "second"]);
        if (failed)
        {
            result.LeadingError.ShouldBeSameAs(scenario == "provider-denied" ? denied : offline);
            result.Output.Errors[0].Code.ShouldBe("DECRYPTION_FAILED");
        }
        else
        {
            result.Output.Errors.ShouldBeEmpty();
            result.Output.Injected["HELLO"].ShouldBe(["first", "second"]);
        }
    }

    // Reference source for the four tests below: dotenvx/tests/lib/decryptors/index.test.js. Upstream swaps the Session class and the
    // Armor decryptor with proxyquire; here `ArmorProviders.SessionFactory` and `KeyProviders.ArmorDecryptor` are the seams, and
    // there is no worker (the one synchronous implementation serves both upstream variants).
    private static Session ArmorSession(bool noArmor)
    {
        Session session = NSubstitute.Substitute.For<Session>((global::Dotenvx.IDotenvxLogger?)null);
        session.NoArmor().Returns(noArmor);
        ArmorProviders.SessionFactory = _ => session;

        return session;
    }

    /// <remarks>Refer to "decryptors returns a custom decryptor unchanged"</remarks>
    [Fact]
    public void DecryptorsReturnsACustomDecryptorUnchanged()
    {
        ArmorSession(noArmor: false);
        Decryptor custom = (_, _) => new DecryptorResult("");

        KeyProviders.ResolveDecryptor(new KeyAccessOptions { HasCustomDecryptor = true, CustomDecryptor = custom }).ShouldBeSameAs(custom);
        KeyProviders.ResolveDecryptor(new KeyAccessOptions { HasCustomDecryptor = true, CustomDecryptor = null }).ShouldBeNull();
    }

    /// <remarks>Refer to "decryptors returns null when Armor is unavailable or disabled"</remarks>
    [Fact]
    public void DecryptorsReturnsNullWhenArmorIsUnavailableOrDisabled()
    {
        ArmorSession(noArmor: true);
        KeyProviders.ResolveDecryptor(new KeyAccessOptions()).ShouldBeNull();

        ArmorSession(noArmor: false);
        KeyProviders.ResolveDecryptor(new KeyAccessOptions { NoArmor = true }).ShouldBeNull();
    }

    /// <remarks>Refer to "decryptors returns an Armor decryptor"</remarks>
    [Fact]
    public void DecryptorsReturnsAnArmorDecryptor()
    {
        ArmorSession(noArmor: false);
        string? seenSrc = null;
        DecryptorOptions? seenOptions = null;
        KeyProviders.ArmorDecryptor = (src, options, _) =>
        {
            seenSrc = src;
            seenOptions = options;

            return new DecryptorResult("HELLO=World\n");
        };

        Decryptor decryptor = KeyProviders.ResolveDecryptor(new KeyAccessOptions { Token = "token-1", Command = "run" })!;
        DecryptorResult result = decryptor("HELLO=encrypted:ciphertext\n", new DecryptorOptions("public-key", "grant-token-1", null));

        result.ShouldBe(new DecryptorResult("HELLO=World\n"));
        seenSrc.ShouldBe("HELLO=encrypted:ciphertext\n");
        seenOptions!.PublicKey.ShouldBe("public-key");
        seenOptions.GrantToken.ShouldBe("grant-token-1");
        seenOptions.Token.ShouldBe("token-1");
        seenOptions.Command.ShouldBe("run");
        seenOptions.OnStatus.ShouldBeNull();
    }

    /// <remarks>Refer to "decryptors.sync runs the Armor decryptor through a worker"</remarks>
    [Fact]
    public void DecryptorsSyncRunsTheArmorDecryptorThroughAWorker()
    {
        // The port has no worker: the decryptor is already synchronous, so the same call returns the result directly,
        // and the status callback travels with it like the other options.
        ArmorSession(noArmor: false);
        DecryptorOptions? seenOptions = null;
        KeyProviders.ArmorDecryptor = (_, options, _) =>
        {
            seenOptions = options;

            return new DecryptorResult("HELLO=World\n");
        };
        Action<string> onStatus = _ => { };

        Decryptor decryptor = KeyProviders.ResolveDecryptor(new KeyAccessOptions { Token = "token-1", Command = "run", OnStatus = onStatus })!;
        DecryptorResult result = decryptor("HELLO=encrypted:ciphertext\n", new DecryptorOptions("public-key", "grant-token-1", null));

        result.Src.ShouldBe("HELLO=World\n");
        seenOptions!.OnStatus.ShouldBeSameAs(onStatus);
        seenOptions.Token.ShouldBe("token-1");
    }
}
