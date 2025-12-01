Dotenvx.Dotenvx.Config();

Console.WriteLine($"Hello, {Environment.GetEnvironmentVariable("HELLO") ?? "[Not Set]"}");