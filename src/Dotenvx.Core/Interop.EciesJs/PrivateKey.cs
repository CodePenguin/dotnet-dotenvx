using NBitcoin.Secp256k1;

namespace Dotenvx.Core.Interop.EciesJs;

public class PrivateKey
{
    private readonly Context _context = Context.Instance;
    private readonly ECPrivKey _privateKey;

    public PrivateKey()
    {
        byte[] privateKey = new byte[32];
        new Random().NextBytes(privateKey);
        _privateKey = LoadFromBytes(privateKey);
    }

    public PrivateKey(byte[] privateKey)
    {
        _privateKey = LoadFromBytes(privateKey);
    }

    /// <summary>
    ///     C# equivalent for encapsulate function from https://github.com/ecies/js/blob/master/src/keys/PrivateKey.ts
    /// </summary>
    public byte[] Encapsulate(byte[] publicKey, bool compressed = false)
    {
        if (!ECPubKey.TryCreate(publicKey, _context, out _, out ECPubKey? ecPublicKey))
        {
            throw new ArgumentException("Invalid public key", nameof(publicKey));
        }

        byte[] senderPoint = GetPublicKey(compressed);
        ECPubKey sharedPoint = ecPublicKey.GetSharedPubkey(_privateKey);

        return Utils.GetSharedKey(senderPoint, sharedPoint.ToBytes(compressed));
    }

    public static PrivateKey FromHex(string hex)
    {
        return new PrivateKey(Utils.DecodeHex(hex));
    }

    public byte[] GetPublicKey(bool compressed = false)
    {
        ECPubKey publicKey = _privateKey.CreatePubKey();
        return publicKey.ToBytes(compressed);
    }

    private ECPrivKey LoadFromBytes(byte[] privateKey)
    {
        if (privateKey.Length != 32)
        {
            throw new ArgumentException("Private key must be 32 bytes", nameof(privateKey));
        }

        return _context.TryCreateECPrivKey(privateKey, out ECPrivKey? ecPrivateKey)
            ? ecPrivateKey
            : throw new ArgumentException("Invalid private key", nameof(privateKey));
    }
}