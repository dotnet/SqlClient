# Milestone Verification Summary: 7.0.0-preview4

**Generated**: 2026-02-11  
**Tag**: v7.0.0-preview3 (2025-12-08, commit: 5e14b56572f7c1700ee8bf8eb492cec1de9a79be)  
**Milestone**: 7.0.0-preview4  
**Repository**: dotnet/SqlClient

## Executive Summary

This document confirms the status of all PRs merged to `main` since the v7.0.0-preview3 tag and the issues they closed, verifying their assignment to the 7.0.0-preview4 milestone.

### Quick Stats

- **Total PRs merged since v7.0.0-preview3**: 44
- **Total GitHub Issues closed by those PRs**: 3
- **Total items requiring milestone**: 47

## PRs Merged Since v7.0.0-preview3 (44 Total)

### PRs Already With Milestone 7.0.0-preview4 (19)

These PRs already have the correct milestone assigned:

1. #3772 - Perf | Use source generation to serialize user agent JSON
2. #3773 - Flatten | SqlInternalConnectionTds and SqlInternalConnection
3. #3791 - Perf | Reuse XmlWriterSettings, eliminate MemoryCacheEntryOptions allocations
4. #3794 - Tests | Widen SqlVector test criteria, roundtrip additional values in tests
5. #3818 - Flatten | DbMetaDataFactory -> SqlMetaDataFactory
6. #3826 - Update UserAgent to pipe-delimited format
7. #3837 - Merge Project | Build the Common Project
8. #3842 - Tests | Msc Test Improvements/Cleanup
9. #3857 - Fixing NullReferenceException issue with SqlDataAdapter
10. #3870 - Common Project | Unit Tests
11. #3872 - Ensure that 0 length reads return an empty array not null
12. #3890 - Common Project | Functional Tests
13. #3902 - Azure Split - Step 1 - Prep Work
14. #3904 - Azure Split - Step 2 - New Files
15. #3908 - Azure Split - Step 3 - Tie Everything Together
16. #3909 - DRI | Fix AKV Official Build; Sign Less DLLs
17. #3912 - Fix #3736 | Propagate Errors from ExecuteScalar
18. #3928 - Stabilize macOS agent setup
19. #3929 - Avoid unintended SPN generation for non-integrated authentication on native SNI path

### PRs Needing Milestone 7.0.0-preview4 (25)

These PRs require the milestone to be assigned:

1. #3749 - Fixing NullReferenceException issue with SqlDataAdapter
2. #3797 - Use global.json to restrict .NET SDK use
3. #3811 - Add ADO pipeline dashboard summary tables
4. #3829 - Add 7.0.0-preview3 release notes and release note generation prompt
5. #3841 - Introduce app context switch for setting MSF=true by default
6. #3853 - Fix LocalAppContextSwitches race conditions in tests
7. #3854 - Revert "Fixing NullReferenceException issue with SqlDataAdapter (#3749)"
8. #3856 - Test | Add flaky test quarantine zone
9. #3859 - Minor improvements to Managed SNI tracing
10. #3864 - Add Release compile step to PR pipelines
11. #3865 - Stress test pipeline: Add placeholder
12. #3869 - Tests | SqlError, SqlErrorCollection
13. #3879 - Release Notes for 5.1.9
14. #3893 - Fix CodeCov upload issues
15. #3895 - Add release notes for 6.1.4
16. #3897 - Add release notes for 6.0.5
17. #3900 - Cleanup, Merge | Revert public visibility of internal interop enums
18. #3905 - Reduce default test job timeout to 60 minutes
19. #3906 - Fail tests that run for more than 10 minutes
20. #3911 - Retired 5.1 pipelines, added some missing SNI pipelines
21. #3919 - Updated 1ES inventory config to the latest schema
22. #3925 - Create stub pipeline files for Abstractions and Azure packages
23. #3932 - Common MDS | Cleanup Manual Tests
24. #3933 - Fix MDS Official Pipeline
25. #3938 - Prevent actions from running in forks

## Issues Closed by Those PRs (3 Total)

### Issue #3716 - NullReferenceException in SqlDataAdapter
- **Status**: Requires milestone assignment
- **Fixed by**: PRs #3749, #3857, #3854
- **Description**: Addresses NullReferenceException when systemParams is null in batch RPC scenarios

### Issue #3736 - ExecuteScalar doesn't propagate errors
- **Status**: Requires milestone assignment
- **Fixed by**: PR #3912
- **Description**: Fixes ExecuteScalar() swallowing server errors that occur after the first result row is returned

### Issue #3523 - Connection performance regression (SPN generation)
- **Status**: Requires milestone assignment
- **Fixed by**: PR #3929
- **Description**: Fixes 5-second connection delay with SQL authentication by avoiding unnecessary SPN generation

## Verification Commands

### Bash One-Liner to Check All PRs

