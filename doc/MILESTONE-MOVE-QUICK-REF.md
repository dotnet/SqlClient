# Quick Reference: Move Items Between Milestones

## The One-Liner (Copy-Paste Ready)

```bash
gh issue list --repo dotnet/SqlClient --milestone "7.0.0-preview5" --state all --limit 1000 --json number | jq -r '.[].number' | xargs -I {} gh issue edit {} --repo dotnet/SqlClient --milestone "7.0.0-preview4"
```

## Alternative: Using the Script

```bash
./tools/move-milestone-items.sh "7.0.0-preview5" "7.0.0-preview4"
```

For detailed documentation, see [milestone-move-one-liner.md](./milestone-move-one-liner.md)
