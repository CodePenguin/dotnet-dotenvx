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
        services.AddTransient<ArmorCommand>();
        services.AddTransient<LoginAction>();
        services.AddTransient<LogoutAction>();
        services.AddTransient<CurlAction>();
        services.AddTransient<NativeCommand>();
        services.AddTransient<ProtectAction>();
        services.AddTransient<HiddenCommand>();
        services.AddTransient<FeedbackAction>();
        services.AddTransient<SpecAction>();
        services.AddTransient<CheckAction>();
        services.AddTransient<PrimitivesCommand>();
        services.AddTransient<OnePasswordCommand>();
        services.AddTransient<BitwardenCommand>();
        services.AddTransient<global::Dotenvx.Actions.HelpAction>();
        services.AddTransient<UpdateAction>();
        services.AddTransient<PrebuildAction>();
        services.AddTransient<PrecommitAction>();
        services.AddTransient<ScanAction>();
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
            return new DotenvxLogger(serviceProvider.GetRequiredService<ILogger<DotenvxLogger>>(), options);
        });
    }
}