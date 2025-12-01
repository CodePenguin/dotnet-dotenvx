namespace Dotenvx.Interop.DotenvxJs.Types;

public class OpsLib
{
    public string Status { get; init; } = "";
    public Action<string> Observe { get; init; } = _ => { };
}