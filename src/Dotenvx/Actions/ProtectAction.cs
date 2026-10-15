using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Dotenvx.Helpers;
using Dotenvx.Interop.DotenvxJs.Events;
using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Services;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;

using ParseResult = System.CommandLine.ParseResult;

namespace Dotenvx.Actions;

/// <remarks>
/// Reference source: dotenvx/src/cli/actions/{protect,protectDocker,protectStdin,protectProcess}.js (hidden `protect`).
/// Accepted difference: on a terminal upstream asks which protections to set (enquirer checklist); this port takes the
/// defaults it pre-selects (both). Upstream's event tracking wrapper is not ported.
/// </remarks>
public class ProtectAction : Command
{
    private readonly Argument<string?> _directoryArgument =
        new("directory") { Arity = ArgumentArity.ZeroOrOne, Description = "directory to check with --docker (default: \".\")" };

    private readonly Option<bool> _dockerOption =
        new("--docker") { Description = "check env files are encrypted or dockerignored" };

    private readonly Option<string?> _gitFileOption =
        new("--git-file") { Description = "check stdin contents for the given Git pathname", HelpName = "pathname", Hidden = true };

    private readonly Option<bool> _gitProcessOption =
        new("--git-process") { Description = "run the Git protection filter protocol", Hidden = true };

    private readonly IDotenvxLogger _logger;

    public ProtectAction(IDotenvxLogger logger) : base("protect", "protect secrets and private keys from code commits")
    {
        _logger = logger;
        Hidden = true;
        Add(_directoryArgument);
        Add(_dockerOption);
        Add(_gitFileOption);
        Add(_gitProcessOption);
        SetAction(Execute);
        EventTracking.Track(this, "protect");
    }

    private int Execute(ParseResult parseResult)
    {
        bool docker = parseResult.GetValue(_dockerOption);
        string? gitFile = parseResult.GetValue(_gitFileOption);
        bool gitProcess = parseResult.GetValue(_gitProcessOption);
        if (new[] { docker, gitFile is not null, gitProcess }.Count(flag => flag) > 1)
        {
            Console.Error.WriteLine(docker && gitFile is not null
                ? "error: option '--docker' cannot be used with option '--git-file <pathname>'"
                : docker
                    ? "error: option '--docker' cannot be used with option '--git-process'"
                    : "error: option '--git-file <pathname>' cannot be used with option '--git-process'");

            return 1;
        }

        string directory = parseResult.GetValue(_directoryArgument) ?? ".";
        if (docker)
        {
            return ProtectDocker(directory);
        }

        if (gitProcess)
        {
            return new ProtectGit(_logger).Process(parseResult);
        }

        if (gitFile is not null)
        {
            return new ProtectGit(_logger).Stdin(parseResult, gitFile);
        }

        SpinnerScope? spinner = null;
        try
        {
            bool filter = true;
            bool ignore = false;
            Protect.ProtectState? current = null;
            bool interactive = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) && SecretCli.IsInteractive();
            if (interactive)
            {
                current = Protect.State();
                Protect.ProtectState state = current;
                IReadOnlyList<string> protections = Prompts.Multiselect(new Prompts.MultiselectRequest("Set protections",
                [
                    new Prompts.Choice("Protect plaintext secrets from code commits (.env*)", "filter"),
                    new Prompts.Choice("Protect private keys from code commits (.env.keys*)", "ignore")
                ])
                {
                    Initial = ["filter", "ignore"],
                    SubmitLabelFunction = selected =>
                    {
                        if (state.Configured && new[] { "filter", "ignore" }.All(key => selected.Contains(key) == (key == "filter" ? state.Filter : state.Ignore)))
                        {
                            return "Done";
                        }

                        return selected.Count == 0 ? "Remove protections" : state.Configured ? "Apply changes" : "Install protections";
                    }
                }) ?? [];
                filter = protections.Contains("filter");
                ignore = protections.Contains("ignore");
            }

            spinner = SpinnerScope.Start(parseResult, filter || ignore ? "protecting" : "unprotecting");

            if (interactive)
            {
                // Check ownership-sensitive removal before changing the filter.
                if (!ignore && current!.Ignore)
                {
                    Protect.RemoveIgnore();
                }

                if (!filter && current!.Filter)
                {
                    Protect.RemoveFilter();
                }
            }

            List<string> protectedFiles = [];
            string? hookWarning = null;
            if (filter)
            {
                Protect.InstallProtectFilter();
                Protect.RemoveLegacyProtectFilter();
                hookWarning = Protect.UninstallPrecommitHook().Warning;
                protectedFiles.Add(".env*");
            }

            if (ignore)
            {
                if (current is null || !current.Ignore)
                {
                    Protect.InstallProtectIgnore();
                }

                protectedFiles.Add(".env.keys*");
            }

            if (interactive)
            {
                Protect.MarkConfigured();
            }

            spinner.Dispose();
            if (hookWarning is not null)
            {
                _logger.Warn(hookWarning);
            }

            string status = protectedFiles.Count == 2 ? "full" : "partial";
            _logger.Success(protectedFiles.Count > 0
                ? $"⛉ protection: {status} ({string.Join(", ", protectedFiles)})"
                : "⛉ protection: none");

            return 0;
        }
        catch (Exception error)
        {
            spinner?.Dispose();
            CatchAndLog(_logger, error);
            EventScope.Fail(error);

            return 1;
        }
    }

    private int ProtectDocker(string directory)
    {
        _logger.Debug($"directory: {directory}");
        _logger.Debug($"options: {JsonStringify(new { docker = true })}");

        try
        {
            PrebuildResult result = Prebuild.Factory(directory).Run();
            foreach (CustomException warning in result.Warnings)
            {
                _logger.Warn(warning.MessageWithHelp);
            }

            _logger.Success(result.SuccessMessage);

            return 0;
        }
        catch (Exception error)
        {
            CatchAndLog(_logger, error);
            EventScope.Fail(error);

            return 1;
        }
    }
}

