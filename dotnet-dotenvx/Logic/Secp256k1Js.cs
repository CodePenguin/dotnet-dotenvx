using System;
using System.Security.Cryptography;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using ECPoint = Org.BouncyCastle.Math.EC.ECPoint;

namespace dotnet_dotenvx.Logic;

public static class Secp256k1Js
{
    public static readonly X9ECParameters Ecp = SecNamedCurves.GetByName("secp256k1");
    public static readonly ECDomainParameters DomainParameters = new(Ecp.Curve, Ecp.G, Ecp.N, Ecp.H, Ecp.GetSeed());
    
    /// <summary>
    /// C# equivalent for secp256k1 v3.6.0 lib/elliptic/index.js (https://github.com/cryptocoinjs/secp256k1-node).
    /// </summary>
/*
    public static byte[] Ecdh(byte[] publicKeyBytes, byte[] privateKeyBytes)
    {
        ArgumentNullException.ThrowIfNull(publicKeyBytes);
        ArgumentNullException.ThrowIfNull(privateKeyBytes);
        if (privateKeyBytes.Length != 32) throw new ArgumentException("Private key must be 32 bytes.", nameof(privateKeyBytes));

        // Decode public key (handles compressed/uncompressed)
        var q = Ecp.Curve.DecodePoint(publicKeyBytes);
        if (q.IsInfinity) throw new ArgumentException("Invalid Public Key", nameof(publicKeyBytes));

        // Subgroup check: n * Q must be infinity
        var check = q.Multiply(DomainParameters.N);
        if (!check.IsInfinity) throw new ArgumentException("Invalid Public Key", nameof(publicKeyBytes));
        
        // Private Key out of Range
        var d = new BigInteger(1, privateKeyBytes);
        if (d.CompareTo(BigInteger.One) < 0 || d.CompareTo(DomainParameters.N.Subtract(BigInteger.One)) > 0)
        {
            throw new ArgumentException("Invalid Private Key", nameof(privateKeyBytes));
        }

        // Shared point S = d * Q
        var sharedPoint = q.Multiply(d).Normalize();
        if (sharedPoint.IsInfinity) throw new InvalidOperationException("Invalid ECDH operation.");
        
        // Get x-coordinate and pad to 32 bytes
        byte[] xBytes = sharedPoint.AffineXCoord.ToBigInteger().ToByteArrayUnsigned();
        if (xBytes.Length > 32) throw new InvalidOperationException("Unexpected x-coordinate length > 32.");

        byte[] padded = new byte[32];
        Buffer.BlockCopy(xBytes, 0, padded, 32 - xBytes.Length, xBytes.Length);
        return SHA256.HashData(xBytes); // 32 bytes
    }
*/
/*
    public static byte[] Ecdh(byte[] publicKey, byte[] privateKey)
    {
        if (privateKey.Length != 32) throw new ArgumentException("Private key must be 32 bytes.", nameof(privateKey));

        var curveParams = SecNamedCurves.GetByName("secp256k1");
        var curve = curveParams.Curve;

        // Decode public key
        var pubPoint = curve.DecodePoint(publicKey);
        var privateKeyValue = new BigInteger(1, privateKey);

        // Multiply: private * public
        var sharedPoint = pubPoint.Multiply(privateKeyValue).Normalize();

        // Get X coordinate as unsigned big-endian bytes
        var xBytes = sharedPoint.AffineXCoord.ToBigInteger().ToByteArrayUnsigned();

        // Always pad to 32 bytes
        var padded = new byte[32];
        Buffer.BlockCopy(xBytes, 0, padded, 32 - xBytes.Length, xBytes.Length);
        
        // Hash with SHA-256
        return SHA256.HashData(padded);
    }
*/
    public static byte[] Ecdh(byte[] publicKey, byte[] privateKey)
    {
        var curve = SecNamedCurves.GetByName("secp256k1");
        var ecParams = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);

        // Decode public key (handles compressed/uncompressed)
        var q = curve.Curve.DecodePoint(publicKey);

        // Private key as positive BigInteger
        var d = new BigInteger(1, privateKey);
        var privParams = new ECPrivateKeyParameters(d, ecParams);

        // Shared point
        var sharedPoint = q.Multiply(d).Normalize();

        // Get X coordinate
        var xBytes = sharedPoint.AffineXCoord.GetEncoded();

        // Ensure 32 bytes (pad if shorter)
        if (xBytes.Length < 32)
        {
            var padded = new byte[32];
            Buffer.BlockCopy(xBytes, 0, padded, 32 - xBytes.Length, xBytes.Length);
            xBytes = padded;
        }

        // Apply SHA-256 like Node.js default
        return SHA256.HashData(xBytes);
    }
    
    public static byte[] GetPublicKey(byte[] privateKey, bool compressed = true)
    {
        var d = new BigInteger(1, privateKey);
        var q = Ecp.G.Multiply(d).Normalize();
        var pub = new ECPublicKeyParameters("EC", q, SecObjectIdentifiers.SecP256k1);
        return pub.Q.GetEncoded(compressed);
    }
}