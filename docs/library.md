# Using the library (`Dotenvx.Core`)

Load, read, parse and write `.env` values (encrypted ones included) from .NET code. It is the .NET counterpart of the Node library API (`require('@dotenvx/dotenvx').config()` and friends), and the file formats and behavior are the same. Requires .NET 10.

```sh
dotnet add package Dotenvx.Core
```

The entry points are static methods on `Dotenvx.Dotenvx`. Each takes an optional options object (the option and result types are in the `Dotenvx` namespace, so add `using Dotenvx;`); property names follow the Node options in PascalCase (`path` is `Path`, `overload` is `Overload`, `envKeysFile` is `EnvKeysFile`, `noNative` is `NoNative`, ...).

## `Config`: load into the process environment

```csharp
Dotenvx.Dotenvx.Config();

Console.WriteLine(Environment.GetEnvironmentVariable("HELLO"));
```

Reads `.env` (decrypting values as needed) and sets each variable on the current process. A variable that is already set is kept unless `Overload = true`. Call it before anything reads the environment.

```csharp
using Dotenvx;

ConfigResult result = Dotenvx.Dotenvx.Config(new ConfigOptions
{
    Path = [".env.local", ".env"],   // first file wins; PathString = ".env.local,.env" also works
    Overload = false,                 // true: values from the files replace variables that are already set
    ProcessEnv = myDictionary         // write here instead of the real environment
});

IReadOnlyDictionary<string, string> loaded = result.Parsed;
```

Other options: `Strict = true` throws on the first error (including a listed file that does not exist) instead of logging it, and `Convention = "nextjs"` loads a framework's file set.

`Config` logs problems through `ConfigOptions.Logger` (nothing is printed when it is left unset). Use `Ignore = ["MISSING_ENV_FILE"]` to skip specific error codes.

## `Get`: read values

```csharp
using Dotenvx;

object? one = Dotenvx.Dotenvx.Get("HELLO");        // the value, or null
object? all = Dotenvx.Dotenvx.Get(null);           // IReadOnlyDictionary<string, string> of every key
object? shell = Dotenvx.Dotenvx.Get(null, new GetOptions { Format = "shell" });   // "A=1 B=2"
```

`Get` does not modify the process environment. `Format` accepts `shell`, `eval`, `eval-export` and `colon`. The return type is `object?` because it is a `string` for a key, a formatted `string` for a format, and a dictionary otherwise.

## `Parse`: parse text you already have

```csharp
IReadOnlyDictionary<string, string> values = Dotenvx.Dotenvx.Parse("HELLO=World\nNAME=\"dotenvx\"");
```

Works on a string and reads no files and does not modify the process environment. To decrypt an encrypted string, pass the key: `new ParseOptions { PrivateKey = "..." }`.

## `Set`: write a value

```csharp
using Dotenvx;

SetsResult result = Dotenvx.Dotenvx.Set("HELLO", "World", new SetsOptions { Path = [".env"] });
```

Encrypts the value by default, creating the key pair if needed. `Plain = true` keeps it readable and `NoCreate = true` leaves a missing file uncreated (nothing is written). `ChangedFilePaths` and `UnchangedFilePaths` in the result say what was written.

## Keys

Private keys are looked up the same way as the CLI (`.env.keys`, the `DOTENV_PRIVATE_KEY` environment variable, the OS secret store, 1Password, Bitwarden and Dotenvx Armor). Turn sources off with `NoNative`, `No1Password`, `NoBitwarden` and `NoArmor` on the options, or point `EnvKeysFile` at another keys file.

## Packages

| Package | Use it when |
| --- | --- |
| `Dotenvx.Core` | you want `Config/Get/Parse/Set` |
| [`Dotenvx.Configuration`](configuration.md) | you want `.env` as an `IConfiguration` source |
| `Dotenvx.Primitives` | you only need the low-level parse/encrypt/decrypt functions (port of `@dotenvx/primitives`) |

Copy the `.env` file to your output directory (`<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`), because `Config` and `Get` read it relative to the working directory. See [`samples/HelloWorld.Config`](../samples/HelloWorld.Config).
