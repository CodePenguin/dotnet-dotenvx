using System;
using System.Collections.Generic;
using System.IO;
using System.CommandLine;
using System.Linq;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/commands/lock.js (hidden from the root help by DotenvxRootCommand's FilteringHelpAction, advertised by the "Better Security"
/// section). Upstream's `preAction` hook also runs the Armor session's update-version check (Phase E, network).
/// </remarks>
public class LockCommand : Command
{
    public LockCommand(IDotenvxLogger logger) : base("lock", "lock private keys with a local passphrase")
    {
        Subcommands.Add(new LockUpAction(logger));
        Subcommands.Add(new LockDownAction(logger));
        SetAction(parseResult =>
        {
            // lock.js: `this.help()` prints the lock help and exits 0
            parseResult.RootCommandResult.Command.Parse(["lock", "--help"]).Invoke();
            return 0;
        });
    }

    internal static Option<string?> EnvFileOption()
    {
        return new("--env-file", "-f") { Description = "path to your env file", HelpName = "path" };
    }

    internal static Option<string> EnvKeysFileOption()
    {
        return new("--env-keys-file", "-fk")
        {
            Description = "path to your .env.keys file", HelpName = "path", DefaultValueFactory = _ => ".env.keys"
        };
    }

    internal static string OptionsJson(string? envFile, string envKeysFile)
    {
        var options = new Dictionary<string, string>();
        if (envFile is not null)
        {
            options["envFile"] = envFile;
        }

        options["envKeysFile"] = envKeysFile;

        return JsonStringify(options);
    }

    internal static int Run(
        IDotenvxLogger logger, ParseResult parseResult, Option<string?> envFileOption, Option<string> envKeysFileOption,
        Func<string?, string, int> body)
    {
        string? envFile = parseResult.GetValue(envFileOption);
        string envKeysFile = parseResult.GetValue(envKeysFileOption) ?? ".env.keys";
        logger.Debug($"options: {OptionsJson(envFile, envKeysFile)}");

        try
        {
            return body(envFile, envKeysFile);
        }
        catch (PromptCancelledException)
        {
            return 130;
        }
        catch (FileNotFoundException error)
        {
            // fs.readFileSync's raw ENOENT text (the catch block prints error.message, not messageWithHelp)
            logger.Error($"ENOENT: no such file or directory, open '{Path.GetFullPath(error.FileName ?? "")}'");
            return 1;
        }
        catch (Exception error)
        {
            logger.Error(error.Message);
            return 1;
        }
    }

    private static string KeyDisplay(LockResult result)
    {
        string display = ArmoredKeyDisplay.Display(result.PublicKeyValue);

        return display != "" ? display : result.PrivateKeyName;
    }

    private sealed class LockUpAction : Command
    {
        private readonly Option<string?> _envFileOption = EnvFileOption();
        private readonly Option<string> _envKeysFileOption = EnvKeysFileOption();

        public LockUpAction(IDotenvxLogger logger) : base("up", "lock key in .env.keys")
        {
            Add(_envFileOption);
            Add(_envKeysFileOption);
            SetAction(parseResult => Run(logger, parseResult, _envFileOption, _envKeysFileOption, (envFile, keys) =>
            {
                LockUp lockUp = LockUp.Factory(envFile, keys);
                LockUpPlan plan = lockUp.Plan();
                LockResult[] results =
                [
                    .. plan.AlreadyLocked.Select(locked => new LockResult(false, locked.PrivateKeyName, null,
                        locked.LockedPrivateKeyValue, locked.PublicKeyValue, true))
                ];

                if (plan.Matches.Length > 0)
                {
                    string? passphrase = Prompts.Password("passphrase", "⊡");
                    if (passphrase is null)
                    {
                        // stdin ended before Enter: Node's pending prompt lets the process exit 0 silently
                        return 0;
                    }

                    results = lockUp.Run((privateKey, publicKey) =>
                        LockCrypto.LockedValue(privateKey, passphrase, publicKey)).Results;
                }

                foreach (LockResult result in results)
                {
                    if (result.Changed)
                    {
                        logger.Success($"⊡ locked ({KeyDisplay(result)})");
                    }
                    else
                    {
                        logger.Info($"○ no change ({KeyDisplay(result)})");
                    }
                }

                return 0;
            }));
        }
    }

    private sealed class LockDownAction : Command
    {
        private readonly Option<string?> _envFileOption = EnvFileOption();
        private readonly Option<string> _envKeysFileOption = EnvKeysFileOption();

        public LockDownAction(IDotenvxLogger logger) : base("down", "unlock key in .env.keys")
        {
            Add(_envFileOption);
            Add(_envKeysFileOption);
            SetAction(parseResult => Run(logger, parseResult, _envFileOption, _envKeysFileOption, (envFile, keys) =>
            {
                LockDown lockDown = LockDown.Factory(envFile, keys);
                LockDownPlan plan = lockDown.Plan();
                LockResult[] results = plan.AlreadyUnlocked;

                if (plan.Locked.Length > 0)
                {
                    string? passphrase = Prompts.Password("passphrase", "⊡");
                    if (passphrase is null)
                    {
                        return 0;
                    }

                    results = lockDown.Run(locked => LockCrypto.UnlockedValue(locked, passphrase)).Results;
                }

                foreach (LockResult result in results)
                {
                    if (result.Changed)
                    {
                        logger.Success($"⊡ unlocked ({KeyDisplay(result)})");
                    }
                    else
                    {
                        logger.Info($"○ no change ({KeyDisplay(result)})");
                    }
                }

                return 0;
            }));
        }
    }
}
