# Parity checklist: dotenvx v2.24.1

Rule (decided 2026-10-04): **anything that exists in the targeted version is in scope.** This file lists every upstream
component, whether it is ported, and how it is verified. A component is only "done" when it is ported, covered by unit
tests ported from `dotenvx/tests` (or by oracle/parity tests when upstream has none) and compared against the real Node
CLI by a parity test. Update this file in the same commit that changes a row.

Legend: `done` ported and verified, `partial` some behavior missing, `todo` not started.

## Verification baseline (always run before claiming parity)

| Check | Command | What it proves |
| --- | --- | --- |
| Unit tests | `dotnet test test/<project>` (Primitives, Core, Configuration, Test) | ported upstream unit tests and our own |
| Parity tests | `dotnet test test/Dotenvx.ParityTest` (about 5 min) | scripted scenarios against both real CLIs |
| Differential sweep | `node scripts/parity-sweep.js` (about 4 min) | ~590 command x flag x fixture combinations, exit/stdout/stderr/files |
| Upstream test titles | `node scripts/upstream-test-titles.js` | every upstream test is ported or listed in `docs/upstream-tests-not-ported.md` |
| Primitives oracle | `PrimitivesOracleParityTest`, `PrimitivesExportsOracleParityTest` | every primitives export against the real package |

## Commands (top level, `dotenvx --help` at the pin)

| Command | Status | Notes |
| --- | --- | --- |
| `run` | done | `--no-1password`/`--no-bitwarden` and `op://`/`bw://` resolution included (Phase C); the "awaiting ..." status text is Phase F |
| `get` | done | |
| `set` | done | hidden-value prompt on a TTY, key-storage selection, `--no-armor`/`--no-native` |
| `del` | done | |
| `encrypt` (`enc`) | done | `enc` is shown in help (hidden upstream; System.CommandLine cannot hide an alias, trap #17) |
| `decrypt` (`dec`) | done | `dec` shown in help, see `enc` |
| `keypair` | done | |
| `ls` | done | file order differs on some filesystems (trap #64) |
| `validate` | done | |
| `gitignore` | done | |
| `genexample` | done | |
| `precommit` | done | |
| `prebuild` | done | |
| `help [command]` | done | text is the System.CommandLine renderer (accepted) |
| `doctor` | done | hidden |
| `update` | done | always prints the npm commands (never a pkg binary) |
| `lock` (`up`, `down`) | done | scrypt implemented in `LockCrypto` (no .NET built-in); prompt stderr rendering is not byte-identical to enquirer; the `preAction` Armor update check is Phase E |
| `native` (`up`, `down`, `push`, `pull`) | done | every store shells out like upstream (`security`, `secret-tool`, `powershell.exe` + upstream script); tested with a fake tool on PATH, real OS stores untested here (Credential Manager writes fail on the dev machine) |
| `armor` (`up`, `down`, `push`, `pull`, `open`, `move`, `status`, `settings`) | done | verified against a fake Armor API; `open` launches a browser (unit tests only); upstream `ext`-style dynamic fallback to `dotenvx-armor` included |
| `login`, `logout` | done | OAuth device flow against the fake API |
| `curl` | done | |
| `ext` (`ls`, `genexample`, `gitignore`, `prebuild`, `precommit`, `scan`) | done | |
| `ext <name>` dynamic extensions (`executeExtension`) | done | `dotenvx-ext-<name>` from PATH, vault install hint |

## Flags upstream has that we do not

None: `--no-armor`, `--no-native`, `--no-1password`, `--no-bitwarden` and `--token` are ported on every command that has them upstream (`HelpTextParityTest` no longer has an exclusion list).

## Phases

| Phase | Scope | Upstream source | How to verify without a real service |
| --- | --- | --- | --- |
| A (DONE) | `help [command]`, `doctor`, `update`, hidden `enc`/`dec`, root help layout | `cli/actions/{doctor,update}.js`, `services/doctor.js`, `cli/dotenvx.js` | parity tests against Node |
| B (DONE) | `lock up/down` (local passphrase) | `cli/actions/lock/*`, `services/lock{Up,Down}.js`, `cli/commands/lock.js` | round trip with Node both ways; `INVALID_PASSPHRASE` |
| C (DONE) | 1Password and Bitwarden secret resolution (`op`, `bw`) - DONE | `helpers/resolveOnePassword.js`, `resolveBitwardenPassword.js`, `config-onepassword/bitwarden` tests | fake `op`/`bw` executables on PATH, same for both CLIs |
| D (DONE) | Native secret stores and `native` command | `helpers/{macosKeychain,linuxSecretService,windowsCredentialManager}.js`, `services/keychain*.js`, `providers/native`, `cli/actions/keychain/*` | fake `security`/`secret-tool`/PowerShell shims; real Credential Manager on Windows locally |
| E (DONE) | Armor: device and session store, API client, OAuth login/logout, armor commands, providers and decryptors, `curl`, token handling | `db/*`, `lib/api/*`, `services/armor*.js`, `login.js`, `logout.js`, `providers/armor`, `decryptors/armor`, `cli/actions/armor/*`, `curl.js`, `settings` | local fake HTTP server shared by Node and .NET runs, `HOME`/config dir overridden |
| F (DONE) | Interactive prompts, spinner and status text, pty for `--redact`, full signal forwarding, dynamic extensions, set/encrypt key-storage selection | `helpers/{prompts,createSpinner,createElapsedStatus,ptyCommand,executeExtension,executeDynamic}.js` | scripted stdin, TTY emulation where possible |
| G (DONE) | Remove the out-of-scope notes and exclusion lists; every upstream test titled and ported; sweep extended to the new commands | `docs/upstream-tests-not-ported.md` must become empty | `node scripts/upstream-test-titles.js` prints nothing without the skip pattern |

## Working method per phase

1. Read the upstream source and its tests; run the real Node command to see exact output.
2. Port with the traceability remarks (`Reference source:` / `Refer to "<title>"`).
3. Add parity tests (and extend `scripts/parity-sweep.js`) for every flag combination.
4. Move the phase's test files out of `docs/upstream-tests-not-ported.md` and the skip pattern in
   `scripts/upstream-test-titles.js`.
5. Update the rows above and the root `--help` comparison; commit.

## Accepted differences

0. `run -e --x` / `run -e -f` (a dash-leading value for a variadic option) is rejected as a missing value here; the rest of the option-syntax shapes match (trap #75, `shape` scenarios in the sweep). (all verified live, none hide a behavior gap)

1. (Resolved) `--help` is byte-identical: `src/Dotenvx/Help/CommanderHelp.cs` lays out `help-spec.json` (dumped from the real commander tree) with commander's wrapping, verified for every command path and `help <cmd>` by `HelpExactParityTest`.
2. Prompt and spinner rendering on stderr (enquirer / yocto-spinner redraw sequences) is not byte-identical; prompt behavior (keys, Enter, EOF, input draining) is.
3. `debug` output (`-d`, `-l debug`) has a different shape.
4. Real OS secret stores (macOS Keychain, Linux Secret Service, Windows Credential Manager) are exercised only through fake tools on PATH in tests; the commands and the upstream PowerShell script are the same as Node's.
5. POSIX signal forwarding (`SignalForwarder`) is unit-tested through seams only; it was written on a Windows machine.
6. `armor open` and the `login`/`armor` browser opening are tested through the `OpenUrl` seam, never against a real browser.
