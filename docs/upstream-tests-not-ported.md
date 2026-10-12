# Upstream tests not ported

Every upstream test under `dotenvx/tests` is either ported (cited by an exact `Refer to "<title>"` remark in `test/**`) or listed here with the reason. Generated from the submodule at the pinned tag; regenerate with the title-diff script described in CONTRIBUTING.md when the pin moves.

## tests/lib/helpers/prompts.test.js

Assertions on the argument object handed to the `enquirer` library (`type: 'select'`, normalized `choices`, `stdin`/`stdout`). This port has no enquirer: `Prompts.Select`/`Prompts.Password` read the terminal (or piped stdin) themselves. Their behavior is proven live against Node by LockParityTest, ArmorKeysParityTest and ArmorProviderParityTest (arrow keys, Enter, EOF, chunk draining).

- "select uses enquirer with normalized choices and IO context"
- "select does not require IO context"
