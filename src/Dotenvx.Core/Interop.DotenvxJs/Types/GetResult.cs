namespace Dotenvx.Core.Interop.DotenvxJs.Types;

public class GetResult
{
    public required IDictionary<string, string> Parsed { get; set; }
    public required IList<Exception> Errors { get; set; }
}