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

// Node.js always writes "\n" regardless of OS; Console.Out/Error default to Environment.NewLine,
// which is "\r\n" on Windows. Match Node so scripts piping our output don't see stray "\r".
Console.Out.NewLine = "\n";
Console.Error.NewLine = "\n";

DotenvxLoggerOptions loggerOptions = DotenvxRootCommand.GetDotenvxLoggerConfiguration(args);

// no args: the command line is commander-style, not host configuration (`-f=.env` made the config provider throw)
IHost host = Host.CreateDefaultBuilder()
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