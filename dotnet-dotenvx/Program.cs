using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using dotnet_dotenvx;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureHostConfiguration(builder =>
    {
        builder.AddEnvironmentVariables();
        builder.UseDotEnvX();
    })
    .ConfigureServices(services =>
    {
        services.AddTransient<MyService>();
    })
    .Build();

var my = host.Services.GetRequiredService<MyService>();
await my.ExecuteAsync();

internal class MyService(IConfiguration configuration)
{
    public Task ExecuteAsync(CancellationToken stoppingToken = default)
    {
        Console.WriteLine($"Hello, {configuration["HELLO"] ?? "[Not Set]"}");
        return Task.CompletedTask;
    }
}