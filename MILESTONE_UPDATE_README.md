# Milestone Update Script - 7.0.0-preview4

## Overview

This directory contains a script to update PRs merged to the `main` branch since the **v7.0.0-preview3** tag (December 8, 2025) with the **7.0.0-preview4** milestone.

## Analysis Summary

- **Tag analyzed**: v7.0.0-preview3
- **Tag date**: December 8, 2025
- **Tag commit**: 5e14b56572f7c1700ee8bf8eb492cec1de9a79be
- **Total PRs merged since tag**: 44
- **PRs already with milestone**: 19
- **PRs needing milestone**: 25

## PRs to Update

The following 25 PRs will be updated with the 7.0.0-preview4 milestone:

| PR # | Title |
|------|-------|
| #3749 | Fixing NullReferenceException issue with SqlDataAdapter |
| #3797 | Use global.json to restrict .NET SDK use |
| #3811 | Add ADO pipeline dashboard summary tables |
| #3829 | Add 7.0.0-preview3 release notes and release note generation prompt. |
| #3841 | Introduce app context switch for setting MSF=true by default |
| #3853 | Fix LocalAppContextSwitches race conditions in tests |
| #3854 | Revert "Fixing NullReferenceException issue with SqlDataAdapter (#3749)" |
| #3856 | Test \| Add flaky test quarantine zone |
| #3859 | Minor improvements to Managed SNI tracing |
| #3864 | Add Release compile step to PR pipelines |
| #3865 | Stress test pipeline: Add placeholder |
| #3869 | Tests \| SqlError, SqlErrorCollection |
| #3879 | Release Notes for 5.1.9 |
| #3893 | Fix CodeCov upload issues |
| #3895 | Add release notes for 6.1.4 |
| #3897 | Add release notes for 6.0.5 |
| #3900 | Cleanup, Merge \| Revert public visibility of internal interop enums |
| #3905 | Reduce default test job timeout to 60 minutes |
| #3906 | Fail tests that run for more than 10 minutes |
| #3911 | Retired 5.1 pipelines, added some missing SNI pipelines. |
| #3919 | Updated 1ES inventory config to the latest schema. |
| #3925 | Create stub pipeline files for Abstractions and Azure packages |
| #3932 | Common MDS \| Cleanup Manual Tests |
| #3933 | Fix MDS Official Pipeline |
| #3938 | Prevent actions from running in forks |

## Prerequisites

Before running the script, ensure you have:

1. **GitHub CLI (gh) installed**
   ```bash
   # On macOS
   brew install gh
   
   # On Ubuntu/Debian
   sudo apt install gh
   
   # On Windows
   winget install --id GitHub.cli
   ```
   
   Or visit: https://cli.github.com/

2. **Authenticated with GitHub**
   ```bash
   gh auth login
   ```
   
   Follow the prompts to authenticate with your GitHub account.

3. **Write access to dotnet/SqlClient repository**
   - You must be a maintainer or have appropriate permissions

## Usage

### Dry Run (Recommended First)

Before making any changes, perform a dry run to see what would be updated:

```bash
./update-milestone-7.0.0-preview4.sh --dry-run
```

This will:
- ✅ Check all prerequisites
- ✅ Fetch PR details
- ✅ Show what would be updated
- ❌ NOT make any actual changes

### Execute Updates

Once you've verified the dry run output, execute the updates:

```bash
./update-milestone-7.0.0-preview4.sh
```

This will:
- Update each of the 25 PRs with the 7.0.0-preview4 milestone
- Display progress for each PR
- Provide a summary at the end

## Script Features

- ✅ **Colored output** - Easy-to-read progress indicators
- ✅ **Error handling** - Gracefully handles failures
- ✅ **Rate limiting protection** - Small delays between API calls
- ✅ **Dry run mode** - Test before executing
- ✅ **Detailed logging** - Shows PR titles and status
- ✅ **Summary report** - Success/failure counts

## Example Output

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

Processing PR #3797... 
  Title: Use global.json to restrict .NET SDK use
  ✓ Successfully updated milestone

...

========================================
Summary
========================================
Total PRs: 25
Successfully updated: 25
========================================
```

## Troubleshooting

### "gh: command not found"

Install the GitHub CLI from https://cli.github.com/

### "Not authenticated with GitHub CLI"

Run `gh auth login` and follow the authentication steps.

### "Failed to update milestone"

Possible causes:
- Insufficient permissions (need write access to repository)
- Rate limiting (script includes delays, but wait a few minutes if this occurs)
- Network issues
- Milestone doesn't exist (verify "7.0.0-preview4" milestone exists in the repository)

### Permission Denied

If you get a permission error when running the script:
```bash
chmod +x update-milestone-7.0.0-preview4.sh
```

## Verification

After running the script, you can verify the updates by:

1. **Using GitHub UI**: Visit https://github.com/dotnet/SqlClient/milestone/XX and check the PRs

2. **Using gh CLI**:
   ```bash
   gh pr view 3749 --repo dotnet/SqlClient --json milestone
   ```

3. **Query all PRs**:
   ```bash
   gh pr list --repo dotnet/SqlClient --search "milestone:7.0.0-preview4" --state merged --limit 100
   ```

## Notes

- The script includes a 0.5 second delay between API calls to avoid rate limiting
- All PR numbers and titles are documented in the script comments
- The script will exit with code 1 if any updates fail (unless in dry-run mode)
- This script is idempotent - running it multiple times won't cause issues

## Support

For issues with:
- **This script**: Contact the repository maintainers
- **GitHub CLI**: Visit https://github.com/cli/cli/issues
- **Repository access**: Contact dotnet/SqlClient administrators

## Related Resources

- [GitHub CLI Documentation](https://cli.github.com/manual/)
- [GitHub API Rate Limiting](https://docs.github.com/en/rest/overview/resources-in-the-rest-api#rate-limiting)
- [SqlClient Release Process](../CONTRIBUTING.md)
