# Automatic configuration (`Dotenvx.Configuration`)

`Dotenvx.Configuration` adds your `.env` files, encrypted ones included, to `Microsoft.Extensions.Configuration`, so the values show up in `IConfiguration` next to `appsettings.json` and the rest. This has no Node equivalent.

```sh
dotnet add package Dotenvx.Configuration
```

## Host builder

```csharp
using Dotenvx.Configuration;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureHostConfiguration(builder => builder.UseDotenvx())
    .Build();

IConfiguration configuration = host.Services.GetRequiredService<IConfiguration>();
Console.WriteLine(configuration["HELLO"]);
```

`ConfigureAppConfiguration` works the same way if you prefer to add it there. A runnable version is in [`samples/HelloWorld.UseDotenvx`](../samples/HelloWorld.UseDotenvx).

## Options

`UseDotenvx` takes an optional callback that configures the same `ConfigOptions` object as the [library](library.md) (`Path`, `Overload`, `Strict`, `Convention`, `EnvKeysFile`, ...):

```csharp
builder.UseDotenvx(options =>
{
    options.Path = [".env.local", ".env"];
    options.Strict = true;
});
```

## Things to know

- **The process environment is set too.** The provider loads through the same code as `Dotenvx.Config()`, so the values are also written to the process environment. A variable that is already set wins over the file (also in `IConfiguration`) unless `Overload` is set. Code that reads `Environment.GetEnvironmentVariable` sees the values as well.
- **Keys are flat.** Each `.env` key is exactly one configuration key (`HELLO`, `ConnectionStrings__Default`). Unlike the standard environment variable provider, `__` is **not** converted to a `:` section separator, so read `configuration["ConnectionStrings__Default"]`, not `configuration["ConnectionStrings:Default"]`.
- **Order matters.** Providers added later override earlier ones. Add `UseDotenvx()` before the sources you want it to be overridden by.
- **Working directory.** Files are looked up relative to the current directory, so copy `.env` to the output directory (`<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`) or set `Path`.
- **Encrypted files** need the private key from `.env.keys` or `DOTENV_PRIVATE_KEY` at runtime, exactly like the CLI. In production, provide it as an environment variable.

## Which approach?

| You want | Use |
| --- | --- |
| no code changes, any language | [`dotenvx run -- ...`](using-the-cli.md) |
| `.env` loaded into the process by one line of code | [`Dotenvx.Config()`](library.md) |
| `.env` as an `IConfiguration` source (options pattern, DI) | `UseDotenvx()` (this page) |
