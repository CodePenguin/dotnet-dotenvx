using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using Shouldly;

namespace Dotenvx.Core.Test.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/resolveOnePassword.js and resolveBitwardenPassword.js. /// `execFile` vs `execFileSync` split collapses into one process seam here (`SecretCli.Exec`).
/// </remarks>
public class ResolveSecretReferencesTest : EnvironmentTestBase
{
    private const string ItemId = "7ac9cae8-5067-4faf-b6ab-acfd00e2c328";

    private readonly List<(string Command, string[] Args, IReadOnlyDictionary<string, string>? Env)> _calls = [];

    public ResolveSecretReferencesTest()
    {
        Environment.SetEnvironmentVariable("BW_SESSION", "test-session");
    }

    private void Stub(Func<string, string[], IReadOnlyDictionary<string, string>?, SecretCliResult> exec)
    {
        SecretCli.Exec = (command, args, env) =>
        {
            _calls.Add((command, args, env));
            return exec(command, args, env);
        };
    }

    private void Forbid()
    {
        SecretCli.Exec = (_, _, _) => throw new InvalidOperationException("should not call the CLI");
    }

    /// <remarks>Refer to "resolves op:// values asynchronously"</remarks>
    [Fact]
    public void TestResolvesOnePasswordValues()
    {
        Stub((_, _, _) => new SecretCliResult(0, "super-secret"));
        var parsed = new Dictionary<string, string>
        {
            ["SECRET"] = "op://vault/item/password",
            ["SECOND_SECRET"] = "op://vault/second/password",
            ["PLAIN"] = "value"
        };

        var statuses = new List<string>();
        ResolveSecretsResult result = ResolveOnePassword.Resolve(parsed, statuses.Add);

        statuses.ShouldBe(["awaiting 1password", "injecting"]);
        parsed["SECRET"].ShouldBe("super-secret");
        parsed["SECOND_SECRET"].ShouldBe("super-secret");
        parsed["PLAIN"].ShouldBe("value");
        _calls[0].Command.ShouldBe("op");
        _calls[0].Args.ShouldBe(["read", "op://vault/item/password", "--no-newline"]);
        _calls[1].Args.ShouldBe(["read", "op://vault/second/password", "--no-newline"]);
        result.Errors.ShouldBeEmpty();
        result.Unresolved.ShouldBeEmpty();
    }

    /// <remarks>Refer to "resolves op:// values synchronously without a shell"</remarks>
    [Fact]
    public void TestResolvesOnePasswordValuesWithoutAShell()
    {
        Stub((_, _, _) => new SecretCliResult(0, "super-secret"));
        var parsed = new Dictionary<string, string>
        {
            ["SECRET"] = "op://vault/item/password; echo unsafe",
            ["PLAIN"] = "value"
        };

        ResolveSecretsResult result = ResolveOnePassword.Resolve(parsed);

        parsed["SECRET"].ShouldBe("super-secret");
        _calls[0].Args.ShouldBe(["read", "op://vault/item/password; echo unsafe", "--no-newline"]);
        result.Errors.ShouldBeEmpty();
    }

    /// <remarks>Refer to "reports a missing op CLI and preserves the unresolved reference"</remarks>
    [Fact]
    public void TestReportsAMissingOnePasswordCli()
    {
        Stub((_, _, _) => new SecretCliResult(-1, "", true));
        var parsed = new Dictionary<string, string>
        {
            ["DATABASE_PASSWORD"] = "op://private/reference",
            ["PLAIN"] = "value"
        };

        ResolveSecretsResult result = ResolveOnePassword.Resolve(parsed);

        parsed["DATABASE_PASSWORD"].ShouldBe("op://private/reference");
        result.Unresolved.ShouldBe(["DATABASE_PASSWORD"]);
        var error = result.Errors[0].ShouldBeOfType<CustomException>();
        error.Code.ShouldBe("1PASSWORD_FAILED");
        error.Message.ShouldBe(
            "[1PASSWORD_FAILED] 1Password CLI is not installed and could not resolve DATABASE_PASSWORD");
        error.Help.ShouldBe("fix: [https://www.1password.dev/cli/get-started]");
    }

    /// <remarks>Not from a JS test - a failing `op read` (non-zero exit) is "failed to resolve", not "not installed".</remarks>
    [Fact]
    public void TestReportsAFailingOnePasswordRead()
    {
        Stub((_, _, _) => new SecretCliResult(1, ""));
        var parsed = new Dictionary<string, string> { ["A"] = "op://v/i/f" };

        ResolveSecretsResult result = ResolveOnePassword.Resolve(parsed);

        result.Errors[0].Message.ShouldBe("[1PASSWORD_FAILED] 1Password CLI failed to resolve A");
        parsed["A"].ShouldBe("op://v/i/f");
    }

