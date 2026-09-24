namespace Dotenvx.Interop.DotenvxJs.Types;

public class GitignoreGenericResult
{
    public required string Filename { get; init; }
    public required string[] Patterns { get; init; }
    public required bool Changed { get; init; }

    /// <remarks>
    /// True when the file didn't exist and touchFile was false (gitignore.js's Generic.run()
    /// returns immediately with NO log call at all in this case - not even "no change") - the
    /// .npmignore/.vercelignore case when those files aren't already present. The Action layer
    /// must skip logging entirely when this is true, not fall through to a "no change" line.
    /// </remarks>
    public required bool Skipped { get; init; }

    /// <remarks>
    /// The Git/Docker/Npm/Vercel-specific "add to X" line each logs via logger.verbose before
    /// running - carried on the result rather than logged inside the wrapper classes themselves,
    /// since this port's services return data and only the Action layer logs (see Gitignore.cs).
    /// </remarks>
    public required string VerboseMessage { get; init; }
}
