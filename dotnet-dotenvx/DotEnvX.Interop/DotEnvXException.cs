using System;

namespace dotnet_dotenvx.DotEnvX.Interop;

public class DotEnvXException(string code, string message) : Exception(message)
{
    public string Code { get; private set; } = code;
}