using NBitcoin.Secp256k1;

namespace dotenvx.EciesJs.Interop;

public class PublicKey
{
    private readonly Context _context = Context.Instance;
    private readonly ECPubKey _publicKey;

    public PublicKey(byte[] publicKey)
    {
        if (!ECPubKey.TryCreate(publicKey, _context, out _, out ECPubKey? ecPublicKey))
        {
            throw new ArgumentException("Invalid public key", nameof(publicKey));
        }

        _publicKey = ecPublicKey;
    }

    /// <summary>
    ///     C# equivalent for decapsulate function from https://github.com/ecies/js/blob/master/src/keys/PublicKey.ts
    /// </summary>
    public byte[] Decapsulate(byte[] privateKey, bool compressed = false)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        if (privateKey.Length != 32)
        {
            throw new ArgumentException("Private key must be 32 bytes", nameof(privateKey));
        }

        if (!_context.TryCreateECPrivKey(privateKey, out ECPrivKey? ecPrivateKey))
        {
            throw new ArgumentException("Invalid private key", nameof(privateKey));
        }

        // Get Shared Public Key
        ECPubKey sharedPoint = _publicKey.GetSharedPubkey(ecPrivateKey);

        return Utils.GetSharedKey(_publicKey.ToBytes(compressed), sharedPoint.ToBytes(compressed));
    }

    public static PublicKey FromHex(string hex)
    {
        return new PublicKey(Utils.DecodeHex(hex));
    }
}