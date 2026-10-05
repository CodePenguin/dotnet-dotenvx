using System.CommandLine;

namespace Dotenvx.Helpers;

/// <remarks>
/// dotenvx.js registers `--no-1password` and `--no-bitwarden` on run, get and validate; the actions read
/// `options['1password'] === false || options.no1Password === true`, which for a plain negation flag is just
/// "was it given".
/// </remarks>
public static class SecretResolverOptions
{
    public static Option<bool> NoNative()
    {
        return new Option<bool>("--no-native") { Description = "disable OS secret store features" };
    }

    public static Option<bool> No1Password()
    {
        return new Option<bool>("--no-1password") { Description = "disable 1Password secret reference resolution" };
    }

    public static Option<bool> NoBitwarden()
    {
        return new Option<bool>("--no-bitwarden") { Description = "disable Bitwarden secret reference resolution" };
    }
}
