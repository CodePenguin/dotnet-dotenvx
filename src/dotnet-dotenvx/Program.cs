using System;
using System.Threading;
using System.Threading.Tasks;

using Dotenvx.Configuration;

using dotnet_dotenvx;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureHostConfiguration(builder =>
    {
        builder.AddEnvironmentVariables();
        builder.UseDotenvx();
    })
    .ConfigureServices(services => { services.AddTransient<MyService>(); })
    .Build();

MyService my = host.Services.GetRequiredService<MyService>();
await my.ExecuteAsync();

namespace dotnet_dotenvx
{
    internal class MyService(IConfiguration configuration)
    {
        public Task ExecuteAsync(CancellationToken stoppingToken = default)
        {
            Console.WriteLine($"Hello, {configuration["HELLO"] ?? "[Not Set]"}");
            return Task.CompletedTask;
        }
    }
}