using dotnet_dotenvx.Extensions;

using Microsoft.Extensions.Configuration;
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
        services.AddDotenvxLogging(args);
    })
    .ConfigureLogging(options =>
    {
        options.ClearProviders();
        options.AddDotenvx();
    })
    .Build();

return host.RunDotenvxCommands(args);