namespace Dotenvx.Interop.DotenvxJs.Types;

internal class GetResult
{
    public required IReadOnlyDictionary<string, string> Parsed { get; init; }
    public required Exception[] Errors { get; init; }

    /// <summary>Where each public key's private key came from (`includeKeySources`): null when not asked for.</summary>
    public IReadOnlyDictionary<string, string>? KeySources { get; init; }
}
