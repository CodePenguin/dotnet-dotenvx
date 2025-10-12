namespace Dotenvx.Core.Interop.Dotenvx.Types;

public class EnvItem
{
    public required EnvType Type { get; init; }
    public required string Value { get; init; }
}