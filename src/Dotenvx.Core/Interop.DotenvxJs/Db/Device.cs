using Dotenvx.Interop.DotenvxJs.Helpers;

using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Db;

/// <remarks>
/// Reference source: dotenvx/src/db/device.js. The device keypair lives in `.device-key` in the config directory
/// (`DOTENVX_CONFIG` or the OS default), encrypted with the same fixed key as upstream so the Node and .NET CLIs share
/// one device.
/// </remarks>
public class Device
{
    private const string PrivateKeySlot = "private_key/1";

    private readonly ConfStore _store;

    /// <param name="cwd">The config directory; defaults to `DOTENVX_CONFIG` or the OS default, like upstream.</param>
    public Device(string? cwd = null)
    {
        _store = new ConfStore(cwd ?? Environment.GetEnvironmentVariable("DOTENVX_CONFIG"), ".device-key",
            "dotenvxpro dotenvxpro dotenvxpro"); // backwards compatible
    }

    public string ConfigPath()
    {
        return _store.Path;
    }

    public string PrivateKey()
    {
        string? current = _store.GetString(PrivateKeySlot);
        if (!string.IsNullOrEmpty(current))
        {
            _store.Set(PrivateKeySlot, current);

            return current;
        }

        string privateKey = Keypair().PrivateKey;
        _store.Set(PrivateKeySlot, privateKey);

        return privateKey;
    }

    public string PublicKey()
    {
        // must have private key to try and get public key
        string privateKeyHex = PrivateKey();

        return string.IsNullOrEmpty(privateKeyHex) ? "" : Derive(privateKeyHex);
    }

    public string Encrypt(string value)
    {
        return Primitives.Primitives.Encrypt(value, PublicKey(), false);
    }

    public string Decrypt(string value)
    {
        return Primitives.Primitives.Decrypt(PrivateKey(), value, false);
    }
}
