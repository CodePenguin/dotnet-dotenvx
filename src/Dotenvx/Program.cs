using Dotenvx;
using Dotenvx.Extensions;
using Dotenvx.Logging;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

DotenvxLoggerOptions loggerOptions = DotenvxRootCommand.GetDotenvxLoggerConfiguration(args);

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureHostConfiguration(builder =>
    {
        builder.AddEnvironmentVariables();
    })
    .ConfigureServices(services =>
    {
        services.AddDotenvxCommands();
        services.AddDotenvxLogging(loggerOptions);
    })
    .ConfigureLogging(options =>
    {
        options.ClearProviders();
        options.AddDotenvx(loggerOptions);
    })
    .Build();

return host.RunDotenvxCommands(args);