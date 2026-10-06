# dotenvx for .NET

*a secure dotenv* for .NET: a port of [`@dotenvx/dotenvx`](https://github.com/dotenvx/dotenvx) by Scott Motte ([@motdotla](https://github.com/motdotla)), the creator of `dotenv`.

- **run anywhere**: one `dotenvx` command that injects your `.env` into any program (cross-platform)
- **multi-environment**: `.env`, `.env.production`, `.env.local`, conventions such as Next.js
- **encrypted envs**: commit an encrypted `.env`, keep the private key out of source control
- **a .NET library** too: load `.env` files into environment variables or `IConfiguration`

The port aims for byte-for-byte compatibility with the Node CLI at **v2.24.1**: same commands, flags, output, exit codes and file formats. A `.env` encrypted by one can be decrypted by the other. See [Compatibility](#compatibility).

> This is a .NET port, not the upstream project. For the product, documentation and cloud features see [dotenvx.com](https://dotenvx.com). Requires the .NET 10 runtime.

## Quickstart

Install the CLI as a .NET tool:

```sh
dotnet tool install --global Dotenvx
dotenvx --version   # 2.24.1
```

Create a `.env` and run any command with it:

```sh
echo 'HELLO="World"' > .env

dotenvx run -- node -e 'console.log("Hello " + process.env.HELLO)'
# ⟐ injected env (1) from .env
# Hello World
```

It works with any language or tool, because it only sets environment variables for the child process.

### Encrypt it

```sh
dotenvx encrypt
# ◈ encrypted (.env)
```

`.env` now holds a public key and ciphertext, and a new `.env.keys` holds the private key:

```ini
#/-------------------[DOTENV_PUBLIC_KEY]--------------------/
...
DOTENV_PUBLIC_KEY="03fb56a5c14d9303..."

# .env
HELLO="encrypted:BCtGJMSPQbej/ZQ6yILA8ZhVGFKJFYhs..."
```

```sh
dotenvx run -- node -e 'console.log("Hello " + process.env.HELLO)'
# ⟐ injected env (2) from .env
# Hello World

dotenvx get HELLO
# World
```

Commit `.env`. **Never commit `.env.keys`**; in production, provide its contents as the `DOTENV_PRIVATE_KEY` environment variable and dotenvx decrypts at runtime.

### Multiple environments

```sh
echo 'HELLO="production"' > .env.production
dotenvx run -f .env.production -- node -e 'console.log("Hello " + process.env.HELLO)'
# ⟐ injected env (1) from .env.production
# Hello production
```

Use `-f` more than once to layer files (the first file wins unless you pass `--overload`), `-e KEY=value` to set a value inline, and `--convention nextjs` or `--convention flow` to load a framework's file set.

## Commands

| Command | What it does |
| --- | --- |
| `run -- <command>` | inject env into a command (`--overload`, `--strict`, `--redact`, `--mask`, `--validate`, ...) |
| `get [KEY]` | print one value, or every value as JSON (`--all`, `--format`, `--pretty-print`) |
| `set <KEY> [value]` | set a value, encrypting it by default (`--plain` to keep it readable) |
| `del <KEY>` | delete a key |
| `encrypt` / `decrypt` | encrypt or decrypt `.env` files in place (`-k`, `--exclude-key`, `--stdout`) |
| `keypair [KEY]` | print the public/private key pair |
| `ls [directory]` | list `.env` files as a tree |
| `validate` | check `.env` against `.env.example` |
| `genexample [directory]` | generate `.env.example` |
| `gitignore` | add `.env*` patterns to `.gitignore` |
| `precommit`, `prebuild` | guard against committing or building with unencrypted `.env` files |
| `lock` | protect private keys with a local passphrase |
| `native` | keep private keys in the OS secret store (Keychain, Credential Manager, Secret Service) |
| `armor`, `login`, `logout`, `curl` | [Dotenvx Armor](https://dotenvx.com) integration |
| `doctor`, `update`, `help` | diagnostics, update instructions, help |
| `ext <name>` | extensions: `ext ls`, `ext genexample`, `ext scan`, ... and external `dotenvx-ext-<name>` programs |

`dotenvx <command> --help` shows every option. Global options: `-l/--log-level`, `-q`, `-v`, `-d`, `-V`.

Values can reference other variables (`${HELLO}`, `${NAME:-default}`, `$(command)`), and with 1Password or Bitwarden installed, `op://...` and `bw://...` references are resolved at run time.

## Library

The CLI's engine is also available as NuGet libraries.

**Environment variables.** `Dotenvx.Config()` loads `.env` (decrypting as needed) into the process environment, like `dotenv`:

```csharp
Dotenvx.Dotenvx.Config();

Console.WriteLine($"Hello, {Environment.GetEnvironmentVariable("HELLO")}!");
```

`Dotenvx.Dotenvx.Get("KEY")`, `Parse(string src)` and `Set("KEY", "value")` are the other entry points, each taking an options object (paths, `Overload`, `Strict`, `ProcessEnv`, `Convention`, ...).

**`IConfiguration`.** `Dotenvx.Configuration` adds your `.env` files to `Microsoft.Extensions.Configuration`:

```csharp
using Dotenvx.Configuration;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureHostConfiguration(builder => builder.UseDotenvx())
    .Build();

IConfiguration configuration = host.Services.GetRequiredService<IConfiguration>();
Console.WriteLine($"Hello, {configuration["HELLO"] ?? "[Not Set]"}!");
```

See [`samples/`](samples) for runnable versions.

| Package | Contents |
| --- | --- |
| `Dotenvx` | the `dotenvx` .NET tool |
| `Dotenvx.Core` | the library API above and the ported CLI logic |
| `Dotenvx.Configuration` | `IConfigurationBuilder.UseDotenvx()` |
| `Dotenvx.Primitives` | port of the `@dotenvx/primitives` package (parse, encrypt, decrypt, keyring, ...) |

## Compatibility

- **Pinned target.** `dotenvx --version` prints `2.24.1`, the upstream release this port matches. The upstream repository is included as a git submodule (`dotenvx/`) at that exact tag and is used as the reference and as a source of test fixtures.
- **How it is checked.** Besides the unit tests ported from upstream, the test suite runs the real Node CLI and this one side by side on the same inputs and compares exit codes, stdout, stderr and resulting files. This includes the exact `--help` text of every command and a sweep of more than a thousand command, flag and syntax combinations.
- **Known differences.** Operating-system error text stays native to .NET, spinner and prompt redraw sequences differ, and a few corner cases are documented in [docs/parity-checklist.md](docs/parity-checklist.md).

## Building from source

```sh
git clone --recurse-submodules https://github.com/CodePenguin/dotnet-dotenvx
cd dotnet-dotenvx
dotnet build
dotnet test
dotnet run --project src/Dotenvx -- run -- echo hello
```

The parity tests need Node.js and the `dotenvx/` submodule; they skip themselves when either is missing. See [CONTRIBUTING.md](CONTRIBUTING.md) for the project layout, test conventions and how to move to a newer upstream version, and [docs/porting-traps.md](docs/porting-traps.md) for pitfalls found along the way.

## License

[BSD 3-Clause](LICENSE). Copyright (c) 2024 Scott Motte; Copyright (c) 2026 David Lambert (.NET port).