/// <remarks>Reference source: dotenvx/src/cli/actions/{protectStdin,protectProcess}.js (the Git clean filter and its v2 process protocol).</remarks>
internal sealed class ProtectGit(IDotenvxLogger logger)
{
    private static bool Exempt(string filepath)
    {
        return Path.GetFileName(filepath.Replace('\\', '/')) is ".env.example" or ".env.vault" or ".env.x" ||
               filepath.Replace('\\', '/').Split('/')[^1] is ".env.example" or ".env.vault" or ".env.x";
    }

    private static string FixMessage(string filepath, bool privateKeyFile)
    {
        if (filepath.Any(c => c < 32 || c == 127))
        {
            return privateKeyFile ? "fix: add this private-key file to .gitignore" : "fix: encrypt this env file or add it to .gitignore";
        }

        string argument = Regex.IsMatch(filepath, "^[a-zA-Z0-9_./-]+$") ? filepath : "'" + filepath.Replace("'", "'\\''") + "'";

        return privateKeyFile ? $"fix: run [dotenvx gitignore --pattern {argument}]" : $"fix: run [dotenvx encrypt -f {argument}]";
    }

    private bool Check(string filepath, byte[] content, Action beforeError)
    {
        string filename = filepath.Replace('\\', '/').Split('/')[^1];
        bool privateKeyFile = filename.StartsWith(".env.keys", StringComparison.Ordinal);
        string text = Encoding.UTF8.GetString(content);
        List<string>? rejected = null;
        if (!privateKeyFile)
        {
            try
            {
                rejected = Protect.ProtectEnvfile(filepath, text);
            }
            catch (Exception error)
            {
                beforeError();
                logger.Error(error.Message);

                return false;
            }
        }

        bool plaintext = rejected is null ? !Exempt(filepath) && Protect.PlaintextKeys(text).Count > 0 : rejected.Count > 0;
        if (privateKeyFile || plaintext)
        {
            string code = privateKeyFile ? "PRIVATE_KEY_FILE" : "PLAINTEXT_ENV";
            string fix = FixMessage(filepath, privateKeyFile);
            string message = privateKeyFile
                ? $"refusing to stage {JsonStringify(filepath)}"
                : rejected is { Count: > 0 }
                    ? $"{string.Join(", ", rejected)} not encrypted ({JsonStringify(filepath)})"
                    : $"{JsonStringify(filepath)} contains plaintext secrets";
            beforeError();
            logger.Error($"[{code}] {message}. {fix}");

            return false;
        }

        return true;
    }


