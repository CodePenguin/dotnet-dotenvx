using Dotenvx.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureHostConfiguration(builder =>
    {
        builder.UseDotenvx();
    })
    .Build();

IConfiguration configuration = host.Services.GetRequiredService<IConfiguration>();

Console.WriteLine($"Hello, {configuration["HELLO"] ?? "[Not Set]"}!");