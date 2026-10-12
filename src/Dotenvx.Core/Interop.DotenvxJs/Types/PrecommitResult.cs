namespace Dotenvx.Interop.DotenvxJs.Types;

public class PrecommitResult
{
    public required string SuccessMessage { get; init; }
    public required CustomException[] Warnings { get; init; }
}
