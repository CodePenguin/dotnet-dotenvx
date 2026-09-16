using Dotenvx.Actions;
using Dotenvx.Interop.DotenvxJs.Shared;
using Dotenvx.Logging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dotenvx.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddDotenvxCommands(this IServiceCollection services)
    {
        services.AddTransient<DotenvxRootCommand>();
        services.AddTransient<DecryptAction>();
        services.AddTransient<DelAction>();
        services.AddTransient<EncryptAction>();
        services.AddTransient<GetAction>();
        services.AddTransient<KeyPairAction>();
        services.AddTransient<LsAction>();
        services.AddTransient<RotateAction>();
        services.AddTransient<RunAction>();
        services.AddTransient<SetAction>();
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