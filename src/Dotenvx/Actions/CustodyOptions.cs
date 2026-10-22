using System;
using System.Collections.Generic;
using System.CommandLine;

using Dotenvx.Interop.DotenvxJs.Helpers;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

namespace Dotenvx.Actions;

/// <remarks>The shared `-f, --env-file <path>` / `-fk, --env-keys-file <path>` options of the native/1password/bitwarden steps.</remarks>
internal static class CustodyOptions
{
    internal static Option<string?> EnvFileOption()
    {
        return new("--file", "-f", "--env-file") { Description = "path to your env file", HelpName = "path" };
    }

    internal static Option<string> EnvKeysFileOption()
    {
        return new("--env-keys-file", "-fk")
        {
            Description = "path to your .env.keys file",
            HelpName = "path",
            DefaultValueFactory = _ => ".env.keys"
        };
    }

}
