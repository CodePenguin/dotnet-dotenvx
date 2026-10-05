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
| `run` | done | |
| `get` | done | |
| `set` | partial | interactive hidden-value prompt on a TTY and key-storage selection missing (Phase F) |
| `del` | done | |
| `encrypt` (`enc`) | partial | key-storage selection prompt, `--no-armor`/`--no-native` flags missing (Phases D/E/F); `enc` shown in help, hidden upstream |
| `decrypt` (`dec`) | partial | provider/decryptor (Armor, native) paths missing |
| `keypair` | partial | provider path missing |
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
| `native` (`up`, `down`, `push`, `pull`) | todo | Phase D |
| `armor` (`up`, `down`, `push`, `pull`, `open`, `move`, `status`, `settings`) | todo | Phase E |
| `login`, `logout` | todo | Phase E |
| `curl` | todo | Phase E |
| `ext` (`ls`, `genexample`, `gitignore`, `prebuild`, `precommit`, `scan`) | done | |
| `ext <name>` dynamic extensions (`executeExtension`) | todo | Phase F |
| `rotate` | not upstream | forward-ported ahead of the pin (historical commit `f1ffd157`); hidden from the pin's help is not possible (trap #17) |

## Flags upstream has that we do not

`--no-armor`, `--no-native`, `--no-1password`, `--no-bitwarden`, `--token` on commands that resolve keys
(`HelpTextParityTest.NodeOnlyAliases` is the current exclusion list; it must shrink to empty as phases land).

## Phases

| Phase | Scope | Upstream source | How to verify without a real service |
| --- | --- | --- | --- |
| A | `help [command]`, `doctor`, `update`, hidden `enc`/`dec`, root help layout | `cli/actions/{doctor,update}.js`, `services/doctor.js`, `cli/dotenvx.js` | parity tests against Node |
| B | `lock up/down` (local passphrase) | `cli/actions/lock/*`, `services/lock{Up,Down}.js`, `cli/commands/lock.js` | round trip with Node both ways; `INVALID_PASSPHRASE` |
| C | 1Password and Bitwarden secret resolution (`op`, `bw`) | `helpers/resolveOnePassword.js`, `resolveBitwardenPassword.js`, `config-onepassword/bitwarden` tests | fake `op`/`bw` executables on PATH, same for both CLIs |
| D | Native secret stores and `native` command | `helpers/{macosKeychain,linuxSecretService,windowsCredentialManager}.js`, `services/keychain*.js`, `providers/native`, `cli/actions/keychain/*` | fake `security`/`secret-tool`/PowerShell shims; real Credential Manager on Windows locally |
| E | Armor: device and session store, API client, OAuth login/logout, armor commands, providers and decryptors, `curl`, token handling | `db/*`, `lib/api/*`, `services/armor*.js`, `login.js`, `logout.js`, `providers/armor`, `decryptors/armor`, `cli/actions/armor/*`, `curl.js`, `settings` | local fake HTTP server shared by Node and .NET runs, `HOME`/config dir overridden |
| F | Interactive prompts, spinner and status text, pty for `--redact`, full signal forwarding, dynamic extensions, set/encrypt key-storage selection | `helpers/{prompts,createSpinner,createElapsedStatus,ptyCommand,executeExtension,executeDynamic}.js` | scripted stdin, TTY emulation where possible |
| G | Remove the out-of-scope notes and exclusion lists; every upstream test titled and ported; sweep extended to the new commands | `docs/upstream-tests-not-ported.md` must become empty | `node scripts/upstream-test-titles.js` prints nothing without the skip pattern |

## Working method per phase

1. Read the upstream source and its tests; run the real Node command to see exact output.
2. Port with the traceability remarks (`Reference source:` / `Refer to "<title>"`).
3. Add parity tests (and extend `scripts/parity-sweep.js`) for every flag combination.
4. Move the phase's test files out of `docs/upstream-tests-not-ported.md` and the skip pattern in
   `scripts/upstream-test-titles.js`.
5. Update the rows above and the root `--help` comparison; commit.
