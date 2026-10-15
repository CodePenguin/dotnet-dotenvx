# Parity checklist: dotenvx v2.35.0

The goal is byte-for-byte parity with the Node `@dotenvx/dotenvx` CLI at the pinned tag: **anything that exists in that version is in scope.** This file lists every upstream component, whether it is ported, and how it is verified. A component is "done" when it is ported, covered by unit tests ported from `dotenvx/tests` (or by oracle/parity tests when upstream has none) and compared against the real Node CLI by a parity test. Update this file in the same change as the code.

## Verification baseline

| Check | Command | What it proves |
| --- | --- | --- |
| Unit tests | `dotnet test test/<project>` (Primitives, Core, Configuration, Test) | ported upstream unit tests and our own, including a check that the command tree equals upstream's |
| Parity tests | `dotnet test test/Dotenvx.ParityTest` (about 10 min) | scripted scenarios run against both real CLIs, including exact `--help` bytes for every command |
| Differential sweep | `node scripts/parity-sweep.js` (about 20-40 min per OS; manual only, see "Platforms and CI") | ~15000 command x flag x fixture combinations and multi-step lifecycles, plus every option of every offline command, comparing exit code, stdout, stderr and files |
| Upstream test titles | `node scripts/upstream-test-titles.js` | every upstream test is ported or listed in `docs/upstream-tests-not-ported.md` |
| Primitives oracle | `PrimitivesOracleParityTest`, `PrimitivesExportsOracleParityTest` | every `@dotenvx/primitives` export against the real package |

## Commands

| Command | Status | Notes |
| --- | --- | --- |
| `run` | done | including `op://` / `bw://` secret references and `--redact`/`--mask`/`--profile`, summary `⟐ injected env (N)` |
| `get` | done | |
| `set` | done | hidden-value prompt on a terminal, key-storage selection, `--no-armor`/`--no-native` |
| `del` | done | |
| `encrypt` (`enc`), `decrypt` (`dec`) | done | `enc`/`dec` are hidden aliases; `encrypt --with <public-key>` (2.35.0) |
| `keypair` | done | |
| `ls` | done | file order can differ on some filesystems (trap #64) |
| `genexample` | done | |
| `protect` (`--docker`, `--git-file`, `--git-process`) | done | git filter/attributes/ignore installed in an isolated git config in tests; the interactive checklist uses its defaults |
| `gitignore`, `precommit` (`--install`, `--uninstall`), `prebuild` | done | deprecated in favor of `protect`; print the deprecation warning |
| `check`, `spec` | done | hidden beta commands; `spec` multiselect uses its defaults |
| `primitives` (`profile`, `redact`, `policy`, `check`, `keypair`, `derive`) | done | |
| `feedback`, `hidden` | done | `hidden` lists the hidden commands |
| `help [command]` | done | byte-identical to commander's help |
| `doctor` | done | hidden |
| `update` | done | prints the npm commands (never a packaged binary) |
| `native`, `1password`, `bitwarden` (`up`, `down`, `push`, `pull`) | done | every store shells out like upstream (`security`, `secret-tool`, PowerShell with upstream's script) |
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
9. A child killed by `SIGUSR1` makes Node open its inspector on port 9229, and both runs race for that port; only the stderr text can differ (`ACCEPTED_STDERR_ONLY` in the sweep).
10. macOS only: Node exits before a large write (over about 64 KB) to a pipe drains and cuts the output (`encrypt --stdout` of a 600-key file stops near key 417); .NET prints all of it. The sweep accepts `r16 many keys encrypt --stdout` on macOS only. Writing to a file or a terminal is complete in both.
11. Linux and macOS: the real Keychain and Secret Service stay behind fakes (see 5); on Windows the Credential Manager is also tested for real.
12. Interactive menus (custody, `spec`, `protect`, feedback, passwords) run on a port of enquirer (`Interop.EnquirerJs`): `EnquirerParityTest` (piped keys, byte-identical stderr) and `PtyParityTest` (real pseudo-terminal, screens compared cell by cell) check them against Node. Verified on Windows; Linux/macOS runs pending.

## Platforms and CI

- **CI** (`.github/workflows/ci.yml`) runs restore, build, `dotnet test` (unit tests and `Dotenvx.ParityTest`) and pack on Linux only, on push to `main`, pull requests and tags. The `all-os` input of a manual run adds Windows and macOS. The sweep never runs in CI.
- **Sweep** (`.github/workflows/sweep.yml`, manual only, or `node scripts/parity-sweep.js` locally): ~15,100 scenarios per OS. Status at `99e78d9`/`36f4a4b`: Windows, Linux (WSL2, Node 22) and macOS (arm64, Node 25) 0 unexplained differences. The macOS full run showed 5 load-induced flukes (all pass alone) and the pipe truncation in 10.
- **macOS** skips scenarios that use native stores (`/usr/bin/security` cannot be shadowed on PATH).
- **Test isolation**: `ConfigExpandTest` clears `PGUSER`/`PGHOST` (a Windows runner defines `PGUSER` machine-wide and dotenvx never overrides existing variables).
- **Linux box notes**: without libicu set `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`; fakes need `DOTNET_ROOT`; the parity fixture uses `DOTNET_HOST_PATH` to find `dotnet`.
- **Not verifiable here**: a real TTY, real services and browsers, signals delivered through a terminal.