    private void LogProtected(IEnumerable<string> files)
    {
        string[] names = [.. files.Distinct()];
        if (names.Length == 0)
        {
            return;
        }

        List<string> display = [.. names.Take(3).Select(name => Regex.IsMatch(name, @"^[\w./-]+$") ? name : JsonStringify(name))];
        if (names.Length > 3)
        {
            display.Add($"+{names.Length - 3} more");
        }

        logger.Success($"⛉ safe to commit ({string.Join(", ", display)})");
    }

    // createProtectSpinner.js: Git pipes stdin/stdout; only stderr can tell us whether a user is watching.
    private static SpinnerScope? ProtectSpinner(ParseResult parseResult)
    {
        if (Console.IsErrorRedirected || Environment.GetEnvironmentVariable("CI") is not null ||
            Environment.GetEnvironmentVariable("TERM") == "dumb")
        {
            return null;
        }

        return SpinnerScope.Start(parseResult, "dotenvx protecting...", ["⛉"]);
    }

    public int Stdin(ParseResult parseResult, string filepath)
    {
        SpinnerScope? spinner = null;
        void Stop()
        {
            spinner?.Dispose();
        }

        try
        {
            spinner = ProtectSpinner(parseResult);
            using MemoryStream buffer = new();
            using (Stream input = Console.OpenStandardInput())
            {
                input.CopyTo(buffer);
            }

            byte[] content = buffer.ToArray();
            if (!Check(filepath, content, Stop))
            {
                return 1;
            }

            // stdout is Git's blob content: no logging, normalization, or extra newline.
            using (Stream output = Console.OpenStandardOutput())
            {
                output.Write(content, 0, content.Length);
                output.Flush();
            }

            Stop();
            if (spinner is not null && !Exempt(filepath))
            {
                LogProtected([filepath]);
            }

            return 0;
        }
        catch (Exception error)
        {
            Stop();
            logger.Error(error.Message);
            EventScope.Fail(error);

            return 1;
        }
        finally
        {
            Stop();
        }
    }

    // ---- pkt-line process protocol ---------------------------------------------------------------------------------------

    private sealed class PacketReader(Stream input)
    {
        private readonly List<byte> _buffer = [];
        private bool _done;

        /// <summary>The next packet payload; null for a flush packet; <see cref="Done" /> at end of input.</summary>
        public bool Next(out byte[]? packet)
        {
            packet = null;
            while (true)
            {
                if (_buffer.Count >= 4)
                {
                    string header = Encoding.ASCII.GetString([.. _buffer.Take(4)]);
                    if (!Regex.IsMatch(header, "^[0-9a-fA-F]{4}$"))
                    {
                        throw new Exception("Invalid Git filter packet");
                    }

                    int length = Convert.ToInt32(header, 16);
                    if (length != 0 && (length < 4 || length > 65520))
                    {
                        throw new Exception("Invalid Git filter packet length");
                    }

                    if (_buffer.Count >= (length == 0 ? 4 : length))
                    {
                        packet = length == 0 ? null : [.. _buffer.Skip(4).Take(length - 4)];
                        _buffer.RemoveRange(0, length == 0 ? 4 : length);

                        return true;
                    }
                }

                if (_done)
                {
                    if (_buffer.Count > 0)
                    {
                        throw new Exception("Incomplete Git filter packet");
                    }

                    return false;
                }

                byte[] chunk = new byte[65536];
                int read = input.Read(chunk, 0, chunk.Length);
                if (read == 0)
                {
                    _done = true;
                }
                else
                {
                    _buffer.AddRange(chunk.Take(read));
                }
            }
        }
    }

