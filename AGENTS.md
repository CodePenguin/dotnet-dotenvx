# AGENTS.md

Guidance for coding agents (Claude Code, opencode, Codex, ...) working in this repository.

## What this is

A .NET 10 port of [`@dotenvx/dotenvx`](https://github.com/dotenvx/dotenvx) that aims for **byte-for-byte parity** with the Node CLI and library. It ships four NuGet packages plus a `dotnet tool`:

| Project | Role |
| --- | --- |
| `src/Dotenvx` | the `dotenvx` CLI (`dotnet tool`), built on `System.CommandLine` |
| `src/Dotenvx.Core` | ported library logic (`Dotenvx.Config/Get/Parse/Set`), used by the CLI and `Dotenvx.Configuration` |
| `src/Dotenvx.Primitives` | port of the external `@dotenvx/primitives` npm package (every export has a public `Primitives.*` counterpart) |
| `src/Dotenvx.Configuration` | `IConfigurationBuilder.UseDotenvx()` for `Microsoft.Extensions.Configuration` |

The upstream JS project is a git submodule at `dotenvx/`, used as porting reference and as the source of test fixtures. After cloning: `git submodule update --init --recursive` (test projects fail without it).

### Pinning rules

- The submodule is pinned to the exact tag **`v2.24.1`** (never upstream HEAD, never a commit past the tag). `src/Dotenvx/Dotenvx.csproj` `Version` stays equal to the pinned tag, so `dotenvx --version` states which release this port matches.
- `src/Dotenvx.Primitives/Dotenvx.Primitives.csproj` `Version` equals the installed `@dotenvx/primitives` version (`dotenvx/node_modules/@dotenvx/primitives/package.json`, currently **3.0.2**).
- Bump versions only through the "Version bump checklist" below. Do not run `git submodule update --remote`.
- Out of scope on purpose: Dotenvx Armor/cloud features, 1Password/Bitwarden resolvers, OS-native secret stores. The parity tests pass `--no-native --no-armor` to Node for this reason; keep those flags (past the pin, native storage becomes the default again).
- `precommit`/`prebuild` are current at the pin but deprecated upstream in favor of `protect`; revisit when the pin passes that change.

## Commands

```
dotnet restore
dotnet build --no-restore
dotnet test --no-build --verbosity normal

dotnet test test/Dotenvx.Core.Test/Dotenvx.Core.Test.csproj          # one project
dotnet test --filter "FullyQualifiedName~Dotenvx.Core.Test.Interop.DotenvxJs.MainTest.SomeTestName"
dotnet run --project src/Dotenvx -- get                               # run the CLI
```

CI (`.github/workflows/ci.yml`) runs restore / build / test on ubuntu with submodules checked out; SDK pinned by `global.json` (.NET 10).

Run test projects separately. `Dotenvx.ParityTest` spawns real `node` and `dotnet` subprocesses and takes minutes (`PrimitivesExportsOracleParityTest` alone about 2 minutes, so do not wrap it in a short timeout: a killed test host looks like a crash). It skips itself when Node/npm or the submodule is missing. The other test projects run in seconds and are fully offline.

## Architecture and porting convention

The code is a near line-for-line port. Check the referenced JS file in `dotenvx/` before fixing anything: parity with upstream beats idiomatic C#.

- `src/Dotenvx.Core/Interop.DotenvxJs/Helpers/` ports `dotenvx/src/lib/helpers/*.js`; `Services/` keeps the old `services/*.js` layout (upstream now has `resolvers/` and `transforms/`); `Main.cs` ports `main.js`; `Types/` holds option/result POCOs.
- `src/Dotenvx/Actions/*.cs` port `dotenvx/src/cli/actions/*.js` (each a `System.CommandLine` `Command`).
- Most files carry `/// <remarks>Reference source: dotenvx/src/lib/helpers/fsx.js</remarks>`. Re-verify that the named file still exists at the pin when you touch a file (the upstream reorg moved many).
- `Interop.IgnoreJs` (gitignore matching) is Core's; `Interop.PicomatchJs` and `Interop.EciesJs` live in `Dotenvx.Primitives`.

### `Dotenvx.Primitives`

Flat `namespace Dotenvx.Primitives`, one `public static partial class Primitives`, methods named after the real exports. Exports: `decrypt, derive, encrypt, encrypted, evaluate, expand, keypair, keyring, keyringSync, match, parse, parseSync, parsearrays, parsearraysSync, publickeys, remove, scan, sealed, upsert` (C# names: `Decrypt`, `Derive`, ..., `Parse`/`ParseSync`/`ParseArrays`/`ParseArraysSync`, `Scan`, `Sealed`, `Upsert`). `Primitives.Scan` is the one shared JS-faithful line parser. The `DotenvxException` hierarchy lives here too (primitives throws errors.js-shaped errors) but keeps the namespace `Dotenvx.Interop.DotenvxJs.Types`.

Two deliberate shape differences: `Encrypt(value, publicKey)` has the opposite parameter order to JS `encrypt(publicKey, value)` (both are strings, so a silent swap is the risk); `Evaluate` keeps an explicit `key` parameter for error messages.

What stays in Core: dotenvx's own code. `DecryptKeyValue` wraps `Primitives.Decrypt` and adds key names to errors; `SmartDotenvPublicKey`/`SmartDotenvPrivateKey` do key lookup; Core's `Helpers/Parse.cs` is dotenvx's own engine (single private key, per-key errors), a different layer from `Primitives.ParseSync` (keyring based).

C# constraints that shaped this: a `partial class` must live in one assembly, which is why Core call sites carry both `using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;` and `using static Dotenvx.Primitives.Primitives;`. Inside `namespace Dotenvx.*`, a reference starting `Dotenvx.Primitives...` resolves relative to the enclosing namespace; qualify with `global::`.

`test/Dotenvx.Primitives.Test` references only `Dotenvx.Primitives` (never Core).

### Test traceability convention

Every test should say why it exists, so coverage gaps and stale tests can be found by diffing JS test titles against C# ones.

- Class: `/// <remarks>Reference source: dotenvx/src/lib/....js</remarks>` naming a file that exists at the pin.
- Method: `/// <remarks>Refer to "{exact upstream test title}"</remarks>`, copied literally from the JS `t.test('...')`/`it('...')`.
- No current upstream equivalent: write `No longer a distinct current upstream scenario - historical coverage from the pre-reorg <file>.test.js's "<title>".` or `Not from a JS test - <reason>.` Never leave a remark that implies a match that is not there.
- Bring a file up to date when you are already editing it; no blanket retrofit is required.

### Errors print one way

`dotenvx/src/lib/helpers/errors.js` is the single source for every error code, message and help text; help is always the generic `fix: [url]`. Every action prints errors with exactly `logger.Error(error.MessageWithHelp ?? error.Message)`: one line, same log level in every branch. Do not add per-command help text or vary the level without checking upstream's `errors.js`, `catchAndLog.js` and the specific action first.

## Static seams and test mocking

Side-effecting statics are mockable through two patterns; follow them for new I/O or services:

1. **Delegate-swap statics** (e.g. `Fsx`): `public static Func<...>/Action<...> { get; internal set; }` defaulting to the real implementation, plus `internal static ResetToDefaults()`.
2. **Factory-swap statics** (e.g. `Del`, `Get`, `KeyPair`, `Ls`, `Parse`, `Run`, `Sets`): `public static Func<..., T> Factory { get; internal set; }` plus `internal static ResetFactory()`; mockable methods are `public virtual`; a `protected` parameterless (or default-arg) constructor marked `// For unit tests` lets NSubstitute proxy the class.

`InternalsVisibleTo` (in `Dotenvx.Core.csproj`) exposes these to `Dotenvx.Core.Test`, `Dotenvx.Configuration.Test` and `Dotenvx.Test`. The CLI project itself has none, so a test seam for CLI-only logic must live in Core.

Mocking uses **NSubstitute** (not Moq): `Substitute.For<T>(ctorArgs...)`, `.Returns(...)`, `.Received(1)`, and `using NSubstitute.ExceptionExtensions;` for `.Throws(...)`. After changing a constructor, grep both `Substitute.For<T>(` and `T.Factory =` separately.

Tests that touch env vars, the filesystem or these statics derive from `test/Dotenvx.Core.Test/EnvironmentTestBase.cs` (snapshots/restores env and cwd, resets every Factory and `Fsx`). `Dotenvx.Core.Test` and `Dotenvx.Test` disable parallelization on purpose. Since .NET 9 an env var set to `""` stays `""`; see `EnvironmentTestBase.EmptyEnvironmentVariableValue`.

## Verifying against the real Node CLI

Do not trust reading the JS alone. Run both, on copies of the same fixtures, and diff raw bytes (`xxd`), not rendered text (colors and symbols look right far more easily than they match):

```
cd dotenvx && npm install --no-audit --no-fund --omit=dev && cd ..
node dotenvx/src/cli/dotenvx.js <command> ...
dotnet src/Dotenvx/bin/Debug/net10.0/dotenvx.dll <command> ...
```

Node and Windows both emit `\` from `path.join`; do not hardcode `/` in expected paths.

### `@dotenvx/primitives` is a live oracle (no source, no tests)

The package ships only a minified `dist/index.cjs` (no source, no maps, no tests; its README says its conformance tests are private; there is no public repo). Run it from inside `dotenvx/`:

```
node -e "console.log(require('@dotenvx/primitives').evaluate.toString())"   # real function body, mangled names
node -e "console.log(JSON.stringify(require('@dotenvx/primitives').parseSync('A=1',{processEnv:{}})))"
```

Parity is proven by oracle tests, not ported tests: `test/Dotenvx.ParityTest/PrimitivesOracleParityTest.cs` and `PrimitivesExportsOracleParityTest.cs` run the real package against the C# exports over input tables and replay every README example. Add a table row for each new edge case. `NodeParityFixture.RunNodeScriptAsync` runs node scripts, from a temp file when they exceed the Windows command-line limit.

## Parity test suite (`test/Dotenvx.ParityTest/`)

- Both CLIs run as real subprocesses via `NodeParityFixture`, so the shipped artifact is what is tested. It is a separate project for local speed; CI runs it with everything else.
- Division of labor: `Dotenvx.Test`/`Dotenvx.Core.Test` are the 1:1 ports of upstream's unit tests and stand alone. This suite exists only to catch drift between the two implementations, so add a scenario here only when cross-implementation behavior needs proving.
- Compare stdout and exit code, not stderr wording, unless the scenario is about stderr. Redact non-deterministic content (fresh keypairs, ECIES ciphertext) with `ParityTestBase.RedactKeyMaterial`.
- Deterministic structure tests also live here: `HelpTextParityTest` (options per command), `ErrorMessageParityTest` (every error subtype vs `errors.js`), `GlobalOptionsParityTest`.
- Use `FixedKeypairFixture` for deterministic keypairs; pass `--no-native --no-armor` to Node's `set`/`encrypt` (never in a `RunBothAsync`'s shared args) when the `.env` has no public key yet.

Detailed, numbered lessons from past bugs are in [docs/porting-traps.md](docs/porting-traps.md). Skim its headings before touching parsing, option handling, error output, or file encodings; the most reusable ones:

- A JS-vs-.NET regex or string difference is the usual root cause of a mismatch: `\s` and `trim` (BOM, no-break space), `\w` (ASCII-only in JS), multiline line terminators, `String.replace(str, str)` (first match only, `$` patterns), `||` on empty strings. (traps #41, #43, #66)
- `System.CommandLine` differs from commander: empty-array defaults, no variadic by default, recursive options, greedy variadics (traps #15, #36, #39, #42).
- A sweep that tests one input shape can pass hundreds of tests while another shape is broken (repeated vs space-separated flags; single flag vs flag combinations). Static re-reads and live comparisons find different bugs.
- Before changing a shared helper, grep every caller (trap #26). Before concluding something is unreachable or settled, check the submodule's git history and a second scenario (traps #27, #29).
- Do not make a stale test pass: confirm the real behavior live against Node first (trap #51).

## Version bump checklist (retarget dotenvx and/or @dotenvx/primitives)

Goal: retarget, run the suites, and let the failures plus a few diffs say exactly what to update. Work top to bottom; do not skip step 2 (a green suite only proves what is already asserted).

**1. Retarget**
1. `cd dotenvx && git fetch --tags && git checkout <new-tag>` (exact tag), then `git add dotenvx` from the repo root.
2. `src/Dotenvx/Dotenvx.csproj` `Version` = the new dotenvx tag.
3. `cd dotenvx && npm install --no-audit --no-fund --omit=dev`, read the installed `@dotenvx/primitives` version, set `src/Dotenvx.Primitives/Dotenvx.Primitives.csproj` `Version` to it, and update the version numbers quoted in this file.
4. Snapshot primitives function sources before and after the bump, from inside `dotenvx/`: `node -e "const p=require('@dotenvx/primitives');const o={};for(const k of Object.keys(p))o[k]=p[k].toString();require('fs').writeFileSync(process.argv[1],JSON.stringify(o,null,1))" <scratch>/primitives-<ver>.json`.

**2. Find what upstream changed**
1. `Object.keys(require('@dotenvx/primitives'))` against the export list above: a new export needs a `Primitives.*` counterpart plus oracle rows; a removed one needs a decision.
2. Diff the two snapshots from 1.4. Each changed export means re-reading it, updating our port, and adding oracle rows.
3. Diff the primitives `README.md`: every example, option table and operator list. Replay new or changed examples in `PrimitivesExportsOracleParityTest.ReadmeExamples...` and extend its option matrices.
4. dotenvx internals, inside `dotenvx/`: `git log --oneline <old>..<new>`, `git diff <old> <new> --stat -- src tests CHANGELOG.md`, then read the real hunks one file at a time (`git diff <old> <new> -- src/lib/<file>.js`; also `src/cli`, `src/shared`, `src/lib/helpers/errors.js`, `tests/**`; add `-M` for renames). Map each changed `src/**` file to its C# port through the `Reference source:` comments and re-port the hunks; map each changed or new test title through the `Refer to "..."` remarks.
5. Primitives internals: keep the old tarball (`npm pack @dotenvx/primitives@<old>`), pretty-print both `dist/index.cjs` files (`npx prettier --parser babel`), and `git diff --no-index` them. Vendored crypto changes matter only if ciphertext format, key derivation or error messages change (rerun the keypair/encrypt/decrypt oracles); engine changes (scan/parse/expand/keyring/upsert/remove/sealed) need the matching C# edit.
6. New or changed commands and flags: diff the `dotenvx.js` registrations and run `dotenvx <cmd> --help` per command. Re-check commander shapes (`<x>` vs `<x...>`, defaults, negations).
7. Re-check version-dependent scope notes: native-store default, `precommit`/`prebuild` to `protect`, Armor/cloud, `ext` additions.

**3. Run the suites; read failures as the to-do list**
1. `dotnet build`, then `Dotenvx.Primitives.Test`, `Dotenvx.Core.Test`, `Dotenvx.Configuration.Test`, `Dotenvx.Test`, `Dotenvx.ParityTest`, each separately.
2. Meaning of failures: the two primitives oracle tests = our port drifted (output prints Node vs C# per input; fix the port, add a table row); `ErrorMessageParityTest` = `errors.js` changed (new codes need a `DotenvxException` subtype); `HelpTextParityTest` = flags or descriptions changed (fix the option, or extend its documented exclusion lists with a reason); `GlobalOptionsParityTest.RootVersionFlagMatchesExactly` = version lockstep broken; other `*ParityTest` = CLI drift; Core/Dotenvx unit tests = ported tests encoding old behavior (confirm the new behavior live before rewriting one).
3. Constructor changes breaking mocks: see "Static seams" above.

**4. Close the gaps the suites cannot see**
1. Re-run the `--help` diff per command, flag-combination and space-separated-vs-repeated multi-value sweeps for any new flag, and a static re-read of every action or service whose upstream file changed.
2. Port new upstream tests under the traceability convention, or record why not (out of scope, no gap).
3. Re-verify any "known difference" claimed in docs before relying on it; they go stale silently.

**5. Record it**
1. Update the pinning notes and primitives version here, and add a numbered trap to `docs/porting-traps.md` for anything non-obvious (what, why, how to apply).
2. Update `.claude/plan.md` (goal, steps, files touched, branch). Commit only when asked; no attribution lines.

## CLI logging

`src/Dotenvx/Logging/` is a custom `ILogger`/`ILoggerProvider` (`DotenvxLogger`, `DotenvxCustomLogger*`) wired via `LoggingBuilderExtensions.AddDotenvx(...)`, matching dotenvx's leveled, colored output (quiet/error/info/verbose/debug) through `IDotenvxLogger` (`Interop.DotenvxJs/Shared/IDotenvxLogger.cs`). `DotenvxRootCommand.GetDotenvxLoggerConfiguration` pre-parses `args` once, silently, to pick the level before the host and command run. Only `info` goes to stdout; every other level goes to stderr (keeps stdout script-safe).

## Tooling cautions

- The Edit/Write tools rewrite literal Unicode-escape text (backslash, `u`, four hex digits) into the character itself, and raw U+2028 in C# source is a line terminator. Write such text through a shell (`printf`/`sed`) or build it from `(char)` values, and verify with `grep`.
- Shell heredocs can alter backslashes; prefer `(char)92` in C# and token substitution in test tables.
