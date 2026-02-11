# Bash One-Liner Reference for Milestone Verification

## Quick Verification One-Liners

### Check Only Items Missing the Milestone (Recommended)

This one-liner checks all 44 PRs and 4 issues, showing ONLY those that don't have the 7.0.0-preview4 milestone:

```bash
echo "=== Checking 44 PRs and 4 Issues ===" && echo "Items WITHOUT milestone 7.0.0-preview4:" && for pr in 3749 3772 3773 3791 3794 3797 3811 3818 3826 3829 3837 3841 3842 3853 3854 3856 3857 3859 3864 3865 3869 3870 3872 3879 3890 3893 3895 3897 3900 3902 3904 3905 3906 3908 3909 3911 3912 3919 3925 3928 3929 3932 3933 3938; do m=$(gh pr view $pr --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "NONE"' 2>/dev/null); [[ "$m" != "7.0.0-preview4" ]] && echo "  PR #$pr: $m"; done && for issue in 3716 3736 3523 3924; do m=$(gh issue view $issue --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "NONE"' 2>/dev/null); [[ "$m" != "7.0.0-preview4" ]] && echo "  Issue #$issue: $m"; done && echo "=== Verification complete ==="
```

### Check All PRs (Show All Statuses)

```bash
for pr in 3749 3772 3773 3791 3794 3797 3811 3818 3826 3829 3837 3841 3842 3853 3854 3856 3857 3859 3864 3865 3869 3870 3872 3879 3890 3893 3895 3897 3900 3902 3904 3905 3906 3908 3909 3911 3912 3919 3925 3928 3929 3932 3933 3938; do echo -n "PR #$pr: "; gh pr view $pr --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "NO MILESTONE"'; done
```

### Check All Issues (Show All Statuses)

```bash
for issue in 3716 3736 3523 3924; do echo -n "Issue #$issue: "; gh issue view $issue --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "NO MILESTONE"'; done
```

### Count Items With/Without Milestone

```bash
total=48; with=0; without=0; for pr in 3749 3772 3773 3791 3794 3797 3811 3818 3826 3829 3837 3841 3842 3853 3854 3856 3857 3859 3864 3865 3869 3870 3872 3879 3890 3893 3895 3897 3900 3902 3904 3905 3906 3908 3909 3911 3912 3919 3925 3928 3929 3932 3933 3938; do m=$(gh pr view $pr --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "NONE"' 2>/dev/null); [[ "$m" == "7.0.0-preview4" ]] && ((with++)) || ((without++)); done; for issue in 3716 3736 3523 3924; do m=$(gh issue view $issue --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "NONE"' 2>/dev/null); [[ "$m" == "7.0.0-preview4" ]] && ((with++)) || ((without++)); done; echo "Total: $total | With milestone: $with | Without: $without"
```

## Update One-Liners

### ⚠️ Update Only Issue #3924 (Only item still needing milestone)

All 44 PRs and 3 originally identified issues now have the milestone.  
Only issue #3924 still needs it:

```bash
gh issue edit 3924 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
```

### ~~Update All Items That Need Milestone~~ (✅ PRs Complete, ⚠️ One Issue Remains)

~~Update 25 PRs and 3 issues (28 total):~~ PRs are all done! Only one issue left:

```bash
gh issue edit 3924 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
```

### Update PRs Only (25 PRs)

```bash
for pr in 3749 3797 3811 3829 3841 3853 3854 3856 3859 3864 3865 3869 3879 3893 3895 3897 3900 3905 3906 3911 3919 3925 3932 3933 3938; do gh pr edit $pr --repo dotnet/SqlClient --milestone "7.0.0-preview4"; done
```

### ~~Update Issues Only (3 Issues)~~ ✅ Complete + One More

The 3 originally identified issues now have the milestone. Add milestone to issue #3924:

```bash
gh issue edit 3924 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
```

## Complete Workflow One-Liner

Check status, update if needed, verify again:

```bash
echo "=== Initial Check ===" && for pr in 3749 3772 3773 3791 3794 3797 3811 3818 3826 3829 3837 3841 3842 3853 3854 3856 3857 3859 3864 3865 3869 3870 3872 3879 3890 3893 3895 3897 3900 3902 3904 3905 3906 3908 3909 3911 3912 3919 3925 3928 3929 3932 3933 3938; do m=$(gh pr view $pr --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "NONE"' 2>/dev/null); [[ "$m" != "7.0.0-preview4" ]] && echo "  PR #$pr needs update"; done && for issue in 3716 3736 3523; do m=$(gh issue view $issue --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "NONE"' 2>/dev/null); [[ "$m" != "7.0.0-preview4" ]] && echo "  Issue #$issue needs update"; done && read -p "Update all? (y/n) " -n 1 -r && echo && [[ $REPLY =~ ^[Yy]$ ]] && (for pr in 3749 3797 3811 3829 3841 3853 3854 3856 3859 3864 3865 3869 3879 3893 3895 3897 3900 3905 3906 3911 3919 3925 3932 3933 3938; do gh pr edit $pr --repo dotnet/SqlClient --milestone "7.0.0-preview4"; done && for issue in 3716 3736 3523; do gh issue edit $issue --repo dotnet/SqlClient --milestone "7.0.0-preview4"; done) && echo "=== Verification ===" && for pr in 3749 3772 3773 3791 3794 3797 3811 3818 3826 3829 3837 3841 3842 3853 3854 3856 3857 3859 3864 3865 3869 3870 3872 3879 3890 3893 3895 3897 3900 3902 3904 3905 3906 3908 3909 3911 3912 3919 3925 3928 3929 3932 3933 3938; do m=$(gh pr view $pr --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "NONE"' 2>/dev/null); [[ "$m" != "7.0.0-preview4" ]] && echo "  PR #$pr: $m"; done && for issue in 3716 3736 3523; do m=$(gh issue view $issue --repo dotnet/SqlClient --json milestone --jq '.milestone.title // "NONE"' 2>/dev/null); [[ "$m" != "7.0.0-preview4" ]] && echo "  Issue #$issue: $m"; done && echo "Complete!"
```

## PR and Issue Lists

### All 44 PRs (Space-Separated)
```
3749 3772 3773 3791 3794 3797 3811 3818 3826 3829 3837 3841 3842 3853 3854 3856 3857 3859 3864 3865 3869 3870 3872 3879 3890 3893 3895 3897 3900 3902 3904 3905 3906 3908 3909 3911 3912 3919 3925 3928 3929 3932 3933 3938
```

### 25 PRs Needing Milestone (Space-Separated)
```
3749 3797 3811 3829 3841 3853 3854 3856 3859 3864 3865 3869 3879 3893 3895 3897 3900 3905 3906 3911 3919 3925 3932 3933 3938
```

### 4 Issues Needing Milestone (Space-Separated) - UPDATE: Now only 1!
```
3924
```

*Issues #3716, #3736, #3523 now have the milestone ✅*

## Usage Tips

1. **Copy-paste friendly**: All one-liners are on single lines for easy copying
2. **Error handling**: Uses `2>/dev/null` to suppress errors
3. **Clear output**: Shows only what's important
4. **Safe to run**: Verification commands don't modify anything

## Prerequisites

```bash
# Check if gh CLI is installed
gh --version

# Check if authenticated
gh auth status

# Authenticate if needed
gh auth login
```
