namespace Dotenvx.Interop.DotenvxJs.Types;

internal class GenexampleResult
{
    public required string EnvExampleFile { get; init; }
    public required string[] EnvFile { get; init; }
    public required string ExampleFilepath { get; init; }
    public required string[] AddedKeys { get; init; }
    public required Dictionary<string, string> Injected { get; init; }
    public required Dictionary<string, string> Existed { get; init; }
}
