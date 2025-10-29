using dotnet_dotenvx.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureHostConfiguration(builder =>
    {
        builder.AddEnvironmentVariables();
    })
    .ConfigureServices(services =>
    {
        services.AddDotenvxCommands();
        services.AddLogging(builder => builder.AddSimpleConsole(options =>
        {
            options.IncludeScopes = false;
            options.SingleLine = true;
        }));
    })
    .ConfigureLogging(options =>
    {
        options.SetDotenvxLogLevel(args);
    })
    .Build();

return host.RunDotenvxCommands(args);