    /// <remarks>Refer to "resolves supported bw:// values asynchronously"</remarks>
    [Fact]
    public void TestResolvesBitwardenValues()
    {
        Stub((_, _, _) => new SecretCliResult(0, "super-secret\n"));
        var parsed = new Dictionary<string, string>
        {
            ["SECRET"] = $"bw://{ItemId}/password",
            ["SECOND_SECRET"] = $"bw://{ItemId}/username",
            ["PLAIN"] = "value"
        };

        var statuses = new List<string>();
        ResolveSecretsResult result = ResolveBitwardenPassword.Resolve(parsed, true, statuses.Add);

        statuses.ShouldBe(["awaiting bitwarden", "injecting"]);
        parsed["SECRET"].ShouldBe("super-secret");
        parsed["SECOND_SECRET"].ShouldBe("super-secret");
        parsed["PLAIN"].ShouldBe("value");
        _calls[0].Command.ShouldBe("bw");
        _calls[0].Args.ShouldBe(["get", "password", ItemId]);
        _calls[1].Args.ShouldBe(["get", "username", ItemId]);
        _calls[0].Env!["BW_SESSION"].ShouldBe("test-session");
        result.Errors.ShouldBeEmpty();
    }

    /// <remarks>Refer to "reuses the session within one env row and unlocks again for the next row"</remarks>
    [Fact]
    public void TestReusesTheSessionWithinOneRowAndUnlocksAgainForTheNext()
    {
        Environment.SetEnvironmentVariable("BW_SESSION", null);
        SecretCli.IsInteractive = () => true;
        Prompts.Password = (_, _, _) => "master-password";
        Stub((_, args, _) => args[0] == "unlock"
            ? new SecretCliResult(0, "request-session\n")
            : new SecretCliResult(0, $"{args[1]}-value\n"));
        var parsed = new Dictionary<string, string>
        {
            ["PASSWORD"] = $"bw://{ItemId}/password",
            ["URI"] = $"bw://{ItemId}/uri"
        };

        var spinnerCalls = new List<string>();
        Spinners.Pause = () => spinnerCalls.Add("pause");
        Spinners.Resume = () => spinnerCalls.Add("resume");
        var statuses = new List<string>();
        ResolveSecretsResult result = ResolveBitwardenPassword.Resolve(parsed, true, statuses.Add);
        var nextParsed = new Dictionary<string, string> { ["USERNAME"] = $"bw://{ItemId}/username" };
        ResolveSecretsResult nextResult = ResolveBitwardenPassword.Resolve(nextParsed, true, statuses.Add);

        spinnerCalls.ShouldBe(["pause", "resume", "pause", "resume"]);
        statuses.ShouldBe(["awaiting bitwarden", "injecting", "awaiting bitwarden", "injecting"]);

        parsed.ShouldBe(new Dictionary<string, string> { ["PASSWORD"] = "password-value", ["URI"] = "uri-value" });
        nextParsed.ShouldBe(new Dictionary<string, string> { ["USERNAME"] = "username-value" });
        _calls.Select(call => call.Args).ShouldBe(
        [
            ["unlock", "--passwordenv", "DOTENVX_BITWARDEN_PASSWORD", "--raw"],
            ["get", "password", ItemId],
            ["get", "uri", ItemId],
            ["unlock", "--passwordenv", "DOTENVX_BITWARDEN_PASSWORD", "--raw"],
            ["get", "username", ItemId]
        ]);
        _calls[0].Env!["DOTENVX_BITWARDEN_PASSWORD"].ShouldBe("master-password");
        _calls[1].Env!["BW_SESSION"].ShouldBe("request-session");
        _calls[2].Env!["BW_SESSION"].ShouldBe("request-session");
        Environment.GetEnvironmentVariable("BW_SESSION").ShouldBeNull("does not export the session globally");
        result.Errors.ShouldBeEmpty();
        nextResult.Errors.ShouldBeEmpty();
    }

    /// <remarks>Refer to "propagates prompt cancellation without resolving it as a Bitwarden failure"</remarks>
    [Fact]
    public void TestPropagatesPromptCancellation()
    {
        Environment.SetEnvironmentVariable("BW_SESSION", null);
        SecretCli.IsInteractive = () => true;
        Prompts.Password = (_, _, _) => throw new PromptCancelledException();
        Forbid();

        Should.Throw<PromptCancelledException>(() => ResolveBitwardenPassword.Resolve(
            new Dictionary<string, string> { ["PASSWORD"] = $"bw://{ItemId}/password" }, true));
    }

