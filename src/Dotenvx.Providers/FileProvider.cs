using Dotenvx.Primitives;

namespace Dotenvx.Providers;

/// <remarks>
/// Reference source: @dotenvx/providers file.js. Looks in the key files (`fk`, default `.env.keys` when not given; an empty
/// list reads nothing; relative to `cwd`) for a `DOTENV_PRIVATE_KEY*` value (comma-separated lists allowed) whose public key
/// is the one asked for. Unreadable files and invalid entries are skipped.
/// </remarks>
public sealed class FileProvider(ProviderOptions options) : IKeyProvider
{
    private IEnumerable<string> Filepaths()
    {
        IReadOnlyList<string> paths = options.Fk ?? [".env.keys"];

        return paths.Select(path => Path.GetFullPath(Path.Combine(options.Cwd ?? ".", path)));
    }

    public Task<IDictionary<string, string>> Get(string publicKey)
    {
        return Task.FromResult(GetSync(publicKey));
    }

    public IDictionary<string, string> GetSync(string publicKey)
    {
        foreach (string filepath in Filepaths())
        {
            string source;
            try
            {
                source = File.ReadAllText(filepath);
            }
            catch (Exception)
            {
                // unavailable files allow fallback
                continue;
            }

            IDictionary<string, string> ring = MatchingKey(source, publicKey);
            if (ring.ContainsKey(publicKey) && ring[publicKey] != "")
            {
                return ring;
            }
        }

        return ProviderErrors.Empty();
    }

    private static IDictionary<string, string> MatchingKey(string source, string publicKey)
    {
        // Object.keys order of scan().parsed: names by first appearance, each with all of its values
        List<string> names = [];
        Dictionary<string, List<string>> values = [];
        foreach (ScanEntry entry in Primitives.Primitives.Scan(source))
        {
            if (!values.TryGetValue(entry.Key, out List<string>? list))
            {
                names.Add(entry.Key);
                values[entry.Key] = list = [];
            }

            list.Add(entry.Value);
        }

        foreach (string name in names)
        {
            if (!name.StartsWith("DOTENV_PRIVATE_KEY", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (string value in values[name])
            {
                foreach (string privateKey in value.Split(','))
                {
                    try
                    {
                        if (Primitives.Primitives.Derive(privateKey) == publicKey)
                        {
                            return new Dictionary<string, string> { [publicKey] = privateKey };
                        }
                    }
                    catch (Exception)
                    {
                        // invalid entries must not prevent finding another valid key
                    }
                }
            }
        }

        return ProviderErrors.Empty();
    }
}
