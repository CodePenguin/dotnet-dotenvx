using Dotenvx.Interop.DotenvxJs.Custodians;
using Dotenvx.Interop.DotenvxJs.Helpers;

using static Dotenvx.Interop.DotenvxJs.Helpers.EnvKeyFiles;
using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Interop.DotenvxJs.Helpers.UpsertEnvKeyHelper;

namespace Dotenvx.Interop.DotenvxJs.Services;

public record CustodyTransferResult(bool Changed, string PrivateKeyName, string PublicKeyValue);

/// <remarks>Reference source: dotenvx/src/lib/services/custodyTransfer.js (up/down/push/pull between `.env.keys` and a local custodian).</remarks>
public static class CustodyTransfer
{
    private static void Verify(string publicKey, string privateKey)
    {
        if (!global::Dotenvx.Providers.MatchesStoredKey.Matches(publicKey, privateKey))
        {
            throw new Exception("private key does not match the .env public key");
        }
    }

    public static CustodyTransferResult Run(ILocalCustodian provider, string name, string operation,
        string envFile = ".env", string envKeysFile = ".env.keys")
    {
        if (operation is not ("up" or "down" or "push" or "pull"))
        {
            throw new Exception("unknown custody operation");
        }

        (string publicKeyName, string privateKeyName) = Keynames(envFile);
        string publicKey = ReadEnvKey(publicKeyName, envFile, true)!;
        CustodyTransferResult Result(bool changed)
        {
            return new CustodyTransferResult(changed, privateKeyName, publicKey);
        }

        string? local = ReadEnvKey(privateKeyName, envKeysFile);
        if (operation is "up" or "push")
        {
            if (!string.IsNullOrEmpty(local))
            {
                Verify(publicKey, local);
            }

            if (operation == "push" && string.IsNullOrEmpty(local))
            {
                throw new Exception($"missing {privateKeyName} in {envKeysFile}");
            }

            string? existing = provider.Get(publicKey).Lookup(publicKey);
            if (!string.IsNullOrEmpty(existing))
            {
                Verify(publicKey, existing);
            }

            if (string.IsNullOrEmpty(local) && string.IsNullOrEmpty(existing))
            {
                throw new Exception($"missing {privateKeyName} in {envKeysFile}");
            }

            if (string.IsNullOrEmpty(existing))
            {
                provider.Set(publicKey, local!);
            }

            string? saved = provider.Get(publicKey).Lookup(publicKey);
            if (string.IsNullOrEmpty(saved) || saved != (string.IsNullOrEmpty(local) ? existing : local))
            {
                throw new Exception($"could not verify private key in {name}; {envKeysFile} unchanged");
            }

            return operation == "up"
                ? Result(RemoveEnvKey(privateKeyName, envKeysFile).Changed || string.IsNullOrEmpty(existing))
                : Result(string.IsNullOrEmpty(existing));
        }

        string? privateKey = provider.Get(publicKey).Lookup(publicKey);
        if (string.IsNullOrEmpty(privateKey))
        {
            if (operation == "down" && !string.IsNullOrEmpty(local))
            {
                Verify(publicKey, local);

                return Result(false);
            }

            throw new Exception($"[NOT_FOUND] private key not found in {name}");
        }

        Verify(publicKey, privateKey);
        UpsertEnvKeyResult written = UpsertEnvKey(privateKeyName, privateKey, envKeysFile);
        if (ReadEnvKey(privateKeyName, envKeysFile, true) != privateKey)
        {
            throw new Exception($"could not verify private key in {envKeysFile}; {name} unchanged");
        }

        if (operation == "down")
        {
            provider.Delete(publicKey);
        }

        return Result(written.Changed || operation == "down");
    }
}
