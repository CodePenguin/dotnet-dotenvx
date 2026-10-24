using Dotenvx.Interop.DotenvxJs.Helpers;
using Dotenvx.Interop.DotenvxJs.Types;

using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;
using static Dotenvx.Primitives.Primitives;

namespace Dotenvx.Interop.DotenvxJs.Services;

/// <remarks>
/// Reference source: dotenvx/src/lib/services/genexample.js. Upstream's constructor accepts
/// envFile as either a bare string or an array (JS duck-typing) and wraps a bare string in
/// _envFilepaths() - this port's constructor always takes string[]? since that's the only shape
/// GenexampleAction's -f/--env-file option (or a direct caller) can produce in a statically-typed
/// language; there's no bare-string entry point to wrap, so no equivalent of upstream's
/// "#run (string envFile)" test scenario exists here.
/// </remarks>
internal class Genexample(string directory = ".", string[]? envFile = null)
{
    private static readonly Func<string, string[]?, Genexample> DefaultFactory =
        (directory, envFile) => new Genexample(directory, envFile);

    private const string ExampleFilename = ".env.example";

    private readonly string[] _envFile = envFile ?? FindEnvFiles(directory);

    protected Genexample() : this(".")
    {
        // For unit tests
    }

    public static Func<string, string[]?, Genexample> Factory { get; internal set; } = DefaultFactory;

    internal static void ResetFactory()
    {
        Factory = DefaultFactory;
    }

    public string ExampleFilepath => NodePath.Resolve(Path.Combine(directory, ExampleFilename));

    public virtual GenexampleResult Run()
    {
        if (_envFile.Length < 1)
        {
            throw new MissingEnvFilesException();
        }

        HashSet<string> keys = [];
        HashSet<string> addedKeys = [];
        Dictionary<string, string> injected = [];
        Dictionary<string, string> existed = [];

        string exampleSrc = $"# {ExampleFilename} - generated with dotenvx\n";

        foreach (string envFilepath in _envFile)
        {
            string filepath = NodePath.Resolve(Path.Combine(directory, envFilepath));
            if (!Fsx.ExistsSync(filepath))
            {
                // genexample.js sets a custom error.help AFTER errors.js's missingEnvFile()
                // factory already baked error.messageWithHelp using the GENERIC help text -
                // messageWithHelp is a plain already-computed string there, not a getter, so the
                // custom help is dead code and never actually surfaces (confirmed live: real Node
                // still prints the generic `fix: [.../484]` URL here, not the custom text).
                throw new MissingEnvFileException(envFilepath);
            }

            string src = Fsx.ReadFileX(filepath);
            Dictionary<string, object> parsed = ScanToDictionary(src);
            foreach (string key in parsed.Keys)
            {
                keys.Add(key);
                src = Upsert(src, key, "");
            }

            exampleSrc += $"\n{src}";
        }

        if (!Fsx.ExistsSync(ExampleFilepath))
        {
            foreach (string key in keys)
            {
                addedKeys.Add(key);
                injected[key] = "";
            }
        }
        else
        {
            exampleSrc = Fsx.ReadFileX(ExampleFilepath);
            Dictionary<string, object> parsed = ScanToDictionary(exampleSrc);
            foreach (string key in keys)
            {
                if (parsed.TryGetValue(key, out object? value))
                {
                    existed[key] = (string)value;
                }
                else
                {
                    exampleSrc += $"{key}=''\n";
                    addedKeys.Add(key);
                    injected[key] = "";
                }
            }
        }

        return new GenexampleResult
        {
            EnvExampleFile = exampleSrc,
            EnvFile = _envFile,
            ExampleFilepath = ExampleFilepath,
            AddedKeys = [.. addedKeys],
            Injected = injected,
            Existed = existed
        };
    }
}
