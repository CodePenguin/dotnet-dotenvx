/*
 * Run with dotenvx: npx dotenvx run dotnet run
 * Run with .NET dotenvx: dotnet dotenvx run
 */

Console.WriteLine($"Hello, {Environment.GetEnvironmentVariable("HELLO") ?? "[Not Set]"}!");
