using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Dotenvx.Interop.DotenvxJs.Events;

namespace Dotenvx.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/events/cli.js (`trackCli`). Wraps a command's action: the options the user gave, the
/// first argument (for `key` commands), the exit code, the failure and the duration go to Armor as one `cli/&lt;command&gt;`
/// event, delivered by a detached `_deliver-events` worker (nothing happens when Armor is off or signed out).
/// </remarks>
public static class EventTracking
{
    // commander's variadic options (`<keys...>`) accumulate every value; the rest keep their last one
    private static readonly HashSet<string> Variadic = ["ignore", "includeKey", "excludeKey", "key"];

    // `set` and `encrypt` declare --env-keys-file without the collector the other commands use
    private static readonly HashSet<string> SingleEnvKeysFile = ["set", "encrypt"];

    /// <summary>Replaces <paramref name="command" />'s action with one that records the command's event.</summary>
    public static void Track(Command command, string name)
    {
        if (command.Action is { } inner)
        {
            command.Action = new TrackedAction($"cli/{name}", inner);
        }
    }

    /// <summary>commander's `optsWithGlobals()` filtered to the options given on the command line (source `cli`).</summary>
    public static Dictionary<string, object?> GivenOptions(ParseResult parseResult)
    {
        var options = new Dictionary<string, object?>();
        string commandName = parseResult.CommandResult.Command.Name;
        for (CommandResult? result = parseResult.CommandResult; result is not null; result = result.Parent as CommandResult)
        {
            foreach (OptionResult optionResult in result.Children.OfType<OptionResult>())
            {
                if (optionResult.Implicit)
                {
                    continue;
                }

                (string attribute, bool negated) = AttributeName(optionResult);
                if (options.ContainsKey(attribute))
                {
                    continue;
                }

                string[] tokens = [.. optionResult.Tokens.Select(token => token.Value)];
                options[attribute] = negated ? false : Value(attribute, optionResult, tokens, commandName);
            }
        }

        return options;
    }

    private static object? Value(string attribute, OptionResult optionResult, string[] tokens, string commandName)
    {
        if (optionResult.Option.ValueType == typeof(bool) && tokens.Length == 0)
        {
            return true;
        }

        if (attribute == "envFile")
        {
            return tokens.SelectMany(token => token.Split(',')).Select(item => item.Trim()).Where(item => item.Length > 0).ToList();
        }

        if (attribute == "envKeysFile" && !SingleEnvKeysFile.Contains(commandName))
        {
            return tokens.Length == 1 ? tokens[0] : tokens.ToList();
        }

        if (Variadic.Contains(attribute))
        {
            return tokens.ToList();
        }

        // optional value (`--mask [characters]`): true when no value was given
        return tokens.Length == 0 ? true : tokens[^1];
    }

    /// <summary>The commander attribute name of an option (`--env-keys-file` => `envKeysFile`, `--no-armor` => `armor`).</summary>
    private static (string Attribute, bool Negated) AttributeName(OptionResult optionResult)
    {
        // `--pp` is a separate commander option from `-pp, --pretty-print`
        if (optionResult.IdentifierToken?.Value == "--pp")
        {
            return ("pp", false);
        }

        string name = optionResult.Option.Name.TrimStart('-');
        bool negated = name.StartsWith("no-", StringComparison.Ordinal);
        if (negated)
        {
            name = name[3..];
        }

        // `--file` is the advertised spelling of `--env-file`, which keeps the envFile attribute
        if (name == "file")
        {
            name = "env-file";
        }

        string[] parts = name.Split('-');

        return (parts[0] + string.Concat(parts.Skip(1).Select(part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part[1..])),
            negated);
    }

    private sealed class TrackedAction(string name, CommandLineAction inner) : AsynchronousCommandLineAction
    {
        public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
        {
            Dictionary<string, object?> options = GivenOptions(parseResult);
            EventScope.Begin();
            EventRecorder? events = null;
            try
            {
                var sink = EventMetadata.Catalog.ContainsKey(name) ? EventBackground.Create(options) : null;
                events = new EventRecorder(name, options, options, sink);
                if (EventMetadata.Catalog.TryGetValue(name, out bool keyArgument) && keyArgument &&
                    parseResult.CommandResult.Command.Arguments.FirstOrDefault() is { } argument &&
                    parseResult.GetResult(argument)?.Tokens.FirstOrDefault()?.Value is { } key)
                {
                    events.Add(new Dictionary<string, object?> { ["key"] = key });
                }
            }
            catch
            {
                // events never affect the command
            }

            int code = 1;
            try
            {
                code = inner switch
                {
                    SynchronousCommandLineAction sync => sync.Invoke(parseResult),
                    AsynchronousCommandLineAction asynchronous => await asynchronous.InvokeAsync(parseResult, cancellationToken),
                    _ => 0
                };

                return code;
            }
            catch (Exception error)
            {
                events?.Fail(error);
                throw;
            }
            finally
            {
                await Finish(events, code);
            }
        }

        private static async Task Finish(EventRecorder? events, int code)
        {
            EventScope? scope = EventScope.Current;
            EventScope.End();
            if (events is null)
            {
                return;
            }

            try
            {
                if (scope?.Error is { } error)
                {
                    events.Fail(error);
                }
                else if (scope?.ErrorCode is { } errorCode)
                {
                    events.Fail(errorCode);
                }

                var details = new Dictionary<string, object?>();
                if (scope?.ErrorCount is { } errorCount)
                {
                    details["error_count"] = (long)errorCount;
                }

                if (scope?.Signal is { } signal)
                {
                    details["signal"] = signal;
                }

                if (details.Count > 0)
                {
                    events.Add(details);
                }

                await events.Complete(code == 0 && !(scope?.ErrorCount > 0) ? "success" : "unsuccessful",
                    new Dictionary<string, object?> { ["exit_code"] = (long)code });
            }
            catch
            {
                // events never affect the command
            }
        }
    }
}
