# Agent rules

Read [CONTRIBUTING.md](CONTRIBUTING.md) first; it is the source of truth for how this repository works. The rules below are the ones agents most often get wrong.

## Rollout rules

- **Which upstream version:** follow "Release cadence" in CONTRIBUTING.md. Target the latest release of a minor line only after about 3 quiet days and a changelog check; skip the versions in between; never track upstream `main`.
- **Scope:** byte-for-byte parity with the targeted `@dotenvx/dotenvx` version. Never skip or defer a feature without the maintainer's explicit permission. Differences are allowed only where the OS or its error handling forces them, and Node-specific error text is not emulated.
- **Done means:** the full `test/Dotenvx.ParityTest` suite passes on Windows and Linux (macOS is run by its peer machine), and nothing in the target version is left unported. The commit that gets published must have a green full-parity run on every OS, from the `ci` workflow started by hand with `full-parity` ticked or from the parity machines.
- **Versions:** the .NET version equals the upstream version; our own fixes use a fourth number (`2.35.0.1`), and `dotenvx --version` must still print the exact upstream version. See "Releasing a fix for the same upstream version".
- **Tags, pushes and packages:** commit and push only when asked. Never create or move a version tag until the maintainer says the version is done. Publishing to nuget.org stays with the maintainer.

## Testing safety

- Unit tests never use live external sources, and a test never opens a real browser.
- Manual CLI runs use `DOTENVX_NO_NATIVE=true` and an isolated `DOTENVX_CONFIG`.
- Anything that touches the `protect` family must isolate global git config (`GIT_CONFIG_GLOBAL`, `GIT_CONFIG_SYSTEM`, `XDG_CONFIG_HOME`).
