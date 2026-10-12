using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/cli/actions/lock/up.js (`lockedValue`) and down.js (`unlockedValue`):
/// `locked:&lt;publicKey&gt;:base64url(0x01 | salt16 | iv12 | tag16 | ciphertext)` with an AES-256-GCM key from
/// `scryptSync(passphrase, salt, 32)` (Node defaults N=16384, r=8, p=1). .NET has no scrypt, so it is implemented here
/// (RFC 7914) on top of PBKDF2-SHA256.
/// </remarks>
public static class LockCrypto
{
    public static string LockedValue(string privateKey, string passphrase, string publicKey)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] iv = RandomNumberGenerator.GetBytes(12);
        byte[] key = Scrypt(Encoding.UTF8.GetBytes(passphrase), salt, 16384, 8, 1, 32);
        byte[] plaintext = Encoding.UTF8.GetBytes(privateKey);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[16];

        using var aes = new AesGcm(key, 16);
        aes.Encrypt(iv, plaintext, ciphertext, tag);

        byte[] payload = [1, .. salt, .. iv, .. tag, .. ciphertext];

        return $"locked:{publicKey}:{Base64Url(payload)}";
    }

    /// <summary>Throws on a wrong passphrase, an unsupported version or malformed data (callers map it to INVALID_PASSPHRASE).</summary>
    public static string UnlockedValue(string lockedPrivateKey, string passphrase)
    {
        string[] parts = lockedPrivateKey.Split(':');
        byte[] payload = FromBase64Url(string.Join(":", parts.Skip(2)));
        byte version = payload.Length > 0 ? payload[0] : (byte)0;
        byte[] salt = Slice(payload, 1, 17);
        byte[] iv = Slice(payload, 17, 29);
        byte[] tag = Slice(payload, 29, 45);
        byte[] ciphertext = Slice(payload, 45, payload.Length);
        byte[] key = Scrypt(Encoding.UTF8.GetBytes(passphrase), salt, 16384, 8, 1, 32);

        if (version != 1)
        {
            throw new InvalidOperationException("unsupported locked private key version");
        }

        byte[] plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(iv, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    private static byte[] Slice(byte[] source, int start, int end)
    {
        start = Math.Min(start, source.Length);
        end = Math.Min(end, source.Length);

        return source[start..end];
    }

    internal static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    internal static byte[] FromBase64Url(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');

        return Convert.FromBase64String(padded);
    }

    internal static byte[] Scrypt(byte[] password, byte[] salt, int n, int r, int p, int dkLen)
    {
        int blockSize = 128 * r;
        byte[] b = Rfc2898DeriveBytes.Pbkdf2(password, salt, 1, HashAlgorithmName.SHA256, p * blockSize);
        uint[] scratch = new uint[32 * r];
        uint[] v = new uint[32 * r * n];

        for (int i = 0; i < p; i++)
        {
            RoMix(b, i * blockSize, r, n, v, scratch);
        }

        return Rfc2898DeriveBytes.Pbkdf2(password, b, 1, HashAlgorithmName.SHA256, dkLen);
    }

    private static void RoMix(byte[] b, int offset, int r, int n, uint[] v, uint[] scratch)
    {
        int words = 32 * r;
        uint[] x = new uint[words];
        for (int i = 0; i < words; i++)
        {
            x[i] = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(offset + i * 4));
        }

        for (int i = 0; i < n; i++)
        {
            Array.Copy(x, 0, v, i * words, words);
            BlockMix(x, r, scratch);
        }

        for (int i = 0; i < n; i++)
        {
            int j = (int)(x[(2 * r - 1) * 16] & (uint)(n - 1));
            for (int k = 0; k < words; k++)
            {
                x[k] ^= v[j * words + k];
            }

            BlockMix(x, r, scratch);
        }

        for (int i = 0; i < words; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(offset + i * 4), x[i]);
        }
    }

    private static void BlockMix(uint[] b, int r, uint[] y)
    {
        uint[] x = new uint[16];
        Array.Copy(b, (2 * r - 1) * 16, x, 0, 16);

        for (int i = 0; i < 2 * r; i++)
        {
            for (int k = 0; k < 16; k++)
            {
                x[k] ^= b[i * 16 + k];
            }

            Salsa208(x);
            Array.Copy(x, 0, y, i * 16, 16);
        }

        for (int i = 0; i < r; i++)
        {
            Array.Copy(y, 2 * i * 16, b, i * 16, 16);
            Array.Copy(y, (2 * i + 1) * 16, b, (i + r) * 16, 16);
        }
    }

    private static void Salsa208(uint[] b)
    {
        uint[] x = (uint[])b.Clone();

        for (int i = 0; i < 8; i += 2)
        {
            x[4] ^= Rotl(x[0] + x[12], 7); x[8] ^= Rotl(x[4] + x[0], 9);
            x[12] ^= Rotl(x[8] + x[4], 13); x[0] ^= Rotl(x[12] + x[8], 18);
            x[9] ^= Rotl(x[5] + x[1], 7); x[13] ^= Rotl(x[9] + x[5], 9);
            x[1] ^= Rotl(x[13] + x[9], 13); x[5] ^= Rotl(x[1] + x[13], 18);
            x[14] ^= Rotl(x[10] + x[6], 7); x[2] ^= Rotl(x[14] + x[10], 9);
            x[6] ^= Rotl(x[2] + x[14], 13); x[10] ^= Rotl(x[6] + x[2], 18);
            x[3] ^= Rotl(x[15] + x[11], 7); x[7] ^= Rotl(x[3] + x[15], 9);
            x[11] ^= Rotl(x[7] + x[3], 13); x[15] ^= Rotl(x[11] + x[7], 18);
            x[1] ^= Rotl(x[0] + x[3], 7); x[2] ^= Rotl(x[1] + x[0], 9);
            x[3] ^= Rotl(x[2] + x[1], 13); x[0] ^= Rotl(x[3] + x[2], 18);
            x[6] ^= Rotl(x[5] + x[4], 7); x[7] ^= Rotl(x[6] + x[5], 9);
            x[4] ^= Rotl(x[7] + x[6], 13); x[5] ^= Rotl(x[4] + x[7], 18);
            x[11] ^= Rotl(x[10] + x[9], 7); x[8] ^= Rotl(x[11] + x[10], 9);
            x[9] ^= Rotl(x[8] + x[11], 13); x[10] ^= Rotl(x[9] + x[8], 18);
            x[12] ^= Rotl(x[15] + x[14], 7); x[13] ^= Rotl(x[12] + x[15], 9);
            x[14] ^= Rotl(x[13] + x[12], 13); x[15] ^= Rotl(x[14] + x[13], 18);
        }

        for (int i = 0; i < 16; i++)
        {
            b[i] += x[i];
        }
    }

    private static uint Rotl(uint value, int bits)
    {
        return (value << bits) | (value >> (32 - bits));
    }
}
