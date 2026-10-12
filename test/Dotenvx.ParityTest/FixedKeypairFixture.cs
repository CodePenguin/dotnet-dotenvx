namespace Dotenvx.ParityTest;

/// <remarks>
/// A fixed, pre-verified ECIES keypair plus a matching pre-encrypted .env, generated once via our
/// own .NET encrypt and confirmed decryptable by both CLIs. Shared by every cross-implementation
/// test that needs a deterministic starting keypair to build on (see EncryptDecryptParityTest's
/// class remarks for why fresh key GENERATION itself can't be used as that starting point).
/// </remarks>
public static class FixedKeypairFixture
{
    public const string PrivateKey = "1fc1cafa954a7a2bf0a6fbff46189c9e03e3a66b4d1133108ab9fcdb9e154b70";
    public const string PublicKey = "02b106c30579baf896ae1fddf077cbcb4fef5e7d457932974878dcb51f42b45498";

    public const string EncryptedEnv =
        "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/\n" +
        "#/            public-key encryption for .env files          /\n" +
        "#/       [how it works](https://dotenvx.com/encryption)     /\n" +
        "#/----------------------------------------------------------/\n" +
        $"DOTENV_PUBLIC_KEY=\"{PublicKey}\"\n\n" +
        "# .env\n" +
        "HELLO=encrypted:BGftYDEUfycZeomeqB4m8HxtfNH3Pe8TxCNAodAJ2Q7t1x8J4iFFY0TjDu1MNIzJKl3DLDT33oDTCVWOBZe1ZvC/RWZy5F92dj6qPPvhA9e7kuifQwrinrEirS9XJINSqHOyyeIP\n";

    public const string PrivateKeyFile = $"DOTENV_PRIVATE_KEY={PrivateKey}\n";
}
