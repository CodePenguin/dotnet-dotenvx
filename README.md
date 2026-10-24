# dotenvx for .NET

[![NuGet](https://img.shields.io/nuget/v/Dotenvx?label=nuget&logo=nuget)](https://www.nuget.org/packages/Dotenvx)
[![Downloads](https://img.shields.io/nuget/dt/Dotenvx?logo=nuget)](https://www.nuget.org/packages/Dotenvx)
[![CI](https://github.com/CodePenguin/dotnet-dotenvx/actions/workflows/ci.yml/badge.svg)](https://github.com/CodePenguin/dotnet-dotenvx/actions/workflows/ci.yml)

*a secure dotenv* for .NET: a port of [`@dotenvx/dotenvx`](https://github.com/dotenvx/dotenvx) by Scott Motte ([@motdotla](https://github.com/motdotla)), the creator of `dotenv`.

- **run anywhere**: one `dotenvx` command that injects your `.env` into any program (cross-platform)
- **multi-environment**: `.env`, `.env.production`, `.env.local`, conventions such as Next.js
- **encrypted envs**: commit an encrypted `.env`, keep the private key out of source control
- **a .NET library** too: load `.env` files into environment variables or `IConfiguration`

The port aims for byte-for-byte compatibility with the Node CLI at **v2.35.0**: same commands, flags, output, exit codes and file formats. A `.env` encrypted by one can be decrypted by the other. See [Compatibility](#compatibility).

> This is a .NET port, not the upstream project. For the product, documentation and cloud features see [dotenvx.com](https://dotenvx.com). Requires the .NET 10 runtime.

## Quickstart

Install the CLI as a .NET tool:

```sh
dotnet tool install --global Dotenvx
dotenvx --version   # 2.35.0
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
# ◈ encrypted .env (03F B56) via .env.keys ⛉
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
# ⟐ injected env (2) from .env (03F B56) via .env.keys ⛉
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
| `run -- <command>` | inject env into a command (`--overload`, `--strict`, `--redact`, `--mask`, `--profile`, ...) |
| `get [KEY]` | print one value, or every value as JSON (`--all`, `--format`, `--pretty-print`) |
| `set <KEY> [value]` | set a value, encrypting it by default (`--plain` to keep it readable) |
| `del <KEY>` | delete a key |
| `encrypt` / `decrypt` | encrypt or decrypt `.env` files in place (`-k`, `--exclude-key`, `--stdout`) |
| `keypair [KEY]` | print the public/private key pair |
| `ls [directory]` | list `.env` files as a tree |
| `genexample [directory]` | generate `.env.example` |
| `protect` | protect secrets and private keys from code commits (`--docker`, git filter); `gitignore`, `precommit`, `prebuild` are deprecated aliases |
| `check`, `spec` | check Envfile rules against `.env` files / create an Envfile (beta) |
| `primitives` | low-level Envfile operations (`profile`, `redact`, `policy`, `check`, `keypair`, `derive`) |
| `native`, `1password`, `bitwarden` | move private keys in/out of the OS secret store (Keychain, Credential Manager, Secret Service), 1Password or Bitwarden (`up`, `down`, `push`, `pull`) |
| `armor`, `login`, `logout`, `curl` | [Dotenvx Armor](https://dotenvx.com) integration |
| `doctor`, `update`, `help` | diagnostics, update instructions, help |
| `ext <name>` | extensions: `ext ls`, `ext genexample`, `ext scan`, ... and external `dotenvx-ext-<name>` programs |

`dotenvx <command> --help` shows every option. Global options: `-l/--log-level`, `-q`, `-v`, `-d`, `-V`.

Values can reference other variables (`${HELLO}`, `${NAME:-default}`, `$(command)`), and with 1Password or Bitwarden installed, `op://...` and `bw://...` references are resolved at run time.

## Documentation

Behaviour that is the same as the Node version (commands, flags, file formats, encryption) is documented upstream at [dotenvx.com/docs](https://dotenvx.com/docs) and in `dotenvx <command> --help`. These pages cover what is specific to the .NET port:

- [Using the CLI](https://github.com/CodePenguin/dotnet-dotenvx/blob/main/docs/using-the-cli.md): installing the tool, running .NET programs with `dotenvx run`, and where the CLI differs from Node.
- [Using the library](https://github.com/CodePenguin/dotnet-dotenvx/blob/main/docs/library.md): `Dotenvx.Config()`, `Get`, `Parse` and `Set` from your own code.
- [Automatic configuration](https://github.com/CodePenguin/dotnet-dotenvx/blob/main/docs/configuration.md): `AddDotenvx()`, `.env` as an `IConfiguration` source.

Runnable versions of each are in [`samples/`](https://github.com/CodePenguin/dotnet-dotenvx/tree/main/samples).

## Packages

| Package | Contents |
| --- | --- |
| `Dotenvx` | the `dotenvx` .NET tool |
| `Dotenvx.Core` | the library API (`Config`, `Get`, `Parse`, `Set`) and the ported CLI logic |
| `Dotenvx.Configuration` | `IConfigurationBuilder.AddDotenvx()` |
| `Dotenvx.Primitives` | port of the `@dotenvx/primitives` package (parse, encrypt, decrypt, keyring, ...); versioned like that package (3.3.0), not like the CLI |
| `Dotenvx.Providers` | port of the `@dotenvx/providers` package (key lookup in `.env.keys`, the OS secret store, Armor, 1Password, Bitwarden); versioned like that package, not like the CLI |

## Compatibility

- **Pinned target.** `dotenvx --version` prints `2.35.0`, the upstream release this port matches. The upstream repository is included as a git submodule (`dotenvx/`) at that exact tag and is used as the reference and as a source of test fixtures.
- **How it is checked.** Besides the unit tests ported from upstream, the test suite runs the real Node CLI and this one side by side on the same inputs and compares exit codes, stdout, stderr and resulting files. This includes the exact `--help` text of every command and a sweep of more than a thousand command, flag and syntax combinations.
- **Known differences.** Operating-system error text stays native to .NET, spinner and prompt redraw sequences differ, and a few corner cases are documented in [docs/parity-checklist.md](https://github.com/CodePenguin/dotnet-dotenvx/blob/main/docs/parity-checklist.md).

## Building from source

```sh
git clone --recurse-submodules https://github.com/CodePenguin/dotnet-dotenvx
cd dotnet-dotenvx
dotnet build
dotnet test
dotnet run --project src/Dotenvx -- run -- echo hello
```

The parity tests need Node.js and the `dotenvx/` submodule; they skip themselves when either is missing. See [CONTRIBUTING.md](https://github.com/CodePenguin/dotnet-dotenvx/blob/main/CONTRIBUTING.md) for the project layout, test conventions and how to move to a newer upstream version, and [docs/porting-traps.md](https://github.com/CodePenguin/dotnet-dotenvx/blob/main/docs/porting-traps.md) for pitfalls found along the way.

## License

[BSD 3-Clause](https://github.com/CodePenguin/dotnet-dotenvx/blob/main/LICENSE). Copyright (c) 2024 Scott Motte; Copyright (c) 2026 David Lambert (.NET port).
