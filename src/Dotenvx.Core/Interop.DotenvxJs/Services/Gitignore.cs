using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/cli/actions/ext/gitignore.js. Upstream logs directly inside its
/// Generic.run() (logger.success/logger.info per file) - this port instead follows this codebase's
/// own established convention (every other service returns data, only the Action layer logs), so
/// GitignoreGeneric.Run() returns a GitignoreGenericResult and GitignoreAction does the logging.
/// </remarks>
public class Gitignore(string[]? patterns = null)
{
    private static readonly Func<string[]?, Gitignore> DefaultFactory = patterns => new Gitignore(patterns);

    protected Gitignore() : this(null)
    {
        // For unit tests
    }

    public static Func<string[]?, Gitignore> Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public virtual GitignoreGenericResult[] Run()
    {
        return
        [
            new GitignoreGit(patterns).Run(),
            new GitignoreDocker(patterns).Run(),
            new GitignoreNpm(patterns).Run(),
            new GitignoreVercel(patterns).Run()
        ];
    }
}

/// <remarks>Reference source: dotenvx/src/cli/actions/ext/gitignore.js's Generic class.</remarks>
public class GitignoreGeneric(string filename, string[]? patterns = null, bool touchFile = false)
{
    private static readonly string[] DefaultPatterns = [".env*"];

    public string Filename { get; } = filename;
    public string[] Patterns { get; } = patterns ?? DefaultPatterns;
    public bool TouchFile { get; } = touchFile;

    public void Append(string pattern)
    {
        Fsx.AppendFileSync(Filename, $"\n{pattern}");
    }

    public GitignoreGenericResult Run(string verboseMessage)
    {
        if (!Fsx.ExistsSync(Filename))
        {
            if (TouchFile && Patterns.Length > 0)
            {
                Fsx.WriteFileX(Filename, "");
            }
            else
            {
                return new GitignoreGenericResult
                {
                    Filename = Filename, Patterns = Patterns, Changed = false, Skipped = true,
                    VerboseMessage = verboseMessage
                };
            }
        }

        string[] lines = Fsx.ReadFileX(Filename).Split(["\r\n", "\n"], StringSplitOptions.None);
        List<string> changedPatterns = [];
        foreach (string pattern in Patterns)
        {
            if (!lines.Contains(pattern.Trim()))
            {
                Append(pattern);
                changedPatterns.Add(pattern.Trim());
            }
        }

        return new GitignoreGenericResult
        {
            Filename = Filename, Patterns = Patterns, Changed = changedPatterns.Count > 0, Skipped = false,
            VerboseMessage = verboseMessage
        };
    }
}

/// <remarks>Reference source: dotenvx/src/cli/actions/ext/gitignore.js's Git class.</remarks>
public class GitignoreGit(string[]? patterns = null)
{
    public GitignoreGenericResult Run()
    {
        return new GitignoreGeneric(".gitignore", patterns, true).Run("add to .gitignore");
    }
}

/// <remarks>Reference source: dotenvx/src/cli/actions/ext/gitignore.js's Docker class.</remarks>
public class GitignoreDocker(string[]? patterns = null)
{
    public GitignoreGenericResult Run()
    {
        return new GitignoreGeneric(".dockerignore", patterns).Run("add to .dockerignore (if exists)");
    }
}

/// <remarks>Reference source: dotenvx/src/cli/actions/ext/gitignore.js's Npm class.</remarks>
public class GitignoreNpm(string[]? patterns = null)
{
    public GitignoreGenericResult Run()
    {
        return new GitignoreGeneric(".npmignore", patterns).Run("add to .npmignore (if existing)");
    }
}

/// <remarks>Reference source: dotenvx/src/cli/actions/ext/gitignore.js's Vercel class.</remarks>
public class GitignoreVercel(string[]? patterns = null)
{
    public GitignoreGenericResult Run()
    {
        return new GitignoreGeneric(".vercelignore", patterns).Run("add to .vercelignore (if existing)");
    }
}
