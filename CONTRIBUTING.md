# Contributing

How this repository is organized and how to change it without breaking parity with the Node CLI. Read the sections you need; [docs/porting-traps.md](docs/porting-traps.md) lists past pitfalls by number (`#NN`), and [docs/parity-checklist.md](docs/parity-checklist.md) lists every component with its status and the accepted differences.

## What this is

A .NET 10 port of [`@dotenvx/dotenvx`](https://github.com/dotenvx/dotenvx) that aims for **byte-for-byte parity** with the Node CLI and library. It ships these NuGet packages plus a `dotnet tool`:

| Project | Role |
| --- | --- |
| `src/Dotenvx` | the `dotenvx` CLI (`dotnet tool`), built on `System.CommandLine` |
| `src/Dotenvx.Core` | ported library logic (`Dotenvx.Config/Get/Parse/Set`), used by the CLI and `Dotenvx.Configuration` |
| `src/Dotenvx.Primitives` | port of the external `@dotenvx/primitives` npm package (every export has a public `Primitives.*` counterpart) |
| `src/Dotenvx.Providers` | port of the external `@dotenvx/providers` npm package (private-key lookup in `.env.keys`, the OS secret store, Armor, 1Password, Bitwarden) |
| `src/Dotenvx.Configuration` | `IConfigurationBuilder.AddDotenvx()` for `Microsoft.Extensions.Configuration` |

The upstream JS project is a git submodule at `dotenvx/`, used as porting reference and as the source of test fixtures. After cloning: `git submodule update --init --recursive` (test projects fail without it).

### Pinning rules

- The submodule is pinned to the exact tag **`v2.35.0`** (never upstream HEAD, never a commit past the tag). `src/Dotenvx/Dotenvx.csproj` `Version` stays equal to the pinned tag, so `dotenvx --version` states which release this port matches.
- `src/Dotenvx.Primitives/Dotenvx.Primitives.csproj` `Version` equals the installed `@dotenvx/primitives` version (`dotenvx/node_modules/@dotenvx/primitives/package.json`, currently **3.3.0**).
- Bump versions only through the "Version bump checklist" below. Do not run `git submodule update --remote`.
- Scope rule: **anything that exists in the targeted version is in scope**, and all of it is ported: Dotenvx Armor (session/device stores, API client, login/logout/curl/settings/armor commands, key providers and decryptors), native secret stores and `native`, 1Password/Bitwarden resolution, `lock`, `doctor`/`update`/`help`, spinner, pty for `--redact`, signal forwarding and dynamic extensions. [docs/parity-checklist.md](docs/parity-checklist.md) lists every component, its status and the accepted differences; keep it current.
- `precommit`/`prebuild`/`gitignore` are deprecated at the pin in favor of `protect`; they still run and print the deprecation warning.

## Commands

```
dotnet restore
dotnet build --no-restore
dotnet test --no-build --verbosity normal

dotnet test test/Dotenvx.Core.Test/Dotenvx.Core.Test.csproj          # one project
dotnet test --filter "FullyQualifiedName~Dotenvx.Core.Test.Interop.DotenvxJs.MainTest.SomeTestName"
dotnet run --project src/Dotenvx -- get                               # run the CLI
```

CI (`.github/workflows/ci.yml`) runs restore / build / format check / unit tests on ubuntu, windows and macos with submodules checked out, in a few minutes; the parity suite is not part of it (run it on every OS before a release); SDK pinned by `global.json` (.NET 10).

Before pushing, run `scripts/prepush.sh`: it builds a fresh clone of the last commit with warnings as errors and runs the unit tests on one processor. Both catch what a warm many-core local build hides (warnings in projects that are already up to date, such as the fakes in `test/fakes`, and timing races that only show on a small CI runner).

Run test projects separately. `Dotenvx.ParityTest` spawns real `node` and `dotnet` subprocesses and takes minutes (`PrimitivesExportsOracleParityTest` alone about 2 minutes, so do not wrap it in a short timeout: a killed test host looks like a crash). It skips itself when Node/npm or the submodule is missing. The other test projects run in seconds and are fully offline.

## Architecture and porting convention

The code is a near line-for-line port. Check the referenced JS file in `dotenvx/` before fixing anything: parity with upstream beats idiomatic C#.

- `src/Dotenvx.Core/Interop.DotenvxJs/Helpers/` ports `dotenvx/src/lib/helpers/*.js`; `Services/` keeps the old `services/*.js` layout (upstream now has `resolvers/` and `transforms/`); `Main.cs` ports `main.js`; `Types/` holds option/result POCOs.
- `src/Dotenvx/Actions/*.cs` port `dotenvx/src/cli/actions/*.js` (each a `System.CommandLine` `Command`).
- Most files carry `/// <remarks>Reference source: dotenvx/src/lib/helpers/fsx.js</remarks>`. Re-verify that the named file still exists at the pin when you touch a file (the upstream reorg moved many).
- `Interop.IgnoreJs` (gitignore matching), `Interop.EnquirerJs` (prompts) and `Interop.YoctoSpinnerJs` (spinner) are folders in Core; `Interop.PicomatchJs` and `Interop.EciesJs` live in `Dotenvx.Primitives`.

### Public surface

Only these are public; everything else is `internal`, and `PublicSurfaceTest` fails when a type in an `Interop.*` namespace becomes public.

- `Dotenvx.Core`: the `Dotenvx` namespace (the `Dotenvx` class, the option and result types, `IDotenvxLogger` and `DotenvxConsoleLogger`, `EventSdk`, and every exception that derives from `DotenvxException`).
- `Dotenvx.Primitives`: `Primitives.*` (one public method per `@dotenvx/primitives` export) with its option and result types.
- `Dotenvx.Providers`: what `@dotenvx/providers` exports (`file`, `native`, `onepassword`, `bitwarden`, `armor`, `provider` and `providerOrder`) with `ProviderOptions` and `IKeyProvider`.
- Code that needs an internal type from another assembly gets it through `InternalsVisibleTo` in the csproj (the CLI, the test projects and `DynamicProxyGenAssembly2` for NSubstitute are already listed). A new test seam is `internal`, not `public`.
- Raw string literals that hold upstream text (the Windows credential script) are compared byte for byte: a bulk edit of `public` to `internal` must not touch them.

### `Dotenvx.Primitives`

Flat `namespace Dotenvx.Primitives`, one `public static partial class Primitives`, methods named after the real exports. Exports: `decrypt, derive, encrypt, encrypted, evaluate, expand, keypair, keyring, keyringSync, match, parse, parseSync, parsearrays, parsearraysSync, publickeys, remove, scan, sealed, upsert` (C# names: `Decrypt`, `Derive`, ..., `Parse`/`ParseSync`/`ParseArrays`/`ParseArraysSync`, `Scan`, `Sealed`, `Upsert`). `Primitives.Scan` is the one shared JS-faithful line parser. The `DotenvxException` hierarchy lives here too (primitives throws errors.js-shaped errors) but keeps the namespace `Dotenvx.Interop.DotenvxJs.Types`.

Two deliberate shape differences: `Encrypt(value, publicKey)` has the opposite parameter order to JS `encrypt(publicKey, value)` (both are strings, so a silent swap is the risk); `Evaluate` keeps an explicit `key` parameter for error messages.

What stays in Core: dotenvx's own code. `DecryptKeyValue` wraps `Primitives.Decrypt` and adds key names to errors; `ScanToDictionary` is glue over `Primitives.Scan`; `MutateSrc`/`MutateKeysSrc` ship the key header formats. There is no second parse engine and no second key lookup: `Run` (resolvers/envs.js) and `Main.Parse` (main.js) call `Primitives.ParseSync`; `Encrypt`/`Sets`/`Decrypt` are 1:1 ports of `transforms/*.js` built on `PublicKeys`, `Scan`, `Upsert`, `Keypair` and `ParseArraysSync`; `KeyPair` is the `resolvers/keypair.js` keyring lookup. The transforms return `KeysSrc` and the callers write it (default `.env.keys` in the working directory).

C# constraints that shaped this: a `partial class` must live in one assembly, which is why Core call sites carry both `using static Dotenvx.Interop.DotenvxJs.Helpers.Helpers;` and `using static Dotenvx.Primitives.Primitives;`. Inside `namespace Dotenvx.*`, a reference starting `Dotenvx.Primitives...` resolves relative to the enclosing namespace; qualify with `global::`.

`test/Dotenvx.Primitives.Test` references only `Dotenvx.Primitives` (never Core).

### Test traceability convention

Every test should say why it exists, so coverage gaps and stale tests can be found by diffing JS test titles against C# ones.

- Class: `/// <remarks>Reference source: dotenvx/src/lib/....js</remarks>` naming a file that exists at the pin.
- Method: `/// <remarks>Refer to "{exact upstream test title}"</remarks>`, copied literally from the JS `t.test('...')`/`it('...')`.
- No current upstream equivalent: write `No longer a distinct current upstream scenario - historical coverage from the pre-reorg <file>.test.js's "<title>".` or `Not from a JS test - <reason>.` Never leave a remark that implies a match that is not there.
- Every upstream test is cited by an exact `Refer to "<title>"` remark. A test that is intentionally not ported (only after asking) stays as a skipped test (`[Fact(Skip = "Not ported: <reason>")]`, placed inline next to the related tests), so the source is the single record; `node scripts/upstream-test-titles.js` checks this.
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

- Both CLIs run as real subprocesses via `NodeParityFixture`, so the shipped artifact is what is tested. It is a separate project for local speed; CI skips it (it takes 15-45 minutes per OS); run it before a release (the `ci` workflow has a manual `full-parity` option, or run it on each machine).
- Division of labor: `Dotenvx.Test`/`Dotenvx.Core.Test` are the 1:1 ports of upstream's unit tests and stand alone. This suite exists only to catch drift between the two implementations, so add a scenario here only when cross-implementation behavior needs proving.
- Compare stdout and exit code, not stderr wording, unless the scenario is about stderr. Redact non-deterministic content (fresh keypairs, ECIES ciphertext) with `ParityTestBase.RedactKeyMaterial`.
- Deterministic structure tests also live here: `HelpExactParityTest` (exact `--help` bytes for every command path and `help <cmd>`), `HelpTextParityTest` (options per command, against System.CommandLine's native help), `ErrorMessageParityTest` (every error subtype vs `errors.js`), `GlobalOptionsParityTest`.
- Use `FixedKeypairFixture` for deterministic keypairs. Every run gets a hermetic `DOTENVX_CONFIG` (no developer Armor session or device key is ever read); pass `--no-native --no-armor` to Node's `set`/`encrypt` (never in a `RunBothAsync`'s shared args) when the `.env` has no public key yet and no Armor/native behavior is under test.
- Armor, native-store and secret-manager scenarios run against fakes shared by both CLIs: `test/fakes/secretcli.cs` (installed as `op`, `bw`, `powershell` or `secret-tool` by `FakeCliInstaller`), `test/fakes/security.cs` (macOS `security`), `test/fakes/gitleaks.cs` and `FakeArmorServer` (`ArmorParityTestBase` wires config dir, device key, secret store and request log; `Compare` asserts identical exit/stdout/stderr/requests/files).

### Coverage-guided parity

`scripts/parity-coverage.js` shows which upstream lines (and which port lines) the parity suite never executes, so new scenarios can target real gaps.

- `node scripts/parity-coverage.js node [--filter "FullyQualifiedName~X"]` runs the suite with `NODE_V8_COVERAGE` set on every Node child; `node scripts/parity-coverage.js dotnet` does the same for the Dotenvx assemblies; `node scripts/parity-coverage.js report` writes `coverage/node-gaps.txt` and `coverage/dotnet-gaps.txt` (git-ignored).
- Set `COVERAGE_KEEP=1` to accumulate over several invocations. Run `CustodyParityTest` on its own first, then everything else with `COVERAGE_KEEP=1` (one full run once lost the custody coverage). The filter cannot contain `|` (cmd shell): one class per invocation.
- In coverage mode the Node child inherits `NODE_V8_COVERAGE`, so the few tests that dump the whole environment (`get --all`, `get --format eval`, SDK `get all`) fail only there; they pass in normal runs.
- Many remaining gaps are not scenarios to write: dead or unreachable upstream code, guard errors the CLI never reaches, and POSIX-only code (signal forwarding, `ptyCommand`, the Linux secret service) that has to run on Linux/macOS.
- Interactive tests (`PtyParityTest`, `EnquirerParityTest`) need `test/tools/pty` (`npm install` runs on first use; the fixture sets the execute bit on node-pty's `spawn-helper`). Never let a test open a real browser, and always isolate global git config (`GIT_CONFIG_GLOBAL`, `GIT_CONFIG_SYSTEM`, `XDG_CONFIG_HOME`) when exercising `protect`.

Detailed, numbered lessons from past bugs are in [docs/porting-traps.md](docs/porting-traps.md). Skim its headings before touching parsing, option handling, error output, or file encodings; the most reusable ones:

- A JS-vs-.NET regex or string difference is the usual root cause of a mismatch: `\s` and `trim` (BOM, no-break space), `\w` (ASCII-only in JS), multiline line terminators, `String.replace(str, str)` (first match only, `$` patterns), `||` on empty strings. (traps #41, #43, #66)
- `System.CommandLine` differs from commander: empty-array defaults, no variadic by default, recursive options, greedy variadics (traps #15, #36, #39, #42).
- A sweep that tests one input shape can pass hundreds of tests while another shape is broken (repeated vs space-separated flags; single flag vs flag combinations). Static re-reads and live comparisons find different bugs.
- Before changing a shared helper, grep every caller (trap #26). Before concluding something is unreachable or settled, check the submodule's git history and a second scenario (traps #27, #29).
- Do not make a stale test pass: confirm the real behavior live against Node first (trap #51).

## Release cadence (which upstream version to port)

Upstream ships very fast (11 to 17 npm releases a month, 45 in July 2026), and minor versions carry renames and reversals (`Envfile` was renamed twice and reverted within four days in 2.32.x). A byte-for-byte port with live-oracle parity tests cannot follow every release, so:

1. **Target one settled upstream version at a time.** Retarget only to the latest release of a minor line, and only once no newer upstream release has appeared for about **3 days**.
2. **Read the changelog first.** Skip a version that a later release renames, removes or reverts within days; wait for the one that says it is final.
3. **Skip the releases in between.** The .NET packages jump from one settled version to the next, and the changelog names the skipped upstream versions. Do not port unreleased upstream `main`, and ignore npm releases that only change tooling dependencies.
4. **Port everything the target contains.** The scope rule above still applies; a bump is not done until the full ParityTest passes on Windows and Linux (macOS by its peer) and nothing from the target is left unported.
5. **Versions track upstream.** The .NET version equals the upstream version; our own fixes against the same upstream version use the fourth number (next section). `@dotenvx/primitives` and `@dotenvx/providers` follow their own npm versions.
6. **Tag and publish only when the version's work is done.** Never create or move a tag, or publish packages, before the maintainer says so.

## Releasing a fix for the same upstream version

Package versions track the upstream release (`2.35.0`), and nuget.org never lets a published version be replaced. A bug fixed in our port of a version that is already published ships as a four-part version:

- The first three numbers stay the upstream version; the fourth is our own fix counter: `2.35.0` (first release), `2.35.0.1`, `2.35.0.2`. It sorts above the previous release and below the next upstream version, and it disappears again when the port moves to a new upstream version (`2.36.0`).
- Set `<Version>2.35.0.1</Version>` in `Dotenvx.csproj`, `Dotenvx.Core.csproj` and `Dotenvx.Configuration.csproj` (they are released together; Configuration depends on the Core version, so it must move with it). `Dotenvx.Primitives` and `Dotenvx.Providers` follow their own upstream npm versions: a fix there gets a fourth number of its own (`3.3.0.1`).
- `Dotenvx.Configuration` has no Node counterpart, so it has no upstream version of its own: it uses the Core version. A change that only touches Configuration (a new option, a fix) is released as the next fourth number like any other fix, and `Dotenvx.Configuration` is published only when its own contents changed.
- In `Dotenvx.csproj` also set `<InformationalVersion>2.35.0</InformationalVersion>`. `dotenvx --version` reads it and must keep printing exactly the upstream version (`GlobalOptionsParityTest.RootVersionFlagMatchesExactly`).
- The commit is recorded by packing (`<repository commit=...>` in the nuspec). To also put it in the version, pass build metadata when packing: `dotnet pack -p:Version=2.35.0.1+abc1234`. NuGet ignores the part after `+` when ordering versions and drops it from the file name. A commit hash cannot be the fourth number: it must be an integer, and commits do not increase in order.
- Try a release before publishing it with a pre-release suffix (`2.35.0.1-rc.1`); it sorts below `2.35.0.1`.
- Never reuse or move a published version. If a published package is bad, unlist it on nuget.org and release the next fourth number.

## Version bump checklist (retarget dotenvx and/or @dotenvx/primitives)

Goal: retarget, run the suites, and let the failures plus a few diffs say exactly what to update. Work top to bottom; do not skip step 2 (a green suite only proves what is already asserted).

**1. Retarget**
1. `cd dotenvx && git fetch --tags && git checkout <new-tag>` (exact tag), then `git add dotenvx` from the repo root.
2. `Version` = the new dotenvx tag in `src/Dotenvx/Dotenvx.csproj`, `src/Dotenvx.Core/Dotenvx.Core.csproj` and `src/Dotenvx.Configuration/Dotenvx.Configuration.csproj` (they are released together). A new upstream version starts again without a fourth number; see "Releasing a fix for the same upstream version".
3. `cd dotenvx && npm install --no-audit --no-fund --omit=dev`, read the installed `@dotenvx/primitives` version, set `src/Dotenvx.Primitives/Dotenvx.Primitives.csproj` `Version` to it, and update the version numbers quoted in this file.
4. Snapshot primitives function sources before and after the bump, from inside `dotenvx/`: `node -e "const p=require('@dotenvx/primitives');const o={};for(const k of Object.keys(p))o[k]=p[k].toString();require('fs').writeFileSync(process.argv[1],JSON.stringify(o,null,1))" <tmp>/primitives-<ver>.json`.

**2. Find what upstream changed**
1. `Object.keys(require('@dotenvx/primitives'))` against the export list above: a new export needs a `Primitives.*` counterpart plus oracle rows; a removed one needs a decision.
2. Diff the two snapshots from 1.4. Each changed export means re-reading it, updating our port, and adding oracle rows.
3. Diff the primitives `README.md`: every example, option table and operator list. Replay new or changed examples in `PrimitivesExportsOracleParityTest.ReadmeExamples...` and extend its option matrices.
4. dotenvx internals, inside `dotenvx/`: `git log --oneline <old>..<new>`, `git diff <old> <new> --stat -- src tests CHANGELOG.md`, then read the real hunks one file at a time (`git diff <old> <new> -- src/lib/<file>.js`; also `src/cli`, `src/shared`, `src/lib/helpers/errors.js`, `tests/**`; add `-M` for renames). Map each changed `src/**` file to its C# port through the `Reference source:` comments and re-port the hunks; map each changed or new test title through the `Refer to "..."` remarks, or run `node scripts/upstream-test-titles.js [file-fragment]` from the repo root: it lists every upstream test title no C# test cites (port it, or, after asking, add a skipped test with the reason).
5. Primitives internals: keep the old tarball (`npm pack @dotenvx/primitives@<old>`), pretty-print both `dist/index.cjs` files (`npx prettier --parser babel`), and `git diff --no-index` them. Vendored crypto changes matter only if ciphertext format, key derivation or error messages change (rerun the keypair/encrypt/decrypt oracles); engine changes (scan/parse/expand/keyring/upsert/remove/sealed) need the matching C# edit.
6. New or changed commands and flags: diff the `dotenvx.js` registrations and run `dotenvx <cmd> --help` per command. Re-check commander shapes (`<x>` vs `<x...>`, defaults, negations).
7. Re-check version-dependent scope notes: native-store default, `precommit`/`prebuild` to `protect`, Armor/cloud, `ext` additions.
8. `UnreadOptionsSentinelTest` (in `Dotenvx.ParityTest`) fails when upstream starts reading an option we left out on purpose (`parity-checklist.md`, accepted difference 13): port the option as a real property, add it to the parity scenarios, and remove it from the test and that list.
9. Regenerate the help spec: `node scripts/gen-help-spec.js` rewrites `src/Dotenvx/Help/help-spec.json` (every command's usage, description, option/argument/subcommand rows and pad width, as commander computes them); commit the diff and read it for new or changed flags. `--help` output comes from that file, so a stale spec fails `HelpExactParityTest`.

**3. Run the suites; read failures as the to-do list**
1. `dotnet build`, then `Dotenvx.Primitives.Test`, `Dotenvx.Core.Test`, `Dotenvx.Configuration.Test`, `Dotenvx.Test`, `Dotenvx.ParityTest`, each separately.
2. Run `node scripts/parity-sweep.js` (about 12 min): ~2300 command x flag x fixture scenarios against both CLIs. Differences land in `$TMP/dotenvx-parity-sweep.txt`; only the `ACCEPTED` list in the script may remain, each with a reason.
3. Meaning of failures: the two primitives oracle tests = our port drifted (output prints Node vs C# per input; fix the port, add a table row); `ErrorMessageParityTest` = `errors.js` changed (new codes need a `DotenvxException` subtype); `HelpExactParityTest` = `--help` bytes differ (regenerate the spec, or a command is missing from it); `HelpTextParityTest` = flags or descriptions of the real `System.CommandLine` options changed (it runs the .NET CLI with `DOTENVX_NATIVE_HELP=1` to see them; fix the option, or extend its documented exclusion lists with a reason); `GlobalOptionsParityTest.RootVersionFlagMatchesExactly` = version lockstep broken; other `*ParityTest` = CLI drift; Core/Dotenvx unit tests = ported tests encoding old behavior (confirm the new behavior live before rewriting one).
4. Constructor changes breaking mocks: see "Static seams" above.

**4. Close the gaps the suites cannot see**
1. Re-run the `--help` diff per command, flag-combination and space-separated-vs-repeated multi-value sweeps for any new flag, and a static re-read of every action or service whose upstream file changed.
2. Port new upstream tests under the traceability convention, or record why not (out of scope, no gap).
3. Re-verify any "known difference" claimed in docs before relying on it; they go stale silently.

**5. Record it**
1. Update the pinning notes and primitives version here, and add a numbered trap to `docs/porting-traps.md` for anything non-obvious (what, why, how to apply).

## CLI logging

`src/Dotenvx/Logging/` is a custom `ILogger`/`ILoggerProvider` (`DotenvxLogger`, `DotenvxCustomLogger*`) wired via `LoggingBuilderExtensions.AddDotenvx(...)`, matching dotenvx's leveled, colored output (quiet/error/info/verbose/debug) through `IDotenvxLogger` (`Interop.DotenvxJs/Shared/IDotenvxLogger.cs`). `DotenvxRootCommand.GetDotenvxLoggerConfiguration` pre-parses `args` once, silently, to pick the level before the host and command run. Only `info` goes to stdout; every other level goes to stderr (keeps stdout script-safe).

## Line endings and formatting

- The repo is LF everywhere (`.gitattributes`: `* text=auto eol=lf`), except `*.sln` (CRLF). C# raw and verbatim strings keep the file's newlines, so LF keeps test expectations identical on every OS.
- The `dotenvx/` submodule is outside our `.gitattributes`: run `git -C dotenvx config core.autocrlf false` before checking it out on Windows. `FixtureLineEndingsTest` fails if a fixture (other than `.env.crlf`, `.env.latin1`, `.env.utf16le`) contains ``.
- Run `dotnet format` before committing; CI runs `dotnet format --verify-no-changes`.

## Source encoding cautions

- Some editors and generators rewrite literal Unicode-escape text (a backslash, `u` and four hex digits) into the character itself, and a raw U+2028 in C# source is a line terminator. Build such text from `(char)` values (or write it with a tool that does not interpret escapes) and verify with `grep`.
- Shell heredocs can alter backslashes; prefer `(char)92` in C# and token substitution in test tables.
