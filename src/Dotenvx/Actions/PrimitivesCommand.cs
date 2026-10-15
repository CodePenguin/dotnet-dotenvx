using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Primitives;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/commands/primitives.js and cli/actions/{primitivesProfile,primitivesRedact,policy,
/// primitivesCheck}.js (hidden group of standalone primitive operations; run from the working directory's `Envfile`).
/// </remarks>
public class PrimitivesCommand : Command
{
    private readonly IDotenvxLogger _logger;

    public PrimitivesCommand(IDotenvxLogger logger) : base("primitives", "standalone primitive operations")
    {
        _logger = logger;

        Command profile = new("profile", "resolve an Envfile profile to comma-separated filenames");
        Option<string?> profileName = new("--profile") { Description = "explicit profile name", HelpName = "name" };
        Option<string[]?> profileFile = FileOption("explicit comma-separated files, overriding profile selection");
        profile.Add(profileName);
        profile.Add(profileFile);
        profile.SetAction(parse => Guard(() =>
        {
            string source = File.ReadAllText("Envfile", Encoding.UTF8);
            string file = Primitives.Primitives.Profile(source, new ProfileOptions
            {
                F = Joined(parse.GetValue(profileFile)),
                Profile = parse.GetValue(profileName),
                ProcessEnv = ProcessEnv()
            });
            Console.Out.Write(file + "\n");

            return 0;
        }));
        Subcommands.Add(profile);

        Command redact = new("redact", "replace secret strings in text with [REDACTED]");
        Argument<string?> text = new("text") { Arity = ArgumentArity.ZeroOrOne, Description = "text to redact" };
        Option<string[]> secret = new("--secret", "-s")
        {
            Description = "literal secret to redact (repeat for multiple secrets)", HelpName = "value", Required = true,
            AllowMultipleArgumentsPerToken = false
        };
        Option<bool> stdin = new("--stdin") { Description = "read text from stdin" };
        redact.Add(text);
        redact.Add(secret);
        redact.Add(stdin);
        redact.SetAction(parse => Guard(() =>
        {
            string? value = parse.GetValue(text);
            if (parse.GetValue(stdin))
            {
                if (value is not null)
                {
                    throw new Exception("Cannot combine a text argument with --stdin");
                }

                value = Console.In.ReadToEnd();
            }

            if (value is null)
            {
                throw new Exception("Provide text or use --stdin");
            }

            Console.Out.Write(Primitives.Primitives.Redact(value, parse.GetValue(secret) ?? []));

            return 0;
        }));
        Subcommands.Add(redact);

        Command policy = new("policy", "print the Envfile policy for selected files as JSON");
        Option<string[]?> policyFile = FileOption("comma-separated env files, first file wins (default: .env)");
        policy.Add(policyFile);
        policy.SetAction(parse => Guard(() =>
        {
            string source = File.ReadAllText("Envfile", Encoding.UTF8);
            string file = Primitives.Primitives.Profile(source, new ProfileOptions { F = Joined(parse.GetValue(policyFile)) ?? ".env" });
            Console.Out.Write(PolicyJson(Primitives.Primitives.Policy(source, file)).ToJsonString(Indented).ReplaceLineEndings("\n") + "\n");

            return 0;
        }));
        Subcommands.Add(policy);

        Command check = new("check", "check selected env files against Envfile and print redacted JSON");
        Option<string[]?> checkFile = FileOption("comma-separated env files, first file wins (default: .env)");
        check.Add(checkFile);
        check.SetAction(parse => Guard(() => Check(Joined(parse.GetValue(checkFile)) ?? ".env")));
        Subcommands.Add(check);

        Subcommands.Add(KeyCommand("keypair", "generate or restore a keypair and print JSON", false));
        Subcommands.Add(KeyCommand("derive", "derive and print a public key from a private key", true));

        SetAction(parse =>
        {
            parse.RootCommandResult.Command.Parse(["primitives", "--help"]).Invoke();

            return 0;
        });
    }

    private static readonly System.Text.Json.JsonSerializerOptions Indented = new() { WriteIndented = true };

    private static Option<string[]?> FileOption(string description)
    {
        return new("--file", "-f") { Description = description, HelpName = "paths" };
    }

    /// <summary>Repeated `-f` values are joined with commas (`previous === undefined ? value : previous,value`).</summary>
    private static string? Joined(string[]? values)
    {
        return values is { Length: > 0 } ? string.Join(',', values) : null;
    }

    private static Dictionary<string, string> ProcessEnv()
    {
        return new Dictionary<string, string>(global::Dotenvx.Helpers.EnvironmentHelper.GetEnvironmentVariablesAsDictionary());
    }

    private int Guard(Func<int> body)
    {
        try
        {
            return body();
        }
        catch (Exception error)
        {
            global::Dotenvx.Interop.DotenvxJs.Helpers.Helpers.CatchAndLog(_logger, error);

            return 1;
        }
    }

    private static JsonNode PolicyJson(EnvPolicy policy)
    {
        JsonObject keys = new();
        foreach ((string key, KeyPolicy rule) in policy.Keys)
        {
            JsonObject env = new() { ["type"] = rule.Env.Type };
            if (rule.Env.Min != null)
            {
                env["min"] = rule.Env.Min;
            }

            if (rule.Env.Max != null)
            {
                env["max"] = rule.Env.Max;
            }

            if (rule.Env.Enum != null)
            {
                env["enum"] = new JsonArray(rule.Env.Enum.Select(item => (JsonNode?)item).ToArray());
            }

            env["required"] = rule.Env.Required;
            env["strict"] = rule.Env.Strict;
            keys[key] = new JsonObject
            {
                ["env"] = env, ["fs"] = new JsonObject { ["encrypted"] = rule.Encrypted },
                ["stdio"] = new JsonObject { ["redacted"] = rule.Redacted }
            };
        }

        return new JsonObject { ["keys"] = keys };
    }

    private static int Check(string fileArg)
    {
        string source = File.ReadAllText("Envfile", Encoding.UTF8);
        string file = Primitives.Primitives.Profile(source, new ProfileOptions { F = fileArg });
        EnvPolicy rules = Primitives.Primitives.Policy(source, file);
        Dictionary<string, string> processEnv = ProcessEnv();
        List<string> keys = [.. rules.Keys.Keys];
        foreach (string filename in file == "" ? [] : file.Split(','))
        {
            PrimitivesParseOutput output = Primitives.Primitives.ParseSync(
                File.ReadAllText(filename, Encoding.UTF8),
                new PrimitivesParseOptions
                {
                    ProcessEnv = processEnv,
                    Fk = [Path.Combine(Path.GetDirectoryName(filename) is { Length: > 0 } dir ? dir : ".", ".env.keys")]
                });
            if (output.Errors.Count > 0)
            {
                throw new Exception("Could not decrypt selected environment files");
            }

            foreach (string key in output.Parsed.Keys.Where(key => !keys.Contains(key)))
            {
                keys.Add(key);
            }

            foreach ((string key, string value) in output.Injected)
            {
                processEnv[key] = value;
            }
        }

        Dictionary<string, string> finalEnv = [];
        foreach (string key in keys.Where(processEnv.ContainsKey))
        {
            finalEnv[key] = processEnv[key];
        }

        CheckResult result = Primitives.Primitives.Check(rules, finalEnv);
        JsonObject checkedValues = new();
        foreach ((string key, string value) in result.Checked)
        {
            checkedValues[key] = value;
        }

        JsonArray errors = new(result.Errors.Select(error => (JsonNode?)new JsonObject
        {
            ["key"] = error.Key, ["code"] = error.Code, ["rule"] = error.Rule, ["message"] = error.Message,
            ["strict"] = error.Strict
        }).ToArray());
        Console.Out.Write(new JsonObject { ["checked"] = checkedValues, ["errors"] = errors }.ToJsonString(Indented).ReplaceLineEndings("\n") + "\n");

        return result.Errors.Count > 0 ? 1 : 0;
    }

    private Command KeyCommand(string name, string description, bool required)
    {
        Command command = new(name, description);
        Argument<string?> privateKey = new("privateKey")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = required ? "hex-encoded private key" : "hex-encoded private key to restore"
        };
        Option<bool> stdin = new("--stdin")
        {
            Description = required ? "read the private key from stdin" : "read the private key from stdin"
        };
        command.Add(privateKey);
        command.Add(stdin);
        command.SetAction(parse => Guard(() =>
        {
            string? value = parse.GetValue(privateKey);
            if (parse.GetValue(stdin))
            {
                if (value is not null)
                {
                    throw new Exception("Cannot combine a private key argument with --stdin");
                }

                value = Console.In.ReadToEnd().Trim();
                if (value.Length == 0)
                {
                    throw new Exception("Expected a private key on stdin");
                }
            }

            if (required && value is null)
            {
                throw new Exception("Provide a private key argument or use --stdin");
            }

            if (value is not null && !System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-fA-F0-9]{64}$"))
            {
                throw new Exception("Expected one 64-character hex private key");
            }

            if (required)
            {
                Console.Out.WriteLine(Primitives.Primitives.Derive(value!));
            }
            else
            {
                (string publicKey, string priv) = Primitives.Primitives.Keypair(value);
                Console.Out.WriteLine(new JsonObject { ["publicKey"] = publicKey, ["privateKey"] = priv }.ToJsonString());
            }

            return 0;
        }));

        return command;
    }
}
