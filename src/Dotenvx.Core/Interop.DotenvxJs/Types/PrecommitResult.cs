namespace Dotenvx.Interop.DotenvxJs.Types;

internal class PrecommitResult
{
    public required string SuccessMessage { get; init; }
    public required CustomException[] Warnings { get; init; }
}
