# GitHub CLI Commands for Milestone Updates
# Repository: dotnet/SqlClient
# Milestone: 7.0.0-preview4
# PRs to update: 25

## Option 1: Individual Commands
Copy and paste these commands one by one, or all at once into your terminal:

```bash
gh pr edit 3749 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3797 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3811 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3829 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3841 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3853 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3854 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3856 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3859 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3864 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3865 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3869 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3879 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3893 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3895 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3897 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3900 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3905 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3906 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3911 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3919 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3925 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3932 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3933 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh pr edit 3938 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
```

## Option 2: One-liner with Loop

```bash
for pr in 3749 3797 3811 3829 3841 3853 3854 3856 3859 3864 3865 3869 3879 3893 3895 3897 3900 3905 3906 3911 3919 3925 3932 3933 3938; do gh pr edit $pr --repo dotnet/SqlClient --milestone "7.0.0-preview4"; done
```

## Option 3: One-liner with Error Handling

```bash
for pr in 3749 3797 3811 3829 3841 3853 3854 3856 3859 3864 3865 3869 3879 3893 3895 3897 3900 3905 3906 3911 3919 3925 3932 3933 3938; do echo "Updating PR #$pr..."; gh pr edit $pr --repo dotnet/SqlClient --milestone "7.0.0-preview4" && echo "✓ Success" || echo "✗ Failed"; done
```

## Option 4: Execute Simple Script

```bash
chmod +x simple-milestone-update.sh
./simple-milestone-update.sh
```

## Option 5: Use Advanced Script (Recommended)

The advanced script includes dry-run mode, error handling, and progress tracking:

```bash
chmod +x update-milestone-7.0.0-preview4.sh
./update-milestone-7.0.0-preview4.sh --dry-run  # Test first
./update-milestone-7.0.0-preview4.sh            # Execute
```

## Verification Commands

After running the updates, verify with:

```bash
# Check a single PR
gh pr view 3749 --repo dotnet/SqlClient --json milestone --jq '.milestone.title'

# Check all PRs in milestone
gh pr list --repo dotnet/SqlClient --search "milestone:7.0.0-preview4" --state merged --limit 50

# Or use the verification script
./verify-milestone-prs.sh
```

## Prerequisites

- GitHub CLI installed: `brew install gh` (macOS) or `sudo apt install gh` (Linux)
- Authenticated: `gh auth login`
- Write access to dotnet/SqlClient repository

## PR Details

| PR # | Title |
|------|-------|
| 3749 | Fixing NullReferenceException issue with SqlDataAdapter |
| 3797 | Use global.json to restrict .NET SDK use |
| 3811 | Add ADO pipeline dashboard summary tables |
| 3829 | Add 7.0.0-preview3 release notes and release note generation prompt |
| 3841 | Introduce app context switch for setting MSF=true by default |
| 3853 | Fix LocalAppContextSwitches race conditions in tests |
| 3854 | Revert "Fixing NullReferenceException issue with SqlDataAdapter (#3749)" |
| 3856 | Test \| Add flaky test quarantine zone |
| 3859 | Minor improvements to Managed SNI tracing |
| 3864 | Add Release compile step to PR pipelines |
| 3865 | Stress test pipeline: Add placeholder |
| 3869 | Tests \| SqlError, SqlErrorCollection |
| 3879 | Release Notes for 5.1.9 |
| 3893 | Fix CodeCov upload issues |
| 3895 | Add release notes for 6.1.4 |
| 3897 | Add release notes for 6.0.5 |
| 3900 | Cleanup, Merge \| Revert public visibility of internal interop enums |
| 3905 | Reduce default test job timeout to 60 minutes |
| 3906 | Fail tests that run for more than 10 minutes |
| 3911 | Retired 5.1 pipelines, added some missing SNI pipelines |
| 3919 | Updated 1ES inventory config to the latest schema |
| 3925 | Create stub pipeline files for Abstractions and Azure packages |
| 3932 | Common MDS \| Cleanup Manual Tests |
| 3933 | Fix MDS Official Pipeline |
| 3938 | Prevent actions from running in forks |
