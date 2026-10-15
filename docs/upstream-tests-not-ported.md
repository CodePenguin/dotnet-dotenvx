# Upstream tests not ported

Every upstream test under `dotenvx/tests` is either ported (cited by an exact `Refer to "<title>"` remark in `test/**`) or listed here with the reason. Generated from the submodule at the pinned tag; regenerate with the title-diff script described in CONTRIBUTING.md when the pin moves.

## tests/lib/helpers/prompts.test.js

Assertions on the argument object handed to the `enquirer` library (`type: 'select'`, normalized `choices`, `stdin`/`stdout`). This port has no enquirer: `Prompts.Select`/`Prompts.Password` read the terminal (or piped stdin) themselves. Their behavior is proven live against Node by LockParityTest, ArmorKeysParityTest and ArmorProviderParityTest (arrow keys, Enter, EOF, chunk draining).

- "select uses enquirer with normalized choices and IO context"
- "select does not require IO context"

## tests/cli/actions/armor/{up,push}.test.js

Both "falls back to private key name when public key display is unavailable" tests stub the service to return a `null` public key. The real `ArmorUp`/`ArmorPush` always resolve and verify the public key (`ResolveLocalKey`), so the state is unreachable here; the fallback is a one-line branch in the action.

- "armor up falls back to private key name when public key display is unavailable"
- "armor push falls back to private key name when public key display is unavailable"

## tests/cli/actions/feedback.test.js (TTY scenarios)

Both need a real TTY on stdin (`process.stdin.isTTY = true`), which the test host cannot provide. Their cases are compared against the real Node CLI instead.

- "prompts in a terminal and supports hostname override" (the hostname half is `FeedbackActionTest.TestHostnameOverride`)
- "cancellation exits without submitting"

## tests/lib/custodians/index.test.js (plugin registry)

Upstream builds registries from plugin objects (`createRegistry`, custom custodians, ordered lookup, disabled/unconfigured probing, synchronous-read validation). This port's registry is the fixed built-in set (`file`, `native`, `armor`, `1password`, `bitwarden`) with no plugin API; the fallback and failure rules are covered by `CustodiansTest`.

- "custom custodians participate in selection, writes and ordered lookup"
- "disabled custodians are never probed or read; unconfigured ones are not read"
- "invalid registration and unsupported synchronous reads fail explicitly"

## tests/lib/decryptors/index.test.js, tests/lib/resolvers/keypair.test.js

These stub JS module internals (`proxyquire` of the decryptor factory, the worker-thread `sync` variant, the resolver's provider chain) that have no counterpart here: key lookup runs through `KeyProviders` and the `Dotenvx.Providers` assembly, and Armor decryption through `ParseWithDecryptor`. The same behaviors (file keys before providers, `--no-armor`, provider origin reporting, Armor keyring requests with the Bearer token, unchanged output when Armor is off) are proven against the real Node CLI by ArmorProviderParityTest, NativeStoreParityTest and the keypair/get/decrypt parity tests.

- "decryptors returns a custom decryptor unchanged"
- "decryptors returns null when Armor is unavailable or disabled"
- "decryptors returns an Armor decryptor"
- "decryptors.sync runs the Armor decryptor through a worker"
- "keypair ${mode} reads envKeysFilepath before other providers"
- "keypair ${mode} handles missing keys when no provider is enabled"
- "keypair ${mode} still reads file keys with noArmor"
- "keypair ${mode} falls back to a provider when the file has no matching key"
- "keypair ${mode} forwards onStatus and reports the provider origin"

## tests/lib/providers/native.test.js

The raw OS-store reads are tested at the `Dotenvx.Providers` level (`NativeStoreTest`: Keychain, Credential Manager, Secret Service command lines and results); the delegation test stubs `@dotenvx/providers` itself.

- "native backend reads raw secrets from macOS Keychain on darwin"
- "native backend reads raw secrets from Windows Credential Manager on win32"
- "native backend reads raw secrets from Linux Secret Service on linux"
- "native provider delegates ${method} key lookup to @dotenvx/providers"

## tests/e2e/output.test.js

- "command outcomes preserve piped stdout and stderr" drives the JS `commandAction` wrapper directly; the .NET CLI's piped output is compared byte-for-byte with Node's by every parity test.
