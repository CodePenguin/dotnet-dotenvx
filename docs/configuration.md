# Automatic configuration (`Dotenvx.Configuration`)

`Dotenvx.Configuration` adds your `.env` files, encrypted ones included, to `Microsoft.Extensions.Configuration`, so the values show up in `IConfiguration` next to `appsettings.json` and the rest. This has no Node equivalent.

```sh
dotnet add package Dotenvx.Configuration
```

## Host builder

```csharp
using Dotenvx.Configuration;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureHostConfiguration(builder => builder.AddDotenvx())
    .Build();

IConfiguration configuration = host.Services.GetRequiredService<IConfiguration>();
Console.WriteLine(configuration["HELLO"]);
```

`ConfigureAppConfiguration` works the same way if you prefer to add it there. A runnable version is in [`samples/HelloWorld.AddDotenvx`](../samples/HelloWorld.AddDotenvx).

## Options

`AddDotenvx` takes an optional callback that configures the same `ConfigOptions` object as the [library](library.md) (`Path`, `Overload`, `Strict`, `Convention`, `EnvKeysFile`, ...):

```csharp
builder.AddDotenvx(options =>
{
    options.Path = [".env.local", ".env"];
    options.Strict = true;
});
```

Another overload configures the source itself, which adds reloading:

```csharp
builder.AddDotenvx(source =>
{
    source.ConfigureOptions = options => options.Path = [".env"];
    source.ReloadOnChange = true; // reload when a Path file (default .env) changes
    source.ReloadDelay = 250;     // milliseconds to wait after a change (default)
});
```

## Things to know

- **The provider only reads.** It never writes the process environment, so `Environment.GetEnvironmentVariable` does not see the values. It starts from a snapshot of the current environment: `${VAR}` expansion sees it, and a variable that is already set wins over the file unless `Overload` is set. Use [`Dotenvx.Config()`](library.md) when you want the values injected into the process.
- **It is silent.** Nothing is printed unless you set `options.Logger`.
- **Keys follow the Microsoft conventions.** Lookups are case-insensitive, and `__` in a key is the section separator, as in environment variables: `ConnectionStrings__Default` is read as `configuration["ConnectionStrings:Default"]`.
- **Order matters.** Providers added later override earlier ones. Add `AddDotenvx()` before the sources you want it to be overridden by.
- **Missing files** do nothing unless `options.Strict` is set, as in the library.
- **Reloading** watches the `Path` files (default `.env`); `Convention` files and `Envs` are not watched. A reload that fails keeps the previous values. Dispose the provider (or the host) to stop watching.
- **Working directory.** Files are looked up relative to the current directory, so copy `.env` to the output directory (`<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`) or set `Path`.
- **Encrypted files** need the private key from `.env.keys` or `DOTENV_PRIVATE_KEY` at runtime, exactly like the CLI. In production, provide it as an environment variable.

## Which approach?

| You want | Use |
| --- | --- |
| no code changes, any language | [`dotenvx run -- ...`](using-the-cli.md) |
| `.env` loaded into the process by one line of code | [`Dotenvx.Config()`](library.md) |
| `.env` as an `IConfiguration` source (options pattern, DI) | `AddDotenvx()` (this page) |
