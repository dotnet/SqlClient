# Quick Reference: Milestone Update Scripts

## Quick Start

```bash
# 1. First, do a dry run to see what would change
./update-milestone-7.0.0-preview4.sh --dry-run

# 2. If everything looks good, run the actual update
./update-milestone-7.0.0-preview4.sh

# 3. Verify the results
./verify-milestone-prs.sh
```

## Files Created

- **`update-milestone-7.0.0-preview4.sh`** - Main script to update PRs with milestone
- **`verify-milestone-prs.sh`** - Verification script to check milestone assignments
- **`MILESTONE_UPDATE_README.md`** - Comprehensive documentation

## One-Line Commands

### Check if gh CLI is installed
```bash
gh --version
```

### Authenticate with GitHub
```bash
gh auth login
```

### Manual PR milestone update (single PR)
```bash
gh pr edit 3749 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
```

### Check milestone for a single PR
```bash
gh pr view 3749 --repo dotnet/SqlClient --json milestone --jq '.milestone.title'
```

### List all PRs in milestone
```bash
gh pr list --repo dotnet/SqlClient --search "milestone:7.0.0-preview4" --state merged --limit 100
```

## PR Numbers Needing Update

```
3749, 3797, 3811, 3829, 3841, 3853, 3854, 3856, 3859, 3864,
3865, 3869, 3879, 3893, 3895, 3897, 3900, 3905, 3906, 3911,
3919, 3925, 3932, 3933, 3938
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Command not found: gh | Install from https://cli.github.com/ |
| Not authenticated | Run `gh auth login` |
| Permission denied (script) | Run `chmod +x *.sh` |
| Permission denied (GitHub) | Contact repo maintainers for write access |
| Rate limit | Wait a few minutes and try again |

## Expected Output (Success)

```
========================================
Milestone Update Script
========================================
Repository: dotnet/SqlClient
Milestone: 7.0.0-preview4
Total PRs to update: 25
========================================

✓ GitHub CLI is installed and authenticated

Processing PR #3749... 
  Title: Fixing NullReferenceException issue with SqlDataAdapter
  ✓ Successfully updated milestone

...

========================================
Summary
========================================
Total PRs: 25
Successfully updated: 25
========================================
```

## Resources

- [Full Documentation](./MILESTONE_UPDATE_README.md)
- [GitHub CLI Manual](https://cli.github.com/manual/)
- [SqlClient Repository](https://github.com/dotnet/SqlClient)
