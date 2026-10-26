using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Db;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

internal static class AccountOptions
{
    public static Option<string?> Hostname()
    {
        return new("--hostname") { Description = "set Armor ⛨ hostname", HelpName = "hostname" };
    }
}

/// <remarks>Reference source: dotenvx/src/cli/actions/login.js (also `dotenvx login`, hidden)</remarks>
public class LoginAction : Command
{
    private readonly Option<string?> _hostnameOption = AccountOptions.Hostname();

    public LoginAction(IDotenvxLogger logger, string description = "log in to move keys off-device, share with your team, and audit access")
        : base("login", description)
    {
        Add(_hostnameOption);
        SetAction(parseResult => Execute(parseResult, logger));
    }

    private int Execute(ParseResult parseResult, IDotenvxLogger logger)
    {
        using SpinnerScope spinner = SpinnerScope.Start(parseResult, "logging in", ["◐", "◓", "◑", "◒"]);
        string? hostnameOption = parseResult.GetValue(_hostnameOption);
        var options = new Dictionary<string, string>();
        if (hostnameOption is not null)
        {
            options["hostname"] = hostnameOption;
        }

        logger.Debug($"options: {JsonStringify(options)}");

        var session = new Session(logger);
        string hostname = string.IsNullOrEmpty(hostnameOption) ? session.Hostname() : hostnameOption;
        Action cleanupOpenKeyListener = () => { };

        try
        {
            var loginService = new Login(hostname, session);
            LoginStart start = loginService.Run();

            string promptMessage =
                $"◌ press Enter to open [{start.VerificationUri}] and enter code [{FormatCodeHelper.FormatCode(start.UserCode)}]...";
            logger.Debug($"POST {hostname} with deviceCode {start.DeviceCode} at interval {start.Interval}");
            logger.Info(promptMessage);

            cleanupOpenKeyListener = ListenForOpenKey.Listen(() => OpenUrl.Open(start.VerificationUriComplete));
            JsonNode data = loginService.Poll(start.DeviceCode, start.Interval);
            cleanupOpenKeyListener();

            logger.Success($"◉ logged in ({data["username"]})");

            return 0;
        }
        catch (Exception error)
        {
            cleanupOpenKeyListener();
            logger.Error(error.Message);

            return 1;
        }
    }

}

/// <remarks>Reference source: dotenvx/src/cli/actions/logout.js (also `dotenvx logout`, hidden)</remarks>
public class LogoutAction : Command
{
    private readonly Option<string?> _hostnameOption = AccountOptions.Hostname();

    public LogoutAction(IDotenvxLogger logger, string description = "log out of connected security features")
        : base("logout", description)
    {
        Add(_hostnameOption);
        SetAction(parseResult => Execute(parseResult, logger));
    }

    private int Execute(ParseResult parseResult, IDotenvxLogger logger)
    {
        string? hostnameOption = parseResult.GetValue(_hostnameOption);
        var options = new Dictionary<string, string>();
        if (hostnameOption is not null)
        {
            options["hostname"] = hostnameOption;
        }

        logger.Debug($"options: {JsonStringify(options)}");

        var session = new Session(logger);
        string hostname = string.IsNullOrEmpty(hostnameOption) ? session.Hostname() : hostnameOption;
        using SpinnerScope spinner = SpinnerScope.Start(parseResult, "logging out", ["◐", "◓", "◑", "◒"]);

        try
        {
            LogoutResult data = new Logout(hostname, session).Run();
            logger.Success($"◌ logged out ({data.Username})");

            return 0;
        }
        catch (Exception error)
        {
            logger.Error(error.Message);

            return 1;
        }
    }
}

/// <remarks>Reference source: dotenvx/src/cli/actions/curl.js (hidden)</remarks>
public class CurlAction : Command
{
    private readonly Argument<string?> _urlArgument =
        new("url") { Arity = ArgumentArity.ZeroOrOne, Description = "(run dotenvx curl --help for list of urls)" };

    private readonly Option<string?> _requestOption =
        new("--request", "-X") { Description = "HTTP request method", HelpName = "method" };

    private readonly Option<string?> _dataOption =
        new("--data") { Description = "JSON request body", HelpName = "json" };

    private readonly Option<string?> _tokenOption = ArmorCommand.Token();

    public CurlAction(IDotenvxLogger logger) : base("curl", "Dotenvx Armor API")
    {
        Add(_urlArgument);
        Add(_requestOption);
        Add(_dataOption);
        Add(_tokenOption);
        SetAction(parseResult => Execute(parseResult, logger));
    }

    private int Execute(ParseResult parseResult, IDotenvxLogger logger)
    {
        string? urlValue = parseResult.GetValue(_urlArgument);
        if (string.IsNullOrEmpty(urlValue))
        {
            parseResult.RootCommandResult.Command.Parse(["curl", "--help"]).Invoke();

            return 0;
        }

        try
        {
            var session = new Session(logger);
            string? tokenOption = parseResult.GetValue(_tokenOption);
            string? token = string.IsNullOrEmpty(tokenOption) ? session.Token() : tokenOption;
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("missing token. Try [dotenvx login].");
            }

            string? body = RequestBody(parseResult.GetValue(_dataOption));
            string requestOption = parseResult.GetValue(_requestOption) ?? "";
            string method = (requestOption != "" ? requestOption : body is null ? "GET" : "POST").ToUpperInvariant();
            string url = RequestUrl(urlValue, session.Hostname());

            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {token}",
                ["Accept"] = "application/json",
                ["dotenvx-device-public-key"] = session.DevicePublicKey()
            };
            if (body is not null)
            {
                headers["Content-Type"] = "application/json";
            }

