# GitHub CLI Commands for Issue Milestone Updates
# Repository: dotnet/SqlClient
# Milestone: 7.0.0-preview4
# Issues to update: 1 (updated 2026-02-11)

## Overview

This document provides commands to add the 7.0.0-preview4 milestone to GitHub issues that were closed by PRs merged since the v7.0.0-preview3 tag.

## Analysis Summary (UPDATED)

After analyzing all 44 PRs merged since v7.0.0-preview3 and checking current GitHub state:
- **Total GitHub issues found**: 4
- **Issues with milestone**: 3 ✅
- **Issues needing milestone**: 1 ⚠️
- **Azure DevOps work items**: Many PRs reference internal ADO work items (AB#xxxxx) which cannot be updated via gh CLI

## Current Status

### Issues Already With Milestone ✅ (3)

These issues already have the 7.0.0-preview4 milestone:

1. **#3716** - NullReferenceException in SqlDataAdapter ✅
   - Fixed by PRs: #3749, #3857, #3854
   
2. **#3736** - ExecuteScalar doesn't propagate errors ✅
   - Fixed by PR: #3912
   
3. **#3523** - Connection performance regression (SPN generation) ✅
   - Fixed by PR: #3929

### Issue Needing Milestone ⚠️ (1)

| Issue # | Title | Fixed By PR(s) |
|---------|-------|----------------|
| 3924 | Avoid running GitHub actions on forks | #3938 |

## Option 1: Individual Command (Only #3924 needs update)

Copy and paste this command:

```bash
gh issue edit 3924 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
```

## ~~Option 2: One-liner with Loop~~ (Not needed - only one issue)

For completeness, though only one issue needs updating:

```bash
gh issue edit 3924 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
```

## ~~Option 3: One-liner with Progress~~ (Not needed - only one issue)

```bash
echo "Updating issue #3924..."; gh issue edit 3924 --repo dotnet/SqlClient --milestone "7.0.0-preview4" && echo "✓ Success" || echo "✗ Failed"
```

## Option 4: Execute Simple Script

```bash
chmod +x simple-issues-milestone-update.sh
./simple-issues-milestone-update.sh
```

## Verification Commands

After running the update, verify with:

```bash
# Check issue #3924
gh issue view 3924 --repo dotnet/SqlClient --json milestone --jq '.milestone.title'

# Check all 4 issues
for issue in 3716 3736 3523 3924; do 
  echo -n "Issue #$issue: "
  gh issue view $issue --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "No milestone"'
done

# List all issues in milestone
gh issue list --repo dotnet/SqlClient --search "milestone:7.0.0-preview4" --state closed --limit 50
```

## Prerequisites

- GitHub CLI installed: `brew install gh` (macOS) or `sudo apt install gh` (Linux)
- Authenticated: `gh auth login`
- Write access to dotnet/SqlClient repository

## Issue Details

### Issue #3716 - NullReferenceException in SqlDataAdapter ✅
**Status**: Has milestone 7.0.0-preview4  
**Fixed by**: PRs #3749, #3857, #3854

Addresses NullReferenceException when systemParams is null in batch RPC scenarios.

### Issue #3736 - ExecuteScalar Error Propagation ✅
**Status**: Has milestone 7.0.0-preview4  
**Fixed by**: PR #3912

Fixes ExecuteScalar() swallowing server errors that occur after the first result row is returned.

### Issue #3523 - SPN Generation Performance ✅
**Status**: Has milestone 7.0.0-preview4  
**Fixed by**: PR #3929

Fixes connection performance regression where SPN generation was triggered for non-integrated authentication modes.

### Issue #3924 - Avoid running GitHub actions on forks ⚠️
**Status**: ⚠️ NEEDS milestone 7.0.0-preview4  
**Fixed by**: PR #3938  
**Type**: Task (engineering improvement)

Prevents GitHub actions from running in forks to save CPU cycles.

**Action Required**:
```bash
gh issue edit 3924 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
```

## Notes

- Many PRs reference Azure DevOps work items (format: AB#xxxxx) rather than GitHub issues
- Azure DevOps work items cannot be updated via GitHub CLI
- Only GitHub issues from the dotnet/SqlClient repository can be updated with this approach
- Some PRs (like infrastructure changes, documentation updates, or internal refactorings) may not reference any issues

## Related Files

- `gh-commands-issues-milestone-update.txt` - Plain text list of commands
- `simple-issues-milestone-update.sh` - Executable script
- `GH_COMMANDS.md` - Commands for updating PRs (separate file)
