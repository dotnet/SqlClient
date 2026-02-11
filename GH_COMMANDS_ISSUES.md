# GitHub CLI Commands for Issue Milestone Updates
# Repository: dotnet/SqlClient
# Milestone: 7.0.0-preview4
# Issues to update: 3

## Overview

This document provides commands to add the 7.0.0-preview4 milestone to GitHub issues that were closed by PRs merged since the v7.0.0-preview3 tag.

## Analysis Summary

After analyzing all 25 PRs merged since v7.0.0-preview3:
- **Total GitHub issues found**: 3
- **Azure DevOps work items**: Many PRs reference internal ADO work items (AB#xxxxx) which cannot be updated via gh CLI

## Issues to Update

| Issue # | Title | Fixed By PR(s) |
|---------|-------|----------------|
| 3716 | NullReferenceException in SqlDataAdapter | #3749, #3857, #3854 |
| 3736 | ExecuteScalar doesn't propagate errors | #3912 |
| 3523 | Connection performance regression (SPN generation) | #3929 |

## Option 1: Individual Commands

Copy and paste these commands one by one, or all at once into your terminal:

```bash
gh issue edit 3716 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh issue edit 3736 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh issue edit 3523 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
```

## Option 2: One-liner with Loop

```bash
for issue in 3716 3736 3523; do gh issue edit $issue --repo dotnet/SqlClient --milestone "7.0.0-preview4"; done
```

## Option 3: One-liner with Progress

```bash
for issue in 3716 3736 3523; do echo "Updating issue #$issue..."; gh issue edit $issue --repo dotnet/SqlClient --milestone "7.0.0-preview4" && echo "✓ Success" || echo "✗ Failed"; done
```

## Option 4: Execute Simple Script

```bash
chmod +x simple-issues-milestone-update.sh
./simple-issues-milestone-update.sh
```

## Verification Commands

After running the updates, verify with:

```bash
# Check a single issue
gh issue view 3716 --repo dotnet/SqlClient --json milestone --jq '.milestone.title'

# List all issues in milestone
gh issue list --repo dotnet/SqlClient --search "milestone:7.0.0-preview4" --state closed --limit 50

# Check specific issues
for issue in 3716 3736 3523; do 
  echo -n "Issue #$issue: "
  gh issue view $issue --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "No milestone"'
done
```

## Prerequisites

- GitHub CLI installed: `brew install gh` (macOS) or `sudo apt install gh` (Linux)
- Authenticated: `gh auth login`
- Write access to dotnet/SqlClient repository

## Issue Details

### Issue #3716 - NullReferenceException in SqlDataAdapter
**Fixed by**: PRs #3749, #3857, #3854

Addresses NullReferenceException when systemParams is null in batch RPC scenarios.

### Issue #3736 - ExecuteScalar Error Propagation
**Fixed by**: PR #3912

Fixes ExecuteScalar() swallowing server errors that occur after the first result row is returned.

### Issue #3523 - SPN Generation Performance
**Fixed by**: PR #3929

Fixes connection performance regression where SPN generation was triggered for non-integrated authentication modes.

## Notes

- Many PRs reference Azure DevOps work items (format: AB#xxxxx) rather than GitHub issues
- Azure DevOps work items cannot be updated via GitHub CLI
- Only GitHub issues from the dotnet/SqlClient repository can be updated with this approach
- Some PRs (like infrastructure changes, documentation updates, or internal refactorings) may not reference any issues

## Related Files

- `gh-commands-issues-milestone-update.txt` - Plain text list of commands
- `simple-issues-milestone-update.sh` - Executable script
- `GH_COMMANDS.md` - Commands for updating PRs (separate file)
