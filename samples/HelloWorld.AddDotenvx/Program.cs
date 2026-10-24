using Dotenvx.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureHostConfiguration(builder =>
    {
        builder.AddDotenvx();
    })
    .Build();

IConfiguration configuration = host.Services.GetRequiredService<IConfiguration>();

Console.WriteLine($"Hello, {configuration["HELLO"] ?? "[Not Set]"}!");
