namespace Dotenvx.Core.Interop.DotenvxJs.Types;

public class EnvItem
{
    public required EnvType Type { get; init; }
    public required string Value { get; init; }
}