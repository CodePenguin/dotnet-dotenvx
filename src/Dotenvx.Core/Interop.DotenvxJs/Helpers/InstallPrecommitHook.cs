using Dotenvx.Interop.DotenvxJs.Types;

namespace Dotenvx.Interop.DotenvxJs.Helpers;

/// <remarks>
/// Reference source: dotenvx/src/lib/helpers/installPrecommitHook.js. No Factory-swap pattern -
/// mirrors upstream's own direct-instantiation + Fsx-stub test style (InstallPrecommitHookTest.cs),
/// matching how DelTest.cs/EncryptTest.cs test their real classes directly. HookScript captured
/// byte-for-byte from the real installed JS module (`node -e` stubbing fsx.writeFileXSync to
/// capture what it's actually called with, rather than hand-transcribing the JS template literal's
/// escape sequences) - notably the embedded raw newline mid-printf-argument (before `" "$RED"...`)
/// is real, not a transcription error: the JS template literal's trailing `\n` there IS processed
/// as an actual newline character (unlike the earlier `\\033` escapes, which are doubled in source
/// specifically so the shell interprets them later, not JS at template-literal-evaluation time).
/// </remarks>
public class InstallPrecommitHook
{
    private const string HookScript =
        "#!/bin/sh\n" +
        "\n" +
        "if command -v dotenvx 2>&1 >/dev/null\n" +
        "then\n" +
        "  dotenvx precommit\n" +
        "elif npx dotenvx -V >/dev/null 2>&1\n" +
        "then\n" +
        "  npx dotenvx precommit\n" +
        "else\n" +
        "  if [ -t 2 ]; then\n" +
        "    RED=\"$(printf '\\033[31m')\"\n" +
        "    RESET=\"$(printf '\\033[0m')\"\n" +
        "  else\n" +
        "    RED=\"\"\n" +
        "    RESET=\"\"\n" +
        "  fi\n" +
        "\n" +
        "  printf \"%s☠ 'dotenvx precommit' command not found (.git/hooks/precommit) fix: [curl -sfS https://dotenvx.sh | sh]%s\n" +
        "\" \"$RED\" \"$RESET\" >&2\n" +
        "  exit 1\n" +
        "fi\n";

    public string HookPath { get; } = Path.Combine(".git", "hooks", "pre-commit");

    public InstallPrecommitHookResult Run()
    {
        try
        {
            string successMessage;
            if (Exists())
            {
                string currentHook = CurrentHook();
                if (currentHook.Contains("dotenvx precommit") || currentHook.Contains("dotenvx ext precommit"))
                {
                    successMessage = $"▣ dotenvx precommit exists [{HookPath}]";
                }
                else
                {
                    AppendHook();
                    successMessage = $"▣ dotenvx precommit appended [{HookPath}]";
                }
            }
            else
            {
                CreateHook();
                successMessage = $"▣ dotenvx precommit installed [{HookPath}]";
            }

            return new InstallPrecommitHookResult { SuccessMessage = successMessage };
        }
        catch (Exception err)
        {
            // Node reports the errno text: ENOENT: no such file or directory, open '<path>'
            throw new PrecommitHookModifyFailedException(err is FileNotFoundException or DirectoryNotFoundException
                ? $"ENOENT: no such file or directory, open '{Path.GetFullPath(HookPath)}'"
                : err.Message);
        }
    }

    internal bool Exists()
    {
        return Fsx.ExistsSync(HookPath);
    }

    internal string CurrentHook()
    {
        return Fsx.ReadFileX(HookPath);
    }

    private void CreateHook()
    {
        Fsx.WriteFileX(HookPath, HookScript);
        Fsx.ChmodSync(HookPath, "755");
    }

    private void AppendHook()
    {
        Fsx.AppendFileSync(HookPath, $"\n{HookScript}");
    }
}
