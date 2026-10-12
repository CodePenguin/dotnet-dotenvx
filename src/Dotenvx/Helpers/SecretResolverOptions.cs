using System;
using System.CommandLine;

using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Shared;

namespace Dotenvx.Helpers;

/// <remarks>
/// dotenvx.js registers `--no-1password` and `--no-bitwarden` on run, get and validate; the actions read
/// `options['1password'] === false || options.no1Password === true`, which for a plain negation flag is just
/// "was it given".
/// </remarks>
public static class SecretResolverOptions
{
    public static Option<bool> NoArmor()
    {
        return new Option<bool>("--no-armor") { Description = "disable Dotenvx Armor features" };
    }

    public static Option<string?> Token()
    {
        return new Option<string?>("--token") { Description = "set Armor ⛨ token", HelpName = "token" };
    }

    /// <summary>How an action decides `noArmor` (run.js, get.js, ...): see <see cref="KeyAccess" />.</summary>
    public enum ArmorMode
    {
        /// <summary>`options.armor === false || (!options.token && sesh.noArmor())` - run, validate, encrypt, set.</summary>
        SessionUnlessToken,

        /// <summary>`options.armor === false || sesh.noArmor()` - get, decrypt (no --token option).</summary>
        Session,

        /// <summary>`armor: options.armor` only; the resolver asks the session itself - keypair.</summary>
        FlagOnly
    }

    public static KeyAccessOptions KeyAccess(IDotenvxLogger logger, bool noNative, bool noArmorFlag, string? token,
        ArmorMode mode, Action<string>? onStatus = null)
    {
        bool noArmor = noArmorFlag;
        if (!noArmor && mode != ArmorMode.FlagOnly &&
            (mode == ArmorMode.Session || string.IsNullOrEmpty(token)))
        {
            noArmor = new Session(logger).NoArmor();
        }

        return new KeyAccessOptions
        {
            NoNative = noNative, NoArmor = noArmor, Token = token, Logger = logger, OnStatus = onStatus
        };
    }

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
