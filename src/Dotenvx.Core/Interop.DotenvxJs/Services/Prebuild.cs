using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Interop.IgnoreJs;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/services/prebuild.js. The exempt-file check
/// (.env.example/.env.vault/.env.x) compares the FULL joined path, not just the basename - nested
/// exempt files are NOT exempt here, the opposite of Precommit (see Precommit.cs's doc comment) -
/// confirmed via a dedicated upstream test proving this asymmetry is deliberate, not a bug to fix.
/// </remarks>
internal class Prebuild(string directory = "./")
{
    private static readonly Func<string, Prebuild> DefaultFactory = directory => new Prebuild(directory);

    private static readonly string[] ExcludeEnvFile =
        ["test/**", "tests/**", "spec/**", "specs/**", "pytest/**", "test_suite/**"];

    protected Prebuild() : this("./")
    {
        // For unit tests
    }

    public static Func<string, Prebuild> Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public virtual PrebuildResult Run()
    {
        int count = 0;
        List<CustomException> warnings = [];
        string dockerignore = ".env.keys";

        if (!Fsx.ExistsSync(".dockerignore"))
        {
            warnings.Add(new CustomException(".dockerignore missing", "fix: [touch .dockerignore]"));
        }
        else
        {
            dockerignore = Fsx.ReadFileX(".dockerignore");
        }

        Ignore ig = new Ignore().Add(dockerignore);
        string[] dotenvFiles = FilePaths();

        foreach (string rawFile in dotenvFiles)
        {
            count++;
            string file = NodePathJoin(directory, rawFile);

            if (ig.Ignores(file))
            {
                if (file is ".env.example" or ".env.vault" or ".env.x")
                {
                    warnings.Add(new CustomException($"{file} ignored (should not be)",
                        $"fix: [dotenvx gitignore --pattern !{file}]"));
                }
            }
            else if (file is not (".env.example" or ".env.vault" or ".env.x"))
            {
                string src = Fsx.ReadFileX(file);
                bool privateKeyFile = Path.GetFileName(file).StartsWith(".env.keys", StringComparison.Ordinal);

                // hasPlaintextSecrets(src) is `!sealed(src)`
                if (privateKeyFile || !Sealed(src))
                {
                    string errorMsg = $"{file} not encrypted/dockerignored";
                    string errorHelp = $"fix: [dotenvx encrypt -f {file}] or [dotenvx gitignore --pattern {file}]";
                    if (privateKeyFile)
                    {
                        errorMsg = $"{file} not dockerignored";
                        errorHelp = $"fix: [dotenvx gitignore --pattern {file}]";
                    }

                    throw new CustomException(errorMsg, errorHelp);
                }
            }
        }

        string successMessage = count == 0 ? "▣ no .env files" : $"▣ encrypted/dockerignored ({count})";
        if (warnings.Count > 0)
        {
            successMessage += $" with warnings ({warnings.Count})";
        }

        return new PrebuildResult { SuccessMessage = successMessage, Warnings = [.. warnings] };
    }

    private string[] FilePaths()
    {
        // Uses Ls.Factory (not `new Ls(...)`) so tests can mock the file list the same way every
        // other service composition in this codebase is mocked, without a real filesystem walk.
        return Ls.Factory(directory, null, ExcludeEnvFile).Run();
    }
}
