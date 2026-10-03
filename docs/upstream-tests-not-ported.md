# Upstream tests not ported

Every upstream test under `dotenvx/tests` is either ported (cited by an exact `Refer to "<title>"` remark in `test/**`) or listed here with the reason. Generated from the submodule at the pinned tag; regenerate with the title-diff script described in AGENTS.md when the pin moves.

## tests/cli/commands/ext.test.js

Dynamic `dotenvx-ext-*` extension binaries and lazy-loaded ext handlers (executeExtension). Not ported: there is no external-extension mechanism in this port.

- "ext dynamic action forwards to executeExtension"
- "ext subcommands lazy-load handlers and preserve this context"
- "ext helpInformation removes dynamic section"

## tests/cli/examples.test.js

The "Examples:" / "Try it:" doc-string trailers of `--help` (pure illustrative text, no behavior; see docs/porting-traps.md). Per-command descriptions and options are compared by HelpTextParityTest instead.

- "run function returns expected string"
- "precommit function returns expected string"
- "prebuild function returns expected string"
- "gitignore function returns expected string"
- "set function returns expected string"
- "curl function returns API endpoints and examples"

## tests/lib/helpers/errors.test.js

Only the 1Password and Bitwarden factories (secret-manager resolution is out of scope); every other errors.js test is ported in ErrorsTest.

- "#errors custom with code auto-fix and debug"
- "#errors custom with explicit help and no code"
- "#errors missingEnvFile falls back to .env"
- "#errors validationFailed"
- "#errors onePasswordFailed"
- "#errors bitwardenFailed"
- "#errors missingEnvKeysFile falls back to .env.keys"
- "#errors missingPublicKey"
- "#errors fileNotWritable"

## tests/lib/helpers/parseWithDecryptor.test.js

The provider/decryptor wrapper around primitives parse: it exists for Dotenvx Armor key providers and server-side decryption, which are out of scope.

- "parseWithDecryptor preserves a real encrypted parse when the Armor provider denies access"
- "parseWithDecryptor delegates server-side decryption and reparses plaintext"
- "parseWithDecryptor rethrows errors that do not request server-side decryption"
- "parseWithDecryptor preserves encrypted src when a key provider fails"
- "parseWithDecryptor preserves encrypted src and the error when the decryptor is offline"
- "parseWithDecryptor preserves encrypted src and the permission error when Armor access is denied"
- "parseWithDecryptor.sync delegates through a synchronous decryptor"
- "parseWithDecryptor.sync preserves encrypted src when a key provider fails"
- "parseWithDecryptor.sync preserves encrypted src and the permission error when Armor access is denied"
- "parseWithDecryptor.arrays preserves duplicate assignments: ${scenario}"

## tests/lib/helpers/readEnvKey.test.js

Used only by the Armor/keychain services (out of scope).

- "readEnvKey reads a key from an env file without decrypting"
- "readEnvKey respects ignored missing key errors"

## tests/lib/helpers/removeEnvKey.test.js

Used only by the Armor/keychain services (out of scope).

- "#removeEnvKey removes key from existing .env.keys file"
- "#removeEnvKey removes duplicate and export-prefixed entries"
- "#removeEnvKey preserves CRLF line endings"
- "#removeEnvKey does not rewrite file when key is already absent"
- "#removeEnvKey leaves an empty .env.keys file unchanged"
- "#removeEnvKey deletes file when removing the last key"
- "#removeEnvKey deletes file when only comments remain after removing last key"
- "#removeEnvKey returns unchanged when .env.keys is missing"

## tests/lib/helpers/upsertEnvKey.test.js

Used only by the Armor/keychain services (out of scope).

- "#upsertEnvKey creates .env.keys when missing"
- "#upsertEnvKey appends key when missing from existing file"
- "#upsertEnvKey does not rewrite file when value is already current"
- "#upsertEnvKey replaces existing values, removes duplicates, and avoids quotes"
- "#upsertEnvKey preserves CRLF line endings"

## tests/lib/resolvers/keypair.test.js

Provider, noArmor and onStatus forwarding for the keypair resolver (Armor providers, spinner status). The `fk` forwarding test is ported in KeyPairTest.

- "keypair forwards envKeysFilepath to primitive keyring"
- "keypair passes no provider when noArmor is true"
- "keypair passes provider by default"
- "keypair forwards onStatus to providers"

## Whole upstream test files skipped as out of scope

Dotenvx Armor (cloud key storage), login/logout/doctor/curl/update, the TTY spinner, OS keychains and native key storage, 1Password/Bitwarden secret managers, key providers/decryptors, interactive prompts, pty and dynamic-extension execution. This port does not implement these features, so their tests have nothing to run against:

- tests/cli/actions/armor/down.test.js
- tests/cli/actions/armor/move.test.js
- tests/cli/actions/armor/pull.test.js
- tests/cli/actions/armor/push.test.js
- tests/cli/actions/armor/up.test.js
- tests/cli/actions/curl.test.js
- tests/cli/actions/doctor.test.js
- tests/cli/actions/login.test.js
- tests/cli/actions/logout.test.js
- tests/cli/actions/spinner-coverage.test.js
- tests/cli/actions/update.test.js
- tests/cli/commands/armor.test.js
- tests/db/session.test.js
- tests/lib/api/getAccount.test.js
- tests/lib/api/oauth.test.js
- tests/lib/api/postArmorDecrypt.test.js
- tests/lib/api/postArmorDown.test.js
- tests/lib/api/postArmorMove.test.js
- tests/lib/api/postArmorPull.test.js
- tests/lib/api/postArmorPush.test.js
- tests/lib/api/postArmorUp.test.js
- tests/lib/api/postLogout.test.js
- tests/lib/config-bitwarden.test.js
- tests/lib/config-onepassword.test.js
- tests/lib/decryptors/armor.test.js
- tests/lib/decryptors/index.test.js
- tests/lib/helpers/armoredKeyDisplay.test.js
- tests/lib/helpers/buildApiError.test.js
- tests/lib/helpers/createElapsedStatus.test.js
- tests/lib/helpers/createSpinner.test.js
- tests/lib/helpers/deviceValue.test.js
- tests/lib/helpers/executeDynamic.test.js
- tests/lib/helpers/executeExtension.test.js
- tests/lib/helpers/isNetworkError.test.js
- tests/lib/helpers/isTeamRequiredError.test.js
- tests/lib/helpers/keypairMetadata.test.js
- tests/lib/helpers/linuxSecretService.test.js
- tests/lib/helpers/login-helpers.test.js
- tests/lib/helpers/macosKeychain.test.js
- tests/lib/helpers/prompts.test.js
- tests/lib/helpers/ptyCommand.test.js
- tests/lib/helpers/removeDynamicHelpSection.test.js
- tests/lib/helpers/removeOptionsHelpParts.test.js
- tests/lib/helpers/resolveBitwardenPassword.test.js
- tests/lib/helpers/resolveOnePassword.test.js
- tests/lib/helpers/sanitizeCommandForMetadata.test.js
- tests/lib/helpers/teamChoicesFromMeta.test.js
- tests/lib/helpers/windowsCredentialManager.test.js
- tests/lib/providers/armor.test.js
- tests/lib/providers/native.test.js
- tests/lib/services/armorDown.test.js
- tests/lib/services/armorMove.test.js
- tests/lib/services/armorPull.test.js
- tests/lib/services/armorPush.test.js
- tests/lib/services/armorUp.test.js
- tests/lib/services/doctor.test.js
- tests/lib/services/login.test.js
- tests/lib/services/logout.test.js
