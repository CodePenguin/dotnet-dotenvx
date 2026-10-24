#!/usr/bin/env bash
# Runs what CI runs, the way CI runs it: from a fresh clone of the last commit (a warm local build skips projects that are
# already up to date, such as the fake programs in test/fakes, and so hides warnings) and with one processor (a small runner
# starves the thread pool, which exposes timing races a many-core machine never shows).
# Usage: scripts/prepush.sh        (commit first: the working tree is not part of the check)
set -euo pipefail

root=$(git rev-parse --show-toplevel)
cd "$root"

if [ -n "$(git status --porcelain)" ]; then
  echo "Uncommitted changes are not part of this check: it tests the last commit ($(git rev-parse --short HEAD))." >&2
fi

dir=$(mktemp -d)
trap 'rm -rf "$dir"' EXIT
git clone -q --recurse-submodules "$root" "$dir/repo"
cd "$dir/repo"

dotnet restore -v q
dotnet build --no-restore                       # warnings are errors (Directory.Build.props)
dotnet format --no-restore --verify-no-changes
DOTNET_PROCESSOR_COUNT=1 dotnet test --no-build --filter "FullyQualifiedName!~Dotenvx.ParityTest"

echo
echo "OK: fresh clone of $(git rev-parse --short HEAD) builds with no warnings and the unit tests pass on one processor."
