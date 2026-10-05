using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

public record ResolveSecretsResult(List<Exception> Errors, List<string> Unresolved);

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/resolveOnePassword.js (`resolveOnePasswordSync` and the async variant, which
/// differs only by the "awaiting 1password" status text).
/// </remarks>
public static class ResolveOnePassword
{
    private static bool IsSecretReference(string value)
    {
        return value.StartsWith("op://", StringComparison.Ordinal);
    }

    private static Exception ResolutionError(string key, bool notFound)
    {
        string message = notFound
            ? $"1Password CLI is not installed and could not resolve {key}"
            : $"1Password CLI failed to resolve {key}";

        return new CustomException($"[1PASSWORD_FAILED] {message}", code: "1PASSWORD_FAILED");
    }

    /// <summary>True when any value is an `op://` reference (the async variant only shows its status then).</summary>
    public static bool HasSecretReferences(IReadOnlyDictionary<string, string> parsed)
    {
        return parsed.Values.Any(IsSecretReference);
    }

    /// <summary>Replaces every `op://` value in <paramref name="parsed" /> with `op read &lt;ref&gt; --no-newline`.</summary>
    public static ResolveSecretsResult Resolve(IDictionary<string, string> parsed, Action<string>? onStatus = null)
    {
        var errors = new List<Exception>();
        var unresolved = new List<string>();
        // the async variant shows "awaiting 1password" (with elapsed seconds) while `op` runs, then "injecting"
        Action stopStatus = parsed.Values.Any(IsSecretReference)
            ? ElapsedStatus.Create(onStatus, "awaiting 1password")
            : () => { };
        bool hadReferences = parsed.Values.Any(IsSecretReference);

        try
        {
            foreach ((string key, string value) in parsed.ToList())
            {
                if (!IsSecretReference(value))
                {
                    continue;
                }

                SecretCliResult result = SecretCli.Exec("op", ["read", value, "--no-newline"], null);
                if (result is { NotFound: false, ExitCode: 0 })
                {
                    parsed[key] = result.Stdout;
                }
                else
                {
                    errors.Add(ResolutionError(key, result.NotFound));
                    unresolved.Add(key);
                }
            }
        }
        finally
        {
            if (hadReferences)
            {
                stopStatus();
                onStatus?.Invoke("injecting");
            }
        }

        return new ResolveSecretsResult(errors, unresolved);
    }
}

/// <remarks>Reference source: dotenvx/src/lib/helpers/resolveBitwardenPassword.js</remarks>
public static partial class ResolveBitwardenPassword
{
    private const string PasswordEnv = "DOTENVX_BITWARDEN_PASSWORD";
    private static readonly string[] Fields = ["username", "password", "uri"];

    [GeneratedRegex("\r?\n$")]
    private static partial Regex TrailingNewline();

    private static bool IsSecretReference(string value)
    {
        return value.StartsWith("bw://", StringComparison.Ordinal);
    }

    private static string SecretValue(string stdout)
    {
        return TrailingNewline().Replace(stdout, "");
    }

    private sealed class SecretException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }

    private static (string Item, string Field) ParseSecretReference(string value)
    {
        string reference = value["bw://".Length..];
        int separatorIndex = reference.LastIndexOf('/');
        string item = separatorIndex < 0 ? reference : reference[..separatorIndex];
        string field = separatorIndex < 0 ? reference : reference[(separatorIndex + 1)..];

        if (separatorIndex < 1 || field == "")
        {
            throw new SecretException("INVALID", "invalid Bitwarden Password Manager reference");
        }

        if (!Fields.Contains(field))
        {
            throw new SecretException("FIELD", $"unsupported Bitwarden Password Manager field {field}");
        }

        return (item, field);
    }

    private static Exception ResolutionError(string key, SecretException? error, bool notFound)
    {
        string message;
        if (notFound || error is { Code: "ENOENT" })
        {
            message = $"Bitwarden Password Manager CLI is not installed and could not resolve {key}";
        }
        else if (error is { Code: "BW_SESSION_MISSING" })
        {
            message =
                $"Bitwarden Password Manager is locked and could not resolve {key}; run 'export BW_SESSION=\"$(bw unlock --raw)\"'";
        }
        else if (error is { Code: "FIELD" })
        {
            message = $"{error.Message} for {key}";
        }
        else if (error is { Code: "INVALID" })
        {
            message = $"invalid Bitwarden Password Manager reference for {key}";
        }
        else
        {
            message = $"Bitwarden Password Manager CLI failed to resolve {key}";
        }

        return new CustomException($"[BITWARDEN_FAILED] {message}", "fix: [https://bitwarden.com/help/cli/]",
            "BITWARDEN_FAILED");
    }

    /// <summary>
    /// Replaces every `bw://item/field` value with `bw get field item`. <paramref name="allowPrompt" /> is the async
    /// variant: with no BW_SESSION and an interactive terminal it asks for the master password and runs `bw unlock`;
    /// the sync variant (and any non-interactive run) reports a locked vault instead.
    /// </summary>
    public static ResolveSecretsResult Resolve(IDictionary<string, string> parsed, bool allowPrompt,
        Action<string>? onStatus = null)
    {
        var errors = new List<Exception>();
        var unresolved = new List<string>();
        string? session = Environment.GetEnvironmentVariable("BW_SESSION");
        if (session == "")
        {
            session = null;
        }

        Action? stopStatus = null;
        void StartStatus()
        {
            stopStatus ??= ElapsedStatus.Create(onStatus, "awaiting bitwarden");
        }

        try
        {
        foreach ((string key, string value) in parsed.ToList())
        {
            if (!IsSecretReference(value))
            {
                continue;
            }

            try
            {
                (string item, string field) = ParseSecretReference(value);
                if (session is not null)
                {
                    StartStatus();
                }

                session ??= UnlockSession(allowPrompt, StartStatus);

                SecretCliResult result = SecretCli.Exec("bw", ["get", field, item],
                    new Dictionary<string, string> { ["BW_SESSION"] = session });
                if (result.NotFound || result.ExitCode != 0)
                {
                    errors.Add(ResolutionError(key, null, result.NotFound));
                    unresolved.Add(key);
                    continue;
                }

                parsed[key] = SecretValue(result.Stdout);
            }
            catch (PromptCancelledException)
            {
                throw;
            }
            catch (SecretException error)
            {
                errors.Add(ResolutionError(key, error, false));
                unresolved.Add(key);
            }
        }

        }
        finally
        {
            if (stopStatus is not null)
            {
                stopStatus();
                onStatus?.Invoke("injecting");
            }
        }

        return new ResolveSecretsResult(errors, unresolved);
    }

    private static string UnlockSession(bool allowPrompt, Action startStatus)
    {
        if (allowPrompt && SecretCli.IsInteractive())
        {
            // createSpinner.pause() / resume() around the prompt, then the status starts
            Spinners.Pause();
            string password = Prompts.Password("Bitwarden master password", "◇") ?? throw new PromptCancelledException();
            Spinners.Resume();
            startStatus();
            SecretCliResult result = SecretCli.Exec("bw", ["unlock", "--passwordenv", PasswordEnv, "--raw"],
                new Dictionary<string, string> { [PasswordEnv] = password });
            if (result.NotFound || result.ExitCode != 0)
            {
                throw new SecretException(result.NotFound ? "ENOENT" : "UNLOCK_FAILED", "bw unlock failed");
            }

            return SecretValue(result.Stdout);
        }

        throw new SecretException("BW_SESSION_MISSING", "Bitwarden Password Manager requires an unlocked BW_SESSION");
    }
}