    public int Process(ParseResult parseResult)
    {
        SpinnerScope? spinner = null;
        bool started = false;
        bool failed = false;
        bool activeRequest = false;
        bool reported = false;
        HashSet<string> checkedFiles = [];
        void Stop()
        {
            spinner?.Dispose();
        }

        void Finish()
        {
            Stop();
            if (spinner is not null && !failed && !activeRequest && !reported)
            {
                reported = true;
                LogProtected(checkedFiles);
            }
        }

        using Stream stdin = Console.OpenStandardInput();
        using Stream stdout = Console.OpenStandardOutput();
        PacketReader reader = new(stdin);
        void Write(byte[] data)
        {
            stdout.Write(data, 0, data.Length);
            stdout.Flush();
        }

        void Packet(byte[]? data)
        {
            if (data is null)
            {
                Write(Encoding.ASCII.GetBytes("0000"));

                return;
            }

            Write(Encoding.ASCII.GetBytes((data.Length + 4).ToString("x4")));
            Write(data);
        }

        void PacketText(string text)
        {
            Packet(Encoding.UTF8.GetBytes(text));
        }

        List<byte[]>? List(bool allowEof = false)
        {
            List<byte[]> result = [];
            while (true)
            {
                if (!reader.Next(out byte[]? item))
                {
                    if (allowEof && result.Count == 0)
                    {
                        return null;
                    }

                    throw new Exception("Incomplete Git filter request");
                }

                if (item is null)
                {
                    return result;
                }

                result.Add(item);
            }
        }

        List<string> Lines(IEnumerable<byte[]> entries)
        {
            return [.. entries.Select(entry => Regex.Replace(Encoding.UTF8.GetString(entry), "\n$", ""))];
        }

        try
        {
            List<string> hello = Lines(List()!);
            if (hello.Count == 0 || hello[0] != "git-filter-client" || !hello.Contains("version=2"))
            {
                throw new Exception("Unsupported Git filter handshake");
            }

            PacketText("git-filter-server\n");
            PacketText("version=2\n");
            Packet(null);
            List<string> capabilities = Lines(List()!);
            if (!capabilities.Contains("capability=clean"))
            {
                throw new Exception("Git filter requires clean capability");
            }

            PacketText("capability=clean\n");
            bool smudge = capabilities.Contains("capability=smudge");
            if (smudge)
            {
                PacketText("capability=smudge\n");
            }

            Packet(null);
            while (true)
            {
                List<byte[]>? headers = List(true);
                if (headers is null)
                {
                    return 0;
                }

                activeRequest = true;
                Dictionary<string, string> fields = [];
                foreach (string line in Lines(headers))
                {
                    int index = line.IndexOf('=');
                    fields[line[..index]] = line[(index + 1)..];
                }

                fields.TryGetValue("command", out string? command);
                if ((command != "clean" && !(smudge && command == "smudge")) || !fields.ContainsKey("pathname"))
                {
                    throw new Exception("Invalid Git filter request");
                }

                if (command == "clean" && !started)
                {
                    started = true;
                    spinner = ProtectSpinner(parseResult);
                }

                byte[] content = [.. List()!.SelectMany(part => part)];
                // Checkout must remain a byte-for-byte passthrough, even for existing plaintext history.
                if (command == "clean" && !Check(fields["pathname"], content, Stop))
                {
                    failed = true;
                    PacketText("status=error\n");
                    Packet(null);
                    activeRequest = false;

                    continue;
                }

                PacketText("status=success\n");
                Packet(null);
                for (int offset = 0; offset < content.Length; offset += 65516)
                {
                    Packet(content[offset..Math.Min(content.Length, offset + 65516)]);
                }

                Packet(null);
                Packet(null);
                if (command == "clean" && !Exempt(fields["pathname"]))
                {
                    checkedFiles.Add(fields["pathname"]);
                }

                activeRequest = false;
            }
        }
        catch (Exception)
        {
            failed = true;
            Stop();
            // Never include malformed protocol data: it may contain secret contents.
            logger.Error("Git protection filter protocol failed");
            EventScope.Fail("GIT_FILTER_PROTOCOL_FAILED");

            return 1;
        }
        finally
        {
            Finish();
        }
    }
}
