using System;
using System.Collections.Generic;
using System.CommandLine;

using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Custodians;
using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>Reference source: dotenvx/src/cli/commands/custody.js (the hidden `1password` and `bitwarden` groups).</remarks>
public class CustodyCommand : Command
{
    protected CustodyCommand(IDotenvxLogger logger, string commandName, string name, string custodianId)
        : base(commandName, $"move private keys in/out of {name}")
    {
        (string Op, string Description)[] operations =
        [
            ("up", $"move key from .env.keys into {name}"), ("down", $"move key from {name} to .env.keys"),
            ("push", $"copy key from .env.keys into {name}"), ("pull", $"copy key from {name} into .env.keys")
        ];
        foreach ((string op, string description) in operations)
        {
            Subcommands.Add(new Step(op, description, logger, name, custodianId));
        }

        SetAction(parseResult =>
        {
            new Session(logger).NotifyUpdate();
            parseResult.RootCommandResult.Command.Parse([commandName, "--help"]).Invoke();

            return 0;
        });
    }

    private sealed class Step : Command
    {
        private readonly Option<string?> _envFileOption = CustodyOptions.EnvFileOption();
        private readonly Option<string> _envKeysFileOption = CustodyOptions.EnvKeysFileOption();

        public Step(string op, string description, IDotenvxLogger logger, string name, string custodianId)
            : base(op, description)
        {
            Add(_envFileOption);
            Add(_envKeysFileOption);
            SetAction(parseResult =>
            {
                // custody.js preAction hook
                new Session(logger).NotifyUpdate();

                return Execute(parseResult, logger, op, name, custodianId);
            });
        }

        private int Execute(ParseResult parseResult, IDotenvxLogger logger, string op, string name, string custodianId)
        {
            string? envFile = parseResult.GetValue(_envFileOption);
            string envKeysFile = parseResult.GetValue(_envKeysFileOption) ?? ".env.keys";
            using SpinnerScope spinner = SpinnerScope.Start(parseResult,
                $"{(op is "up" or "push" ? "storing in" : "reading from")} {name}");

            try
            {
                CustodyTransferResult result = CustodyTransfer.Run((ILocalCustodian)Custodians.Get(custodianId), name, op,
                    envFile ?? ".env", envKeysFile);
                spinner.Dispose();
                string display = ArmoredKeyDisplay.Display(result.PublicKeyValue);
                if (display == "")
                {
                    display = result.PrivateKeyName;
                }

                string message = op switch
                {
                    "up" => $"stored in {name}",
                    "down" => $"moved to {envKeysFile}",
                    "push" => $"pushed to {name}",
                    _ => $"pulled to {envKeysFile}"
                };
                if (result.Changed)
                {
                    logger.Success($"□ {message} ({display})");
                }
                else
                {
                    logger.Info($"○ no change ({display})");
                }

                return 0;
            }
            catch (PromptCancelledException)
            {
                return 130;
            }
            catch (Exception error)
            {
                logger.Error(error.Message);

                return 1;
            }
        }
    }
}

public sealed class OnePasswordCommand(IDotenvxLogger logger) : CustodyCommand(logger, "1password", "1Password", "onepassword");

public sealed class BitwardenCommand(IDotenvxLogger logger) : CustodyCommand(logger, "bitwarden", "Bitwarden", "bitwarden");
