using dotnet_dotenvx.Actions;

using Microsoft.Extensions.DependencyInjection;

namespace dotnet_dotenvx.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddDotenvxCommands(this IServiceCollection services)
    {
        services.AddTransient<DotenvxRootCommand>();
        services.AddTransient<RunAction>();
    }
}