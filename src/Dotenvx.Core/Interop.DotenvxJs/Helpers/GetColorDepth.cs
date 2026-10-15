using System.Text.RegularExpressions;

using Dotenvx.Interop.DotenvxJs.Shared;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/colorDepth.js: `if (stream && !stream.isTTY) return 1`, otherwise the stream's own
/// `getColorDepth()`, which is Node's `lib/internal/tty.js` `getColorDepth(env)` ported below (FORCE_COLOR, NO_COLOR,
/// NODE_DISABLE_COLORS, TERM=dumb, the Windows build check, TMUX, CI, TEAMCITY, TERM_PROGRAM, COLORTERM and the TERM tables, as
/// of Node 22.20), so
/// the colours dotenvx writes to a terminal are the ones the Node CLI writes there. INTENTIONALLY NOT PORTED (approved by the
/// user, 2026-10-15): the upstream `catch` that falls back to the TERM variable (0 -> 4, `256color`/`xterm` -> 8) exists only for
/// runtimes whose streams have no `getColorDepth` probe (e.g. deno). The probe here is native code that cannot be missing or throw,
/// so the branch is unreachable; do not add it back. The 4 upstream colorDepth tests that stub the probe are kept as
/// skipped tests in GetColorDepthTest.
/// </remarks>
public static partial class Helpers
{
    private static readonly Dictionary<string, int> TermEnvs = new()
    {
        ["eterm"] = 4, ["cons25"] = 4, ["console"] = 4, ["cygwin"] = 4, ["dtterm"] = 4, ["gnome"] = 4, ["hurd"] = 4,
        ["jfbterm"] = 4, ["konsole"] = 4, ["kterm"] = 4, ["mlterm"] = 4, ["mosh"] = 24, ["putty"] = 4, ["st"] = 4,
        ["rxvt-unicode-24bit"] = 24, ["terminator"] = 24, ["xterm-kitty"] = 24
    };

    private static readonly (string Name, int Colors)[] CiEnvs =
    [
        ("APPVEYOR", 8), ("BUILDKITE", 8), ("CIRCLECI", 24), ("DRONE", 8), ("GITEA_ACTIONS", 24), ("GITHUB_ACTIONS", 24),
        ("GITLAB_CI", 8), ("TRAVIS", 8)
    ];

    private static readonly Regex[] TermEnvsRegExp =
    [
        new("ansi"), new("color"), new("linux"), new("direct"), new("^con[0-9]*x[0-9]"), new("^rxvt"), new("^screen"), new("^xterm"),
        new("^vt100"), new("^vt220")
    ];

    public static int GetColorDepth(ColorStream stream)
    {
        bool isTty = stream == ColorStream.StdOut ? ConsoleTty.IsStdOutTty() : ConsoleTty.IsStdErrTty();

        return isTty ? NodeColorDepth(name => Environment.GetEnvironmentVariable(name), ConsoleTty.Platform) : 1;
    }

    /// <summary>Node's `getColorDepth(env)` (1, 4, 8 or 24 bits-per-colour-channel depth of a terminal).</summary>
    public static int NodeColorDepth(Func<string, string?> env, OsPlatform platform)
    {
        string? forceColor = env("FORCE_COLOR");
        if (forceColor is not null)
        {
            return forceColor switch
            {
                "" or "1" or "true" => 4,
                "2" => 8,
                "3" => 24,
                _ => 1
            };
        }

        if (env("NODE_DISABLE_COLORS") is not null || env("NO_COLOR") is not null || env("TERM") == "dumb")
        {
            return 1;
        }

        if (platform.IsWindows)
        {
            if (platform.Major >= 10)
            {
                if (platform.Build >= 14931)
                {
                    return 24;
                }

                if (platform.Build >= 10586)
                {
                    return 8;
                }
            }

            return 4;
        }

        if (!string.IsNullOrEmpty(env("TMUX")))
        {
            return 24;
        }

        // Azure DevOps
        if (env("TF_BUILD") is not null && env("AGENT_NAME") is not null)
        {
            return 4;
        }

        if (env("CI") is not null)
        {
            foreach ((string name, int colors) in CiEnvs)
            {
                if (env(name) is not null)
                {
                    return colors;
                }
            }

            return env("CI_NAME") == "codeship" ? 8 : 1;
        }

        string? teamcity = env("TEAMCITY_VERSION");
        if (teamcity is not null)
        {
            return Regex.IsMatch(teamcity, @"^(9\.(0*[1-9]\d*)\.|\d{2,}\.)") ? 4 : 1;
        }

        string? termProgram = env("TERM_PROGRAM");
        switch (termProgram)
        {
            case "iTerm.app":
                string? version = env("TERM_PROGRAM_VERSION");

                return string.IsNullOrEmpty(version) || Regex.IsMatch(version, @"^[0-2]\.") ? 8 : 24;
            case "HyperTerm":
            case "MacTerm":
                return 24;
            case "Apple_Terminal":
                return 8;
        }

        string? colorTerm = env("COLORTERM");
        if (colorTerm is "truecolor" or "24bit")
        {
            return 24;
        }

        string? term = env("TERM");
        if (!string.IsNullOrEmpty(term))
        {
            if (term.Contains("truecolor", StringComparison.Ordinal))
            {
                return 24;
            }

            if (Regex.IsMatch(term, "^xterm-256"))
            {
                return 8;
            }

            string lower = term.ToLowerInvariant();
            if (TermEnvs.TryGetValue(lower, out int known))
            {
                return known;
            }

            if (TermEnvsRegExp.Any(pattern => pattern.IsMatch(lower)))
            {
                return 4;
            }
        }

        return string.IsNullOrEmpty(colorTerm) ? 1 : 4;
    }
}

/// <summary>What Node's color probe reads from `process.platform` and `os.release()`.</summary>
public readonly record struct OsPlatform(bool IsWindows, int Major, int Build)
{
    public static OsPlatform Current { get; } = OperatingSystem.IsWindows()
        ? new OsPlatform(true, Environment.OSVersion.Version.Major, Environment.OSVersion.Version.Build)
        : new OsPlatform(false, 0, 0);
}
