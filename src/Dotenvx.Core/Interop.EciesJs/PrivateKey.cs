using NBitcoin.Secp256k1;

namespace Dotenvx.Core.Interop.EciesJs;

/// <summary>
///     C# equivalent for eciesjs src/keys/PrivateKey.ts (https://github.com/ecies/js).
/// </summary>
public class PrivateKey
{
    private readonly Context _context = Context.Instance;
    private readonly ECPrivKey _privateKey;

    public PrivateKey()
    {
        byte[] privateKey = new byte[32];
        new Random().NextBytes(privateKey);
        _privateKey = LoadFromBytes(privateKey);
        PublicKey = new PublicKey(Utils.GetPublicKey(privateKey));
    }

    public PrivateKey(byte[] privateKey)
    {
        _privateKey = LoadFromBytes(privateKey);
        PublicKey = new PublicKey(_privateKey.CreatePubKey().ToBytes());
    }

    public PublicKey PublicKey { get; }

    public byte[] Encapsulate(byte[] publicKey, bool compressed = false)
    {
        if (!ECPubKey.TryCreate(publicKey, _context, out _, out ECPubKey? ecPublicKey))
        {
            throw new ArgumentException("Invalid public key", nameof(publicKey));
        }

        byte[] senderPoint = PublicKey.ToBytes(compressed);
        ECPubKey sharedPoint = ecPublicKey.GetSharedPubkey(_privateKey);

        return Utils.GetSharedKey(senderPoint, sharedPoint.ToBytes(compressed));
    }

    public static PrivateKey FromHex(string hex)
    {
        return new PrivateKey(Utils.DecodeHex(hex));
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

    public string ToHex()
    {
        return Convert.ToHexString(_privateKey.sec.ToBytes()).ToLower();
    }
}