```bash
for pr in 3749 3772 3773 3791 3794 3797 3811 3818 3826 3829 3837 3841 3842 3853 3854 3856 3857 3859 3864 3865 3869 3870 3872 3879 3890 3893 3895 3897 3900 3902 3904 3905 3906 3908 3909 3911 3912 3919 3925 3928 3929 3932 3933 3938; do echo -n "PR #$pr: "; gh pr view $pr --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "NO MILESTONE"'; done
```

### Bash One-Liner to Check All Issues

```bash
for issue in 3716 3736 3523; do echo -n "Issue #$issue: "; gh issue view $issue --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "NO MILESTONE"'; done
```

### Combined One-Liner (Check Everything)

```bash
echo "=== PRs ===" && for pr in 3749 3772 3773 3791 3794 3797 3811 3818 3826 3829 3837 3841 3842 3853 3854 3856 3857 3859 3864 3865 3869 3870 3872 3879 3890 3893 3895 3897 3900 3902 3904 3905 3906 3908 3909 3911 3912 3919 3925 3928 3929 3932 3933 3938; do m=$(gh pr view $pr --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "NONE"' 2>/dev/null); [[ "$m" != "7.0.0-preview4" ]] && echo "  PR #$pr: $m"; done && echo "=== Issues ===" && for issue in 3716 3736 3523; do m=$(gh issue view $issue --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "NONE"' 2>/dev/null); [[ "$m" != "7.0.0-preview4" ]] && echo "  Issue #$issue: $m"; done && echo "Done"
```

### Use Verification Script

```bash
./verify-all-milestones.sh
```

## Update Commands

To assign the milestone to all PRs and issues that need it:

### Update All PRs (One-Liner)

```bash
for pr in 3749 3797 3811 3829 3841 3853 3854 3856 3859 3864 3865 3869 3879 3893 3895 3897 3900 3905 3906 3911 3919 3925 3932 3933 3938; do gh pr edit $pr --repo dotnet/SqlClient --milestone "7.0.0-preview4"; done
```

### Update All Issues (One-Liner)

```bash
for issue in 3716 3736 3523; do gh issue edit $issue --repo dotnet/SqlClient --milestone "7.0.0-preview4"; done
```

### Combined Update (Everything at Once)

```bash
for pr in 3749 3797 3811 3829 3841 3853 3854 3856 3859 3864 3865 3869 3879 3893 3895 3897 3900 3905 3906 3911 3919 3925 3932 3933 3938; do gh pr edit $pr --repo dotnet/SqlClient --milestone "7.0.0-preview4"; done && for issue in 3716 3736 3523; do gh issue edit $issue --repo dotnet/SqlClient --milestone "7.0.0-preview4"; done
```

### Use Update Scripts

```bash
# Update PRs
./update-milestone-7.0.0-preview4.sh

# Update Issues
./simple-issues-milestone-update.sh
```

## Important Notes

### Azure DevOps Work Items

Many PRs in this release reference Azure DevOps work items (format: `AB#xxxxx`) rather than GitHub issues. These include:

- Infrastructure changes
- Internal process improvements
- Pipeline updates
- Documentation changes

**ADO work items cannot be updated via GitHub CLI** and are not included in this verification.

### PR and Issue Traceability

| Issue # | Description | Fixed By PRs |
|---------|-------------|--------------|
| #3716 | NullReferenceException in SqlDataAdapter | #3749, #3857, #3854 |
| #3736 | ExecuteScalar error propagation | #3912 |
| #3523 | SPN generation performance | #3929 |

## Recommended Actions

1. **Verify current state**:
   ```bash
   ./verify-all-milestones.sh
   ```

2. **If items are missing milestone, update them**:
   ```bash
   ./update-milestone-7.0.0-preview4.sh      # For PRs
   ./simple-issues-milestone-update.sh       # For issues
   ```

3. **Verify again**:
   ```bash
   ./verify-all-milestones.sh
   ```

4. **Confirm in GitHub UI**:
   - View milestone: https://github.com/dotnet/SqlClient/milestone/82
   - Should show all 44 PRs and 3 issues

## Files in This Repository

- `verify-all-milestones.sh` - Comprehensive verification script
- `update-milestone-7.0.0-preview4.sh` - Advanced PR update script (with dry-run)
- `verify-milestone-prs.sh` - PR-specific verification
- `simple-milestone-update.sh` - Simple PR update script
- `simple-issues-milestone-update.sh` - Simple issue update script
- `GH_COMMANDS.md` - PR commands documentation
- `GH_COMMANDS_ISSUES.md` - Issue commands documentation
- `MILESTONE_UPDATE_README.md` - Full documentation
- `QUICK_REFERENCE.md` - Quick command reference
- `MILESTONE_VERIFICATION_SUMMARY.md` - This file

## Conclusion

**Current Status**: 
- 19/44 PRs have the milestone ✓
- 25/44 PRs need the milestone ⚠
- 0/3 Issues have the milestone ⚠
- **Total items needing assignment: 28**

**Completion**: After running the update scripts, all 44 PRs and 3 issues should have the 7.0.0-preview4 milestone assigned.
