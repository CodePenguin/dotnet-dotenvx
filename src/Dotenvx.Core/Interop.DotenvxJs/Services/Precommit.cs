using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;
using Dotenvx.Interop.IgnoreJs;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/services/precommit.js. The exempt-file check
/// (.env.example/.env.vault/.env.x) compares the BASENAME, not the full path (upstream's own code
/// comment: "exemptions match on filename, so they apply at any depth") - the opposite of
/// Prebuild's full-path check (see Prebuild.cs's doc comment). Also gates the encrypted/gitignored
/// check behind git awareness (_isFileToBeCommitted, via GitHelper's swappable seam): a file not
/// actually staged/changed isn't enforced, defaulting to "always check" (true) whenever git itself
/// isn't available or errors, matching upstream's two independent defensive-default paths exactly.
/// </remarks>
public class Precommit(string directory = "./", bool install = false)
{
    private static readonly Func<string, bool, Precommit> DefaultFactory =
        (directory, install) => new Precommit(directory, install);

    private static readonly string[] ExcludeEnvFile =
        ["test/**", "tests/**", "spec/**", "specs/**", "pytest/**", "test_suite/**"];

    protected Precommit() : this("./")
    {
        // For unit tests
    }

    public static Func<string, bool, Precommit> Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public virtual PrecommitResult Run()
    {
        if (install)
        {
            InstallPrecommitHookResult installResult = InstallPrecommitHook();
            return new PrecommitResult { SuccessMessage = installResult.SuccessMessage, Warnings = [] };
        }

        int count = 0;
        List<CustomException> warnings = [];
        string gitignore = ".env.keys";

        if (!Fsx.ExistsSync(".gitignore"))
        {
            warnings.Add(new CustomException(".gitignore missing", "fix: [touch .gitignore]"));
        }
        else
        {
            gitignore = Fsx.ReadFileX(".gitignore");
        }

        Ignore ig = new Ignore().Add(gitignore);
        string[] dotenvFiles = FilePaths();

        foreach (string rawFile in dotenvFiles)
        {
            count++;
            string file = NodePathJoin(directory, rawFile);
            string filename = NodePath.Basename(file);

            if (!IsFileToBeCommitted(file))
            {
                continue;
            }

            if (ig.Ignores(file))
            {
                if (filename is ".env.example" or ".env.vault" or ".env.x")
                {
                    warnings.Add(new CustomException($"{file} ignored (should not be)",
                        $"fix: [dotenvx gitignore --pattern !{file}]"));
                }
            }
            else if (filename is not (".env.example" or ".env.vault" or ".env.x"))
            {
                string src = Fsx.ReadFileX(file);
                bool encrypted = Sealed(src);
                if (!encrypted)
                {
                    string errorMsg = $"{file} not encrypted/gitignored";
                    string errorHelp = $"fix: [dotenvx encrypt -f {file}] or [dotenvx gitignore --pattern {file}]";
                    if (file.Contains(".env.keys"))
                    {
                        errorMsg = $"{file} not gitignored";
                        errorHelp = $"fix: [dotenvx gitignore --pattern {file}]";
                    }

                    throw new CustomException(errorMsg, errorHelp);
                }
            }
        }

        string successMessage = count == 0 ? "▣ no .env files" : $"▣ encrypted/gitignored ({count})";
        if (warnings.Count > 0)
        {
            successMessage += $" with warnings ({warnings.Count})";
        }

        return new PrecommitResult { SuccessMessage = successMessage, Warnings = [.. warnings] };
    }

    private string[] FilePaths()
    {
        return Ls.Factory(directory, null, ExcludeEnvFile).Run();
    }

    private static bool IsInGitRepo()
    {
        return GitHelper.IsInsideGitWorkTree();
    }

    private static bool IsFileToBeCommitted(string filePath)
    {
        try
        {
            if (!IsInGitRepo())
            {
                return true; // consider file to be committed if there is an error (not a git repo)
            }

            string[] files = GitHelper.DiffNameOnlyNulSeparated();
            return files.Contains(filePath);
        }
        catch
        {
            return true; // consider file to be committed if there is an error (not using git)
        }
    }

    private InstallPrecommitHookResult InstallPrecommitHook()
    {
        return new Helpers.InstallPrecommitHook().Run();
    }
}
