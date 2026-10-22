using System;

using Microsoft.Extensions.Logging;

namespace Dotenvx.Logging;

public class DotenvxLoggerProvider(DotenvxCustomLoggerConfiguration configuration) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new DotenvxCustomLogger(configuration);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
