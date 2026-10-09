# Parity checklist: dotenvx v2.24.1

The goal is byte-for-byte parity with the Node `@dotenvx/dotenvx` CLI at the pinned tag: **anything that exists in that version is in scope.** This file lists every upstream component, whether it is ported, and how it is verified. A component is "done" when it is ported, covered by unit tests ported from `dotenvx/tests` (or by oracle/parity tests when upstream has none) and compared against the real Node CLI by a parity test. Update this file in the same change as the code.

## Verification baseline

| Check | Command | What it proves |
| --- | --- | --- |
| Unit tests | `dotnet test test/<project>` (Primitives, Core, Configuration, Test) | ported upstream unit tests and our own, including a check that the command tree equals upstream's |
| Parity tests | `dotnet test test/Dotenvx.ParityTest` (about 10 min) | scripted scenarios run against both real CLIs, including exact `--help` bytes for every command |
| Differential sweep | `node scripts/parity-sweep.js` (about 12 min) | ~9000 command x flag x fixture combinations and multi-step lifecycles, plus every option of every offline command, comparing exit code, stdout, stderr and files |
| Upstream test titles | `node scripts/upstream-test-titles.js` | every upstream test is ported or listed in `docs/upstream-tests-not-ported.md` |
| Primitives oracle | `PrimitivesOracleParityTest`, `PrimitivesExportsOracleParityTest` | every `@dotenvx/primitives` export against the real package |

## Commands

| Command | Status | Notes |
| --- | --- | --- |
| `run` | done | including `op://` / `bw://` secret references and `--redact`/`--mask`/`--validate` |
| `get` | done | |
| `set` | done | hidden-value prompt on a terminal, key-storage selection, `--no-armor`/`--no-native` |
| `del` | done | |
| `encrypt` (`enc`), `decrypt` (`dec`) | done | `enc`/`dec` are hidden aliases |
| `keypair` | done | |
| `ls` | done | file order can differ on some filesystems (trap #64) |
| `validate` | done | |
| `gitignore`, `genexample`, `precommit`, `prebuild` | done | |
| `help [command]` | done | byte-identical to commander's help |
| `doctor` | done | hidden |
| `update` | done | prints the npm commands (never a packaged binary) |
| `lock` (`up`, `down`) | done | scrypt implemented in `LockCrypto` (no .NET built-in) |
| `native` (`up`, `down`, `push`, `pull`) | done | every store shells out like upstream (`security`, `secret-tool`, PowerShell with upstream's script) |
| `armor` (`up`, `down`, `push`, `pull`, `open`, `move`, `status`, `settings`) | done | verified against a fake Armor API; falls back to a `dotenvx-armor` program like upstream |
| `login`, `logout`, `curl` | done | OAuth device flow against the fake API |
| `ext` (`ls`, `genexample`, `gitignore`, `prebuild`, `precommit`, `scan`) | done | |
| `ext <name>` dynamic extensions | done | `dotenvx-ext-<name>` from PATH, with upstream's vault install hint |

Every flag upstream registers is ported (`--no-armor`, `--no-native`, `--no-1password`, `--no-bitwarden`, `--token` included); `CommandTreeParityTest` compares commands, options and arguments with the generated upstream command tree.

## Accepted differences

All verified against the real CLI; none hides a behavior gap.

1. Error text that comes from the operating system or the runtime rather than from dotenvx's own source (Node's `ENOENT: ...`, uncaught-error stack traces, `'x' is not recognized ...`) stays native to .NET; dotenvx's own messages and error codes are matched.
2. A dash-leading value for a variadic option (`run -e --x`) is rejected as a missing value; the other option-syntax shapes match (trap #75).
3. Prompt and spinner rendering on stderr (enquirer and yocto-spinner redraw sequences) is not byte-identical; prompt behavior (keys, Enter, end of input, input draining) is.
4. `debug` output (`-d`, `-l debug`) has a different shape.
5. The real OS secret stores (macOS Keychain, Linux Secret Service) are exercised in tests only through fakes: `secret-tool` and `powershell` on PATH, and for the absolute `/usr/bin/security` a redirect of both implementations' process call (`MacosKeychainParityTest`); the commands, and the PowerShell script on Windows, are the same as Node's. The Windows Credential Manager is also covered by a test against the real store (Windows only).
6. POSIX signal forwarding (`SignalForwarder`) and the `script`-based pseudo-terminal are unit-tested through seams; real signals need a Unix host.
7. Browser opening (`armor open`, `login`) is tested through the `OpenUrl` seam, never against a real browser.
8. `ls` file order follows an ordinal sort instead of the filesystem crawl order.
