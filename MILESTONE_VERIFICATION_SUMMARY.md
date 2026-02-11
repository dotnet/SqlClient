# Milestone Verification Summary: 7.0.0-preview4

**Updated**: 2026-02-11 17:29 UTC  
**Tag**: v7.0.0-preview3 (2025-12-08, commit: 5e14b56572f7c1700ee8bf8eb492cec1de9a79be)  
**Milestone**: 7.0.0-preview4  
**Repository**: dotnet/SqlClient

## Executive Summary - CURRENT STATE ✅

🎉 **ALL 44 PRs NOW HAVE THE MILESTONE ASSIGNED!**  
✅ **ALL 3 ORIGINALLY IDENTIFIED ISSUES NOW HAVE THE MILESTONE!**  
⚠️ **One additional issue (#3924) discovered - needs milestone assignment**

### Quick Stats

- **Total PRs merged since v7.0.0-preview3**: 44
- **PRs with milestone 7.0.0-preview4**: **44/44 (100%)** ✅
- **Originally identified issues with milestone**: **3/3 (100%)** ✅
- **Newly discovered issue needing milestone**: 1 ⚠️
- **Overall completion**: 47/48 items (97.9%)

## PRs Merged Since v7.0.0-preview3 (44 Total)

### ✅ ALL 44 PRs Now Have Milestone 7.0.0-preview4 (100% Complete)

All PRs merged since v7.0.0-preview3 now have the correct milestone assigned:

1. #3749 - Fixing NullReferenceException issue with SqlDataAdapter ✅
2. #3772 - Perf | Use source generation to serialize user agent JSON ✅
3. #3773 - Flatten | SqlInternalConnectionTds and SqlInternalConnection ✅
4. #3791 - Perf | Reuse XmlWriterSettings, eliminate MemoryCacheEntryOptions allocations ✅
5. #3794 - Tests | Widen SqlVector test criteria, roundtrip additional values in tests ✅
6. #3797 - Use global.json to restrict .NET SDK use ✅
7. #3811 - Add ADO pipeline dashboard summary tables ✅
8. #3818 - Flatten | DbMetaDataFactory -> SqlMetaDataFactory ✅
9. #3826 - Update UserAgent to pipe-delimited format ✅
10. #3829 - Add 7.0.0-preview3 release notes and release note generation prompt ✅
11. #3837 - Merge Project | Build the Common Project ✅
12. #3841 - Introduce app context switch for setting MSF=true by default ✅
13. #3842 - Tests | Msc Test Improvements/Cleanup ✅
14. #3853 - Fix LocalAppContextSwitches race conditions in tests ✅
15. #3854 - Revert "Fixing NullReferenceException issue with SqlDataAdapter (#3749)" ✅
16. #3856 - Test | Add flaky test quarantine zone ✅
17. #3857 - Fixing NullReferenceException issue with SqlDataAdapter ✅
18. #3859 - Minor improvements to Managed SNI tracing ✅
19. #3864 - Add Release compile step to PR pipelines ✅
20. #3865 - Stress test pipeline: Add placeholder ✅
21. #3869 - Tests | SqlError, SqlErrorCollection ✅
22. #3870 - Common Project | Unit Tests ✅
23. #3872 - Ensure that 0 length reads return an empty array not null ✅
24. #3879 - Release Notes for 5.1.9 ✅
25. #3890 - Common Project | Functional Tests ✅
26. #3893 - Fix CodeCov upload issues ✅
27. #3895 - Add release notes for 6.1.4 ✅
28. #3897 - Add release notes for 6.0.5 ✅
29. #3900 - Cleanup, Merge | Revert public visibility of internal interop enums ✅
30. #3902 - Azure Split - Step 1 - Prep Work ✅
31. #3904 - Azure Split - Step 2 - New Files ✅
32. #3905 - Reduce default test job timeout to 60 minutes ✅
33. #3906 - Fail tests that run for more than 10 minutes ✅
34. #3908 - Azure Split - Step 3 - Tie Everything Together ✅
35. #3909 - DRI | Fix AKV Official Build; Sign Less DLLs ✅
36. #3911 - Retired 5.1 pipelines, added some missing SNI pipelines ✅
37. #3912 - Fix #3736 | Propagate Errors from ExecuteScalar ✅
38. #3919 - Updated 1ES inventory config to the latest schema ✅
39. #3925 - Create stub pipeline files for Abstractions and Azure packages ✅
40. #3928 - Stabilize macOS agent setup ✅
41. #3929 - Avoid unintended SPN generation for non-integrated authentication on native SNI path ✅
42. #3932 - Common MDS | Cleanup Manual Tests ✅
43. #3933 - Fix MDS Official Pipeline ✅
44. #3938 - Prevent actions from running in forks ✅

## Issues Closed by Those PRs

### Originally Identified Issues ✅ (All 3 have milestone)

All originally identified issues now have the 7.0.0-preview4 milestone assigned:

### Issue #3716 - NullReferenceException in SqlDataAdapter ✅
- **Status**: ✅ Has milestone 7.0.0-preview4
- **Fixed by**: PRs #3749, #3857, #3854
- **Description**: Addresses NullReferenceException when systemParams is null in batch RPC scenarios with Always Encrypted

### Issue #3736 - ExecuteScalar doesn't propagate errors ✅
- **Status**: ✅ Has milestone 7.0.0-preview4
- **Fixed by**: PR #3912
- **Description**: Fixes ExecuteScalar() swallowing server errors that occur after the first result row is returned

### Issue #3523 - Connection performance regression (SPN generation) ✅
- **Status**: ✅ Has milestone 7.0.0-preview4
- **Fixed by**: PR #3929
- **Description**: Fixes 5-second connection delay with SQL authentication by avoiding unnecessary SPN generation

### Newly Discovered Issue ⚠️ (1 needs milestone)

### Issue #3924 - Avoid running GitHub actions on forks ⚠️
- **Status**: ⚠️ NO MILESTONE (needs assignment)
- **Fixed by**: PR #3938
- **Type**: Task (engineering improvement)
- **Description**: Prevents GitHub actions from running in forks to save CPU cycles
- **Closed**: 2026-02-06

**Action Required**: Add milestone to issue #3924:
```bash
gh issue edit 3924 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
```

## Verification Commands

### Bash One-Liner to Verify All PRs (Should show nothing if all have milestone)

```bash
for pr in 3749 3772 3773 3791 3794 3797 3811 3818 3826 3829 3837 3841 3842 3853 3854 3856 3857 3859 3864 3865 3869 3870 3872 3879 3890 3893 3895 3897 3900 3902 3904 3905 3906 3908 3909 3911 3912 3919 3925 3928 3929 3932 3933 3938; do m=$(gh pr view $pr --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "NONE"' 2>/dev/null); [[ "$m" != "7.0.0-preview4" ]] && echo "PR #$pr: $m"; done
```

### Bash One-Liner to Verify All Issues

```bash
for issue in 3716 3736 3523 3924; do echo -n "Issue #$issue: "; gh issue view $issue --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "NO MILESTONE"'; done
```

### Combined One-Liner (Check Everything)

```bash
echo "=== PRs ===" && for pr in 3749 3772 3773 3791 3794 3797 3811 3818 3826 3829 3837 3841 3842 3853 3854 3856 3857 3859 3864 3865 3869 3870 3872 3879 3890 3893 3895 3897 3900 3902 3904 3905 3906 3908 3909 3911 3912 3919 3925 3928 3929 3932 3933 3938; do m=$(gh pr view $pr --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "NONE"' 2>/dev/null); [[ "$m" != "7.0.0-preview4" ]] && echo "  PR #$pr: $m"; done && echo "=== Issues ===" && for issue in 3716 3736 3523 3924; do m=$(gh issue view $issue --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "NONE"' 2>/dev/null); [[ "$m" != "7.0.0-preview4" ]] && echo "  Issue #$issue: $m"; done && echo "Done"
```

### Use Verification Script

```bash
./verify-all-milestones.sh
```

## Update Commands

### ⚠️ Only One Issue Needs Milestone (Issue #3924)

```bash
gh issue edit 3924 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
```

### ~~Update All PRs~~ (✅ COMPLETE - All 44 PRs have milestone)

All PRs already have the milestone assigned. No action needed.

### ~~Update All Issues~~ (⚠️ Only #3924 needs update)

The 3 originally identified issues (#3716, #3736, #3523) already have the milestone.
Only issue #3924 needs the milestone:

```bash
gh issue edit 3924 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
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

| Issue # | Description | Fixed By PRs | Milestone Status |
|---------|-------------|--------------|------------------|
| #3716 | NullReferenceException in SqlDataAdapter | #3749, #3857, #3854 | ✅ Has milestone |
| #3736 | ExecuteScalar error propagation | #3912 | ✅ Has milestone |
| #3523 | SPN generation performance | #3929 | ✅ Has milestone |
| #3924 | Avoid running GitHub actions on forks | #3938 | ⚠️ Needs milestone |

## Recommended Actions

1. **Verify current state** (all PRs should pass, only issue #3924 should show):
   ```bash
   ./verify-all-milestones.sh
   ```

2. **Add milestone to issue #3924**:
   ```bash
   gh issue edit 3924 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
   ```

3. **Verify again**:
   ```bash
   ./verify-all-milestones.sh
   ```

4. **Confirm in GitHub UI**:
   - View milestone: https://github.com/dotnet/SqlClient/milestone/82
   - Should show all 44 PRs and 4 issues

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

**Current Status (Updated 2026-02-11 17:29 UTC)**: 
- ✅ 44/44 PRs have the milestone (100% complete)
- ✅ 3/4 Issues have the milestone (75% complete)
- ⚠️ **1 issue needs milestone: #3924**

**Success Rate**: 47/48 items (97.9%)

**Next Step**: Add milestone to issue #3924:
```bash
gh issue edit 3924 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
```

**Completion**: After assigning milestone to issue #3924, all 44 PRs and 4 issues will have the 7.0.0-preview4 milestone assigned (100% complete).
