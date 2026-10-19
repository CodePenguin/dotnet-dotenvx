using System;
using System.CommandLine;

using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Api;
using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Providers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>Reference source: dotenvx/src/cli/actions/feedback.js (hidden `feedback [message]`).</remarks>
public class FeedbackAction : Command
{
    private readonly Argument<string?> _messageArgument =
        new("message") { Arity = ArgumentArity.ZeroOrOne, Description = "feedback message" };

    private readonly Option<string?> _hostnameOption =
        new("--hostname") { Description = "set API hostname", HelpName = "hostname" };

    private readonly IDotenvxLogger _logger;

    public FeedbackAction(IDotenvxLogger logger) : base("feedback", "send feedback to dotenvx")
    {
        _logger = logger;
        Hidden = true;
        Add(_messageArgument);
        Add(_hostnameOption);
        SetAction(Execute);
    }

    private int Execute(ParseResult parseResult)
    {
        SpinnerScope? spinner = null;
        try
        {
            string? body = parseResult.GetValue(_messageArgument);
            if (body is null)
            {
                if (!ConsoleTty.IsStdInTty())
                {
                    throw new Exception("provide a message: dotenvx feedback \"Your feedback\"");
                }

                body = Prompts.InputText("Send feedback") ?? throw new PromptCancelledException();
            }

            if (body.Trim().Length == 0)
            {
                throw new Exception("feedback cannot be empty");
            }

            string? hostnameOption = parseResult.GetValue(_hostnameOption);
            string hostname = string.IsNullOrEmpty(hostnameOption) ? new Session().Hostname() : hostnameOption;
            spinner = SpinnerScope.Start(parseResult, "sending");
            ArmorApi.PostFeedback(hostname, body);
            spinner.Dispose();
            _logger.Success("✔ Feedback sent, thanks!");

            return 0;
        }
        catch (PromptCancelledException)
        {
            spinner?.Dispose();

            return 130;
        }
        catch (Exception error)
        {
            spinner?.Dispose();
            _logger.Error(error.Message);

            return 1;
        }
    }
}
