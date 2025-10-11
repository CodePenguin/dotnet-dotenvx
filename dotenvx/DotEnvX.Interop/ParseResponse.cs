namespace dotenvx.Dotenvx.Interop;

public class ParseResponse(IDictionary<string, string> parsed, IDictionary<string, string> processEnv, IDictionary<string, string> injected, IDictionary<string, string> preExisted, IList<string> errors)
{
    public IDictionary<string, string> Parsed { get; } = parsed;
    public IDictionary<string, string> Environment { get; } = processEnv;
    public IDictionary<string, string> Injected { get; } = injected;
    public IDictionary<string, string> PreExisted { get; } = preExisted;
    public IList<string> Errors { get; } = errors;
}