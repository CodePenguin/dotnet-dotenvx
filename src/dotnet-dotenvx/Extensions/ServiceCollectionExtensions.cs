using Dotenvx.Core.Interop.DotenvxJs.Shared;

using dotnet_dotenvx.Actions;
using dotnet_dotenvx.Logging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace dotnet_dotenvx.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddDotenvxCommands(this IServiceCollection services)
    {
        services.AddTransient<DotenvxRootCommand>();
        services.AddTransient<RunAction>();
    }

    public static void AddDotenvxLogging(this IServiceCollection services, DotenvxLoggerOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IDotenvxLogger>(serviceProvider =>
        {
            DotenvxLogger logger = new(serviceProvider.GetRequiredService<ILogger<DotenvxLogger>>(), options);
            logger.Debug($"Setting log level to {options.LogLevel.ToString().ToLower()}");
            return logger;
        });
    }
}