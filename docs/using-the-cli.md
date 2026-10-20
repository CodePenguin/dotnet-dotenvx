# Using the CLI (manual execution)

The `dotenvx` command is the same program as the Node CLI: same commands, flags, output, exit codes and file formats. For what each command does, use the upstream documentation at [dotenvx.com/docs](https://dotenvx.com/docs) and `dotenvx <command> --help`. This page covers only what is specific to the .NET tool.

## Install

Requires the .NET 10 runtime.

```sh
# global: puts `dotenvx` on your PATH
dotnet tool install --global Dotenvx

# local to a repository: pins the version for everyone who clones it
dotnet new tool-manifest
dotnet tool install Dotenvx
dotnet tool restore     # on a fresh clone
```

A local tool is run as `dotnet dotenvx ...`; a global one as `dotenvx ...`. Everything else on this page works the same either way.

## Run a .NET program with your env

`run` injects the environment into any child process, so a .NET app needs no code changes:

```sh
dotenvx run -- dotnet run
dotenvx run -f .env.production -- dotnet MyApp.dll
dotnet dotenvx run -- dotnet run          # local tool
```

The app reads the values the usual way (`Environment.GetEnvironmentVariable`). Because it only sets variables for the child process, nothing about the app, its project or its configuration has to change. If you prefer to load the values from inside the app, see [Using the library](library.md) and [Automatic configuration](configuration.md).

## Encrypted files

`dotenvx encrypt` writes the public key into `.env` and the private key into `.env.keys`. An `.env` encrypted by this tool can be decrypted by the Node CLI and the reverse. In production, provide the private key as the `DOTENV_PRIVATE_KEY` environment variable (or `DOTENV_PRIVATE_KEY_PRODUCTION` for `.env.production`); the library and `run` decrypt at runtime.

## What differs from the Node CLI

- **Operating-system error text** stays native to .NET (for example the wording of a "file not found" error from the OS). dotenvx's own messages are identical.
- **Spinner and prompt redraw sequences** differ internally; the visible result is the same.
- **`ext` programs** are found the same way (`dotenvx-ext-<name>` on your `PATH`); `dotenvx ext vault` prints the same install hint as upstream.
- Corner cases are listed in [parity-checklist.md](parity-checklist.md).
