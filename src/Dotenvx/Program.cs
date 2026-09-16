using System;
using System.IO;
using System.Text;

using Dotenvx;
using Dotenvx.Extensions;
using Dotenvx.Logging;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

try
{
    // Node.js writes raw UTF-8 to stdout regardless of console codepage; match that so the
    // checkmark/arrow glyphs in log output aren't replaced with '?' on Windows consoles whose
    // default OutputEncoding can't represent them.
    Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}
catch (IOException)
{
    // No console attached (e.g. some non-interactive hosts) - fall back to the default encoding.
}

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