            logger.Debug($"request [{method} {url}]");
            HttpResult response = ArmorHttp.Http(url, method, headers, body);
            Console.Out.WriteLine(OutputBody(response.Body));

            return response.StatusCode >= 400 ? 1 : 0;
        }
        catch (Exception error)
        {
            CatchAndLog(logger, error);

            return 1;
        }
    }

    private static string Origin(Uri uri)
    {
        return uri.GetLeftPart(UriPartial.Authority);
    }

    /// <summary>curl.js `requestUrl`: only the trusted Armor origin and no embedded credentials.</summary>
    internal static string RequestUrl(string value, string hostname)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? url) || !Uri.TryCreate(hostname, UriKind.Absolute, out Uri? trusted))
        {
            throw new InvalidOperationException("Invalid URL");
        }

        if (!string.Equals(Origin(url), Origin(trusted), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"refusing to send Armor token to [{Origin(url)}]. expected [{Origin(trusted)}]");
        }

        if (!string.IsNullOrEmpty(url.UserInfo))
        {
            throw new InvalidOperationException("refusing URL containing credentials");
        }

        return url.AbsoluteUri;
    }

    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions IndentedOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    /// <summary>curl.js `requestBody`: `JSON.stringify(JSON.parse(value))`.</summary>
    internal static string? RequestBody(string? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(value)?.ToJsonString(CompactOptions) ?? "null";
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("invalid JSON passed to [--data]");
        }
    }

    /// <summary>curl.js `outputBody`: pretty JSON (two spaces) when the body parses, else the raw text.</summary>
    internal static string OutputBody(string value)
    {
        try
        {
            string text = (JsonNode.Parse(value)?.ToJsonString(IndentedOptions) ?? "null");

            return text.Replace("\r\n", "\n");
        }
        catch (JsonException)
        {
            return value;
        }
    }
}

/// <remarks>Reference source: dotenvx/src/cli/commands/settings.js and cli/actions/settings/*.js</remarks>
public class SettingsCommand : Command
{
    public SettingsCommand(IDotenvxLogger logger) : base("settings", "settings")
    {
        Subcommands.Add(Leaf("username", "print your username", logger, (session, _) =>
        {
            string? value = session.Username();
            if (!string.IsNullOrEmpty(value))
            {
                Console.Out.WriteLine(value);

                return 0;
            }

            logger.Error("login required. Try running [dotenvx armor login].");

            return 1;
        }));
        Subcommands.Add(Leaf("token", "print your access token (--unmask)", logger, (session, unmask) =>
        {
            string? value = session.Token();
            if (!string.IsNullOrEmpty(value) && value.Length > 1)
            {
                Console.Out.WriteLine(unmask ? value : Mask(value));

                return 0;
            }

            logger.Error("missing token. Try generating one with [dotenvx armor login].");

            return 1;
        }, "unmask access token"));
        Subcommands.Add(Leaf("device", "print your device pubkey (--unmask)", logger, (session, unmask) =>
        {
            string value = session.DevicePublicKey();
            if (!string.IsNullOrEmpty(value) && value.Length > 1)
            {
                Console.Out.WriteLine(unmask ? value : Mask(value, 6));

                return 0;
            }

            logger.Error("missing device. Try generating one with [dotenvx armor login].");

            return 1;
        }, "unmask device pubkey"));
        Subcommands.Add(Leaf("hostname", "print hostname", logger, (session, _) =>
        {
            string value = session.Hostname();
            if (!string.IsNullOrEmpty(value) && value.Length > 1)
            {
                Console.Out.WriteLine(value);

                return 0;
            }

            logger.Error("missing hostname. Try running [dotenvx armor login].");

            return 1;
        }));
        Subcommands.Add(Leaf("path", "print path to settings file", logger, (session, _) =>
        {
            string value = session.StorePath();
            if (!string.IsNullOrEmpty(value) && value.Length > 1)
            {
                Console.Out.WriteLine(value);

                return 0;
            }

            logger.Error("missing path. Try running [dotenvx armor login].");

            return 1;
        }));
        Subcommands.Add(Leaf("on", "turn armor on", logger, (session, _) =>
        {
            session.TurnOn();
            logger.Success("✔ armor: on");

            return 0;
        }));
        Subcommands.Add(Leaf("off", "turn armor off", logger, (session, _) =>
        {
            session.TurnOff();
            logger.Success("✔ armor: off");

            return 0;
        }));

        SetAction(parseResult =>
        {
            // settings.js: `this.help()`
            parseResult.RootCommandResult.Command.Parse(["armor", "settings", "--help"]).Invoke();

            return 0;
        });
    }

    private static Command Leaf(string name, string description, IDotenvxLogger logger,
        Func<Session, bool, int> run, string? unmaskDescription = null)
    {
        var command = new Command(name, description);
        Option<bool>? unmask = null;
        if (unmaskDescription is not null)
        {
            unmask = new Option<bool>("--unmask") { Description = unmaskDescription };
            command.Add(unmask);
        }

        command.SetAction(parseResult =>
        {
            try
            {
                return run(new Session(logger), unmask is not null && parseResult.GetValue(unmask));
            }
            catch (Exception error)
            {
                logger.Error(error.Message);

                return 1;
            }
        });

        return command;
    }
}
