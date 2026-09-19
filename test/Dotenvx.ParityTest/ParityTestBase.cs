using System.Text.RegularExpressions;

namespace Dotenvx.ParityTest;

/// <remarks>
/// Shared per-test scratch directory plus the "run identical fixtures through both CLIs" pattern
/// used by every parity test. A fresh WorkDir is created per test method (xUnit constructs a new
/// instance per [Fact]) so tests never see each other's files.
/// </remarks>
[Collection(NodeParityCollection.Name)]
public abstract class ParityTestBase : IDisposable
{
    protected readonly NodeParityFixture Fixture;
    protected readonly string WorkDir;

    protected ParityTestBase(NodeParityFixture fixture)
    {
        Fixture = fixture;
        WorkDir = Path.Combine(Path.GetTempPath(), "dotenvx-parity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(WorkDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(WorkDir, true);
        }
        catch
        {
            // best-effort cleanup of a scratch temp dir
        }
    }

    /// <summary>Returns false (and logs why) if this suite isn't usable in the current environment.</summary>
    protected bool EnsureAvailable()
    {
        if (Fixture.IsAvailable)
        {
            return true;
        }

        Console.WriteLine($"SKIPPED (Node parity unavailable): {Fixture.UnavailableReason}");
        return false;
    }

    protected void WriteFile(string relativePath, string content)
    {
        string fullPath = Path.Combine(WorkDir, relativePath);
        string? dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(fullPath, content);
    }

    protected string ReadFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(WorkDir, relativePath));
    }

    /// <summary>
    /// Runs setupFixtures, invokes the real Node CLI, then re-runs setupFixtures (so the .NET run
    /// starts from the exact same on-disk state) and invokes our built CLI, with identical args/env.
    /// </summary>
    protected async Task<(CliResult Node, CliResult Dotnet)> RunBothAsync(Action setupFixtures,
        IReadOnlyDictionary<string, string>? env, params string[] args)
    {
        setupFixtures();
        CliResult node = await Fixture.RunNodeAsync(WorkDir, env, args);

        setupFixtures();
        CliResult dotnet = await Fixture.RunDotnetAsync(WorkDir, env, args);

        return (node, dotnet);
    }

    private static readonly Regex HexKeyRegex = new(@"\b[0-9a-f]{40,}\b", RegexOptions.Compiled);
    private static readonly Regex EncryptedValueRegex = new(@"encrypted:[A-Za-z0-9+/=]+", RegexOptions.Compiled);

    /// <summary>
    /// Replaces freshly-generated key material and ciphertext (different every run, by design)
    /// with a fixed placeholder so structurally-identical output can still be compared byte-for-byte.
    /// </summary>
    protected static string RedactKeyMaterial(string text)
    {
        text = EncryptedValueRegex.Replace(text, "encrypted:REDACTED");
        text = HexKeyRegex.Replace(text, "REDACTED");
        return text;
    }
}
