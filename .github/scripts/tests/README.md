# Cherry-Pick Workflow Tests

This directory contains automated tests for the shell scripts used by the
[cherry-pick-hotfix](./../../../.github/workflows/cherry-pick-hotfix.yml)
GitHub Actions workflow.

## What is Bats?

**[Bats](https://github.com/bats-core/bats-core)** (Bash Automated Testing
System) is a TAP-compliant testing framework for Bash scripts. Each `.bats`
file contains one or more `@test` blocks that run shell commands and assert
outcomes using the built-in `run` helper. A test passes when every command
exits with code 0; it fails on the first non-zero exit.

Key concepts:

| Concept | Description |
|---------|-------------|
| `@test "name" { ... }` | Defines a single test case |
| `run <command>` | Captures stdout, stderr, and exit code into `$output` and `$status` |
| `setup()` | Runs before every `@test` — used to create temp files and set env vars |
| `teardown()` | Runs after every `@test` — used to clean up temp files |
| `[[ "$status" -eq 0 ]]` | Assert exit code |
| `[[ "$output" == *"text"* ]]` | Assert output contains a string |

## Installing Bats

### Linux (apt)

```bash
sudo apt-get update && sudo apt-get install -y bats
```

### macOS (Homebrew)

```bash
brew install bats-core
```

### From source (any platform)

```bash
git clone https://github.com/bats-core/bats-core.git
cd bats-core
sudo ./install.sh /usr/local
```

### Verify installation

```bash
bats --version
# Expected output: Bats 1.x.x
```

## Running the Tests

All commands assume you are at the **repository root**.

### Run all tests

```bash
bats .github/scripts/tests/
```

### Run a single test file

```bash
bats .github/scripts/tests/extract-hotfix-versions.bats
bats .github/scripts/tests/cherry-pick-to-release.bats
```

### Run a specific test by name

```bash
bats .github/scripts/tests/extract-hotfix-versions.bats \
  --filter "single Hotfix label"
```

### Verbose output (show each test name)

```bash
bats --tap .github/scripts/tests/
```

### Pretty output (requires bats-core 1.5+)

```bash
bats --formatter pretty .github/scripts/tests/
```

## Test Files

| File | Tests | Covers |
|------|-------|--------|
| `extract-hotfix-versions.bats` | 18 | Label parsing, version extraction, matrix JSON output, edge cases (malformed labels, duplicates, `labeled` vs `closed` events) |
| `cherry-pick-to-release.bats` | 15 | Branch derivation, already-applied detection, clean cherry-pick, conflict handling, milestone lookup, PR creation, duplicate skip logic |

## How the Tests Work

Both test files use the same general approach:

1. **`setup()`** creates a temporary directory and populates it with mock
   `git` and `gh` executables — simple shell scripts that echo predetermined
   responses. Environment variables (`VERSION`, `MERGE_COMMIT_SHA`, etc.) are
   set to known values.

2. **`@test` blocks** call `run bash "$SCRIPT"` to execute the script under
   test in a subshell. The mocks intercept all `git` and `gh` invocations, so
   no real repository or GitHub API access is needed.

3. **Assertions** check `$status` (exit code) and `$output`
   (combined stdout/stderr) for expected values, error messages, or
   GitHub Actions workflow commands (`::set-output::`, `::error::`,
   `::notice::`).

4. **`teardown()`** removes the temporary directory and mock binaries.

### Example mock

```bash
# Mock git that reports 2 parents (a merge commit)
cat > "${STUB_DIR}/git" <<'MOCK'
#!/usr/bin/env bash
case "$*" in
  "rev-list --parents -n1 "*) echo "abc123 parent1 parent2" ;;
  "cherry "*)                 echo "+ abc123" ;;
  *)                          echo "git mock: $*" ;;
esac
MOCK
chmod +x "${STUB_DIR}/git"
```

The mock sits earlier on `$PATH` than the real `git`, so the script under test
calls the mock transparently.

## Troubleshooting

### `bats: command not found`

Bats is not installed. See [Installing Bats](#installing-bats) above.

### Tests fail with `permission denied`

The scripts under `.github/scripts/` must be executable:

```bash
chmod +x .github/scripts/*.sh
```

### Tests pass locally but fail in CI

Ensure the CI workflow installs bats before running tests. For GitHub Actions:

```yaml
- name: Install bats
  run: sudo apt-get update && sudo apt-get install -y bats

- name: Run tests
  run: bats .github/scripts/tests/
```

### A test fails unexpectedly

Run with `set -x` tracing to see each command:

```bash
bats --tap .github/scripts/tests/cherry-pick-to-release.bats \
  --filter "name of failing test" 2>&1
```

Or add `echo "DEBUG: $variable" >&3` inside a test to print to the terminal
(file descriptor 3 is bats's "direct to terminal" channel).