    /// <remarks>Refer to "resolves username, password, and uri synchronously without a shell"</remarks>
    [Fact]
    public void TestResolvesUsernamePasswordAndUri()
    {
        Stub((_, args, _) => new SecretCliResult(0, $"{args[1]}-value\n"));
        var parsed = new Dictionary<string, string>
        {
            ["USERNAME"] = $"bw://{ItemId}/username",
            ["PASSWORD"] = $"bw://{ItemId}/password",
            ["URI"] = $"bw://{ItemId}/uri"
        };

        ResolveSecretsResult result = ResolveBitwardenPassword.Resolve(parsed, false);

        parsed.ShouldBe(new Dictionary<string, string>
        {
            ["USERNAME"] = "username-value",
            ["PASSWORD"] = "password-value",
            ["URI"] = "uri-value"
        });
        _calls.Select(call => call.Args).ShouldBe(
            [["get", "username", ItemId], ["get", "password", ItemId], ["get", "uri", ItemId]]);
        result.Errors.ShouldBeEmpty();
    }

    /// <remarks>Refer to "rejects unsupported fields before calling bw"</remarks>
    [Fact]
    public void TestRejectsUnsupportedFields()
    {
        Forbid();
        var parsed = new Dictionary<string, string> { ["TOTP"] = $"bw://{ItemId}/totp", ["PLAIN"] = "value" };

        ResolveSecretsResult result = ResolveBitwardenPassword.Resolve(parsed, false);

        parsed["TOTP"].ShouldBe($"bw://{ItemId}/totp");
        var error = result.Errors[0].ShouldBeOfType<CustomException>();
        error.Code.ShouldBe("BITWARDEN_FAILED");
        error.Message.ShouldBe("[BITWARDEN_FAILED] unsupported Bitwarden Password Manager field totp for TOTP");
    }

    /// <remarks>Refer to "resolves a human-readable item name with spaces"</remarks>
    [Fact]
    public void TestResolvesAHumanReadableItemNameWithSpaces()
    {
        Stub((_, _, _) => new SecretCliResult(0, "super-secret\n"));
        var parsed = new Dictionary<string, string>
        {
            ["PASSWORD"] = "bw://My GitHub Account/password",
            ["PLAIN"] = "value"
        };

        ResolveSecretsResult result = ResolveBitwardenPassword.Resolve(parsed, false);

        parsed["PASSWORD"].ShouldBe("super-secret");
        _calls.Count.ShouldBe(1);
        _calls[0].Command.ShouldBe("bw");
        _calls[0].Args.ShouldBe(["get", "password", "My GitHub Account"]);
        result.Errors.ShouldBeEmpty();
    }

    /// <remarks>Refer to "rejects a reference without an item selector"</remarks>
    [Fact]
    public void TestRejectsAReferenceWithoutAnItemSelector()
    {
        Forbid();
        var parsed = new Dictionary<string, string> { ["PASSWORD"] = "bw:///password", ["PLAIN"] = "value" };

        ResolveSecretsResult result = ResolveBitwardenPassword.Resolve(parsed, false);

        parsed["PASSWORD"].ShouldBe("bw:///password");
        var error = result.Errors[0].ShouldBeOfType<CustomException>();
        error.Code.ShouldBe("BITWARDEN_FAILED");
        error.Message.ShouldBe("[BITWARDEN_FAILED] invalid Bitwarden Password Manager reference for PASSWORD");
    }

    /// <remarks>Refer to "reports a missing bw CLI and preserves the unresolved reference"</remarks>
    [Fact]
    public void TestReportsAMissingBitwardenCli()
    {
        Stub((_, _, _) => new SecretCliResult(-1, "", true));
        var parsed = new Dictionary<string, string>
        {
            ["PASSWORD"] = $"bw://{ItemId}/password",
            ["PLAIN"] = "value"
        };

        ResolveSecretsResult result = ResolveBitwardenPassword.Resolve(parsed, false);

        parsed["PASSWORD"].ShouldBe($"bw://{ItemId}/password");
        var error = result.Errors[0].ShouldBeOfType<CustomException>();
        error.Code.ShouldBe("BITWARDEN_FAILED");
        error.Message.ShouldBe(
            "[BITWARDEN_FAILED] Bitwarden Password Manager CLI is not installed and could not resolve PASSWORD");
        error.Help.ShouldBe("fix: [https://bitwarden.com/help/cli/]");
    }

    /// <remarks>Refer to "fails immediately when BW_SESSION is missing instead of prompting"</remarks>
    [Fact]
    public void TestFailsImmediatelyWhenBwSessionIsMissing()
    {
        Environment.SetEnvironmentVariable("BW_SESSION", null);
        Forbid();
        var parsed = new Dictionary<string, string> { ["API_KEY"] = $"bw://{ItemId}/password", ["PLAIN"] = "value" };

        ResolveSecretsResult result = ResolveBitwardenPassword.Resolve(parsed, false);

        parsed["API_KEY"].ShouldBe($"bw://{ItemId}/password");
        var error = result.Errors[0].ShouldBeOfType<CustomException>();
        error.Code.ShouldBe("BITWARDEN_FAILED");
        error.Message.ShouldBe(
            "[BITWARDEN_FAILED] Bitwarden Password Manager is locked and could not resolve API_KEY; run 'export BW_SESSION=\"$(bw unlock --raw)\"'");
    }
}
