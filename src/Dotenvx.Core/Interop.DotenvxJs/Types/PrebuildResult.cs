namespace Dotenvx.Interop.DotenvxJs.Types;

public class PrebuildResult
{
    public required string SuccessMessage { get; init; }
    public required CustomException[] Warnings { get; init; }
}
