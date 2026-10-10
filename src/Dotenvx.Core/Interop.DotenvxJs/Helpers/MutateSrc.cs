namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/cryptography/mutateSrc.js (with preserveShebang.js and
/// prependPublicKey.js): prepends the public key header to an env source, keeping a leading shebang line
/// first. The header's `# -fk` comment shows the keys file path relative to the env file's directory,
/// and is omitted when that path is exactly `.env.keys`.
/// </remarks>
public static partial class Helpers
{
    public static string MutateSrc(string envSrc, string envFilePath, string keysFilePath, string publicKeyName,
        string publicKeyValue)
    {
        string filename = NodePath.Basename(envFilePath);
        string filepath = NodePath.Resolve(envFilePath);
        string resolvedKeysFilePath = string.IsNullOrEmpty(keysFilePath)
            ? Path.Combine(Path.GetDirectoryName(filepath) ?? "", ".env.keys")
            : NodePath.Resolve(keysFilePath);
        string relativeFilePath = Path.GetRelativePath(Path.GetDirectoryName(filepath) ?? "", resolvedKeysFilePath);

        (string firstLinePreserved, string rest) = PreserveShebang(envSrc);
        string prepended = PrependPublicKey(publicKeyName, publicKeyValue, filename, relativeFilePath);

        return $"{firstLinePreserved}{prepended}\n{rest}";
    }

    public static (string FirstLinePreserved, string EnvSrc) PreserveShebang(string envSrc)
    {
        string[] lines = envSrc.Split('\n');
        if (!lines[0].StartsWith("#!"))
        {
            return ("", envSrc);
        }

        return (lines[0] + "\n", string.Join('\n', lines.Skip(1)));
    }

    public static string PrependPublicKey(string publicKeyName, string publicKey, string filename,
        string relativeFilePath = ".env.keys")
    {
        string comment = relativeFilePath == ".env.keys" ? "" : $" # -fk {relativeFilePath}";

        return string.Join("\n",
            "#/-------------------[DOTENV_PUBLIC_KEY]--------------------/",
            "#/            public-key encryption for .env files          /",
            "#/       [how it works](https://dotenvx.com/encryption)     /",
            "#/----------------------------------------------------------/",
            $"{publicKeyName}=\"{publicKey}\"{comment}", "", $"# {filename}");
    }
}
