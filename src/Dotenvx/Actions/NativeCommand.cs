using System;
using System.CommandLine;

using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/commands/native.js and cli/actions/keychain/{up,down,push,pull}.js (hidden in the
/// root help, advertised by the "Better Security" section). Upstream's `preAction` hook also runs the Armor session's
/// update-version check (Phase E, network) and each action starts a spinner on a TTY (Phase F).
/// </remarks>
public class NativeCommand : Command
{
    public NativeCommand(IDotenvxLogger logger) : base("native", "move private keys into your OS secret store")
    {
        Subcommands.Add(new Step("up", "store key in OS secret store", logger,
            (f, k) => KeychainUp.Factory(f, k).Run(),
            (result, display) => (true, $"⌥ stored ({display})")));
        Subcommands.Add(new Step("down", "move key from OS secret store to .env.keys", logger,
            (f, k) => KeychainDown.Factory(f, k).Run(),
            (result, display) => (true, $"◇ moved to .env.keys ({display})")));
        Subcommands.Add(new Step("push", "push key to OS secret store from .env.keys", logger,
            (f, k) => KeychainPush.Factory(f, k).Run(),
            (result, display) => (true, $"⌥ pushed ({display})")));
        Subcommands.Add(new Step("pull", "pull key from OS secret store into .env.keys", logger,
            (f, k) => KeychainPull.Factory(f, k).Run(),
            (result, display) => (true, $"◇ pulled to .env.keys ({display})")));

        SetAction(parseResult =>
        {
            // native.js: `this.help()` prints the native help and exits 0
            parseResult.RootCommandResult.Command.Parse(["native", "--help"]).Invoke();
            return 0;
        });
    }

    private sealed class Step : Command
    {
        private readonly Option<string?> _envFileOption = LockCommand.EnvFileOption();
        private readonly Option<string> _envKeysFileOption = LockCommand.EnvKeysFileOption();

        public Step(string name, string description, IDotenvxLogger logger,
            Func<string?, string?, KeychainResult> run,
            Func<KeychainResult, string, (bool Success, string Message)> successMessage) : base(name, description)
        {
            Add(_envFileOption);
            Add(_envKeysFileOption);
            SetAction(parseResult => Execute(parseResult, logger, run, successMessage));
        }

        private static string SpinnerText(string name)
        {
            return name switch { "up" => "storing", "down" => "unkeychaining", "push" => "pushing", _ => "pulling" };
        }

        private int Execute(ParseResult parseResult, IDotenvxLogger logger,
            Func<string?, string?, KeychainResult> run,
            Func<KeychainResult, string, (bool Success, string Message)> successMessage)
        {
            using SpinnerScope spinner = SpinnerScope.Start(parseResult, SpinnerText(parseResult.CommandResult.Command.Name));
            string? envFile = parseResult.GetValue(_envFileOption);
            string envKeysFile = parseResult.GetValue(_envKeysFileOption) ?? ".env.keys";
            logger.Debug($"options: {LockCommand.OptionsJson(envFile, envKeysFile)}");

            try
            {
                KeychainResult result = run(envFile, envKeysFile);
                string display = ArmoredKeyDisplay.Display(result.PublicKeyValue);
                if (display == "")
                {
                    display = result.PrivateKeyName;
                }

                if (result.Changed)
                {
                    logger.Success(successMessage(result, display).Message);
                }
                else
                {
                    logger.Info($"○ no change ({display})");
                }

                return 0;
            }
            catch (Exception error)
            {
                logger.Error(error.Message);
                return 1;
            }
        }
    }
}
