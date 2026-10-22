namespace Dotenvx.Interop.DotenvxJs.Types;

internal class PrebuildResult
{
    public required string SuccessMessage { get; init; }
    public required CustomException[] Warnings { get; init; }
}
