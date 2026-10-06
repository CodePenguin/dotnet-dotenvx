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
        services.AddTransient<ExtCommand>();
        services.AddTransient<DecryptAction>();
        services.AddTransient<DelAction>();
        services.AddTransient<EncryptAction>();
        services.AddTransient<GenexampleAction>();
        services.AddTransient<GetAction>();
        services.AddTransient<GitignoreAction>();
        services.AddTransient<DoctorAction>();
        services.AddTransient<LockCommand>();
        services.AddTransient<ArmorCommand>();
        services.AddTransient<LoginAction>();
        services.AddTransient<LogoutAction>();
        services.AddTransient<CurlAction>();
        services.AddTransient<NativeCommand>();
        services.AddTransient<global::Dotenvx.Actions.HelpAction>();
        services.AddTransient<UpdateAction>();
        services.AddTransient<PrebuildAction>();
        services.AddTransient<PrecommitAction>();
        services.AddTransient<ScanAction>();
        services.AddTransient<ValidateAction>();
        services.AddTransient<KeyPairAction>();
        services.AddTransient<LsAction>();
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