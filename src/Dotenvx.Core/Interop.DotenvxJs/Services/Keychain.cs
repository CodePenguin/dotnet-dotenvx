using Dotenvx.Interop.DotenvxJs.Helpers;

using static Dotenvx.Interop.DotenvxJs.Helpers.EnvKeyFiles;
using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Interop.DotenvxJs.Helpers.UpsertEnvKeyHelper;

namespace Dotenvx.Interop.DotenvxJs.Services;

internal record KeychainResult(
    bool Changed, string? Label, string PrivateKeyName, string? PrivateKeyValue, string PublicKeyValue);

/// <summary>Shared bits of the four `native` services (keychainUp/Down/Push/Pull.js).</summary>
internal abstract class KeychainService(string? envFile, string? envKeysFile)
{
    protected string EnvFile { get; } = envFile ?? ".env";
    protected string EnvKeysFile { get; } = envKeysFile ?? ".env.keys";

    public abstract KeychainResult Run();

    protected string ReadPublicKey()
    {
        // readEnvKey(publicKeyName, envFile, { strict: true, ignore: ['MISSING_PRIVATE_KEY'] })
        return ReadEnvKey(GuessPublicKeyName(EnvFile), EnvFile, true, ["MISSING_PRIVATE_KEY"])!;
    }

    protected static string Label(string publicKey)
    {
        return $"dotenvx ({ArmoredKeyDisplay.Display(publicKey)})";
    }

    protected static string NotFoundMessage(string publicKey)
    {
        return $"[NOT_FOUND] private key not found in {NativeStore.StoreName} ({ArmoredKeyDisplay.Display(publicKey)}). fix: [dotenvx native up]";
    }
}

/// <remarks>Reference source: dotenvx/src/lib/services/keychainUp.js</remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
internal class KeychainUp(string? envFile = null, string? envKeysFile = null) : KeychainService(envFile, envKeysFile)
{
    private static readonly Func<string?, string?, KeychainUp> DefaultFactory = (f, k) => new KeychainUp(f, k);

    public static Func<string?, string?, KeychainUp> Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public override KeychainResult Run()
    {
        string privateKeyName = GuessPrivateKeyName(EnvFile);
        string publicKey = ReadPublicKey();
        string label = Label(publicKey);
        string? existingPrivateKey = null;

        try
        {
            existingPrivateKey = NativeStore.Current.Get(publicKey);
        }
        catch
        {
            // keychainUp.js: `catch {}`
        }

        string? privateKey = ReadEnvKey(privateKeyName, EnvKeysFile, string.IsNullOrEmpty(existingPrivateKey));

        if (!string.IsNullOrEmpty(existingPrivateKey))
        {
            if (privateKey is null)
            {
                return new KeychainResult(false, label, privateKeyName, existingPrivateKey, publicKey);
            }

            if (existingPrivateKey == privateKey)
            {
                RemoveEnvKeyResult result = RemoveEnvKey(privateKeyName, EnvKeysFile);

                return new KeychainResult(result.Changed, label, privateKeyName, privateKey, publicKey);
            }
        }

        NativeStore.Current.Set(publicKey, privateKey!, label);
        if (NativeStore.Current.Get(publicKey) != privateKey)
        {
            throw new Exception("could not verify private key in OS secret store; .env.keys unchanged");
        }

        RemoveEnvKey(privateKeyName, EnvKeysFile);

        return new KeychainResult(true, label, privateKeyName, privateKey, publicKey);
    }
}

/// <remarks>Reference source: dotenvx/src/lib/services/keychainDown.js</remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
internal class KeychainDown(string? envFile = null, string? envKeysFile = null) : KeychainService(envFile, envKeysFile)
{
    private static readonly Func<string?, string?, KeychainDown> DefaultFactory = (f, k) => new KeychainDown(f, k);

    public static Func<string?, string?, KeychainDown> Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public override KeychainResult Run()
    {
        string privateKeyName = GuessPrivateKeyName(EnvFile);
        string publicKey = ReadPublicKey();
        string? privateKey;

        try
        {
            privateKey = NativeStore.Current.Get(publicKey);
            if (string.IsNullOrEmpty(privateKey))
            {
                throw new InvalidOperationException(NotFoundMessage(publicKey));
            }
        }
        catch (Exception)
        {
            privateKey = ReadEnvKey(privateKeyName, EnvKeysFile);

            if (!string.IsNullOrEmpty(privateKey))
            {
                return new KeychainResult(false, null, privateKeyName, privateKey, publicKey);
            }

            throw;
        }

        UpsertEnvKey(privateKeyName, privateKey, EnvKeysFile);
        NativeStore.Current.Delete(publicKey);

        return new KeychainResult(true, null, privateKeyName, privateKey, publicKey);
    }
}

/// <remarks>Reference source: dotenvx/src/lib/services/keychainPush.js</remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
internal class KeychainPush(string? envFile = null, string? envKeysFile = null) : KeychainService(envFile, envKeysFile)
{
    private static readonly Func<string?, string?, KeychainPush> DefaultFactory = (f, k) => new KeychainPush(f, k);

    public static Func<string?, string?, KeychainPush> Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public override KeychainResult Run()
    {
        string privateKeyName = GuessPrivateKeyName(EnvFile);
        string publicKey = ReadPublicKey();
        string privateKey = ReadEnvKey(privateKeyName, EnvKeysFile, true)!;
        string label = Label(publicKey);

        try
        {
            if (NativeStore.Current.Get(publicKey) == privateKey)
            {
                return new KeychainResult(false, label, privateKeyName, privateKey, publicKey);
            }
        }
        catch
        {
            // keychainPush.js: `catch {}`
        }

        NativeStore.Current.Set(publicKey, privateKey, label);

        return new KeychainResult(true, label, privateKeyName, privateKey, publicKey);
    }
}

/// <remarks>Reference source: dotenvx/src/lib/services/keychainPull.js</remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
internal class KeychainPull(string? envFile = null, string? envKeysFile = null) : KeychainService(envFile, envKeysFile)
{
    private static readonly Func<string?, string?, KeychainPull> DefaultFactory = (f, k) => new KeychainPull(f, k);

    public static Func<string?, string?, KeychainPull> Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public override KeychainResult Run()
    {
        string privateKeyName = GuessPrivateKeyName(EnvFile);
        string publicKey = ReadPublicKey();
        string? privateKey;

        try
        {
            privateKey = NativeStore.Current.Get(publicKey);
            if (string.IsNullOrEmpty(privateKey))
            {
                throw new InvalidOperationException("not found");
            }
        }
        catch
        {
            throw new InvalidOperationException(NotFoundMessage(publicKey));
        }

        UpsertEnvKeyResult result = UpsertEnvKey(privateKeyName, privateKey, EnvKeysFile);

        return new KeychainResult(result.Changed, null, privateKeyName, privateKey, publicKey);
    }
}
