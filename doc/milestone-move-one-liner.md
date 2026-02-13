# Move Milestone Items - Bash One-Liner

## The One-Liner

To move all items (issues and PRs) from milestone `7.0.0-preview5` to `7.0.0-preview4`:

```bash
gh issue list --repo dotnet/SqlClient --milestone "7.0.0-preview5" --state all --limit 1000 --json number | jq -r '.[].number' | xargs -I {} gh issue edit {} --repo dotnet/SqlClient --milestone "7.0.0-preview4"
```

## Explanation

This one-liner performs the following steps:

1. **`gh issue list --repo dotnet/SqlClient --milestone "7.0.0-preview5" --state all --limit 1000 --json number`**
   - Lists all issues and PRs in the `7.0.0-preview5` milestone
   - `--state all` includes both open and closed items
   - `--limit 1000` ensures we get all items (adjust if needed)
   - `--json number` outputs only the issue/PR numbers in JSON format

2. **`jq -r '.[].number'`**
   - Extracts the issue/PR numbers from the JSON output
   - `-r` flag outputs raw strings (without quotes)
   - Each number is output on a separate line

3. **`xargs -I {} gh issue edit {} --repo dotnet/SqlClient --milestone "7.0.0-preview4"`**
   - Takes each issue/PR number from the previous command
   - Executes `gh issue edit` for each number
   - Updates the milestone to `7.0.0-preview4`

## Prerequisites

- GitHub CLI (`gh`) must be installed and authenticated
- Appropriate permissions on the dotnet/SqlClient repository
- `jq` command-line JSON processor must be installed

## Alternative: With Progress Indication

If you want to see progress as items are moved:

```bash
gh issue list --repo dotnet/SqlClient --milestone "7.0.0-preview5" --state all --limit 1000 --json number | jq -r '.[].number' | xargs -I {} sh -c 'echo "Moving issue #{}..." && gh issue edit {} --repo dotnet/SqlClient --milestone "7.0.0-preview4"'
```

## Alternative: Move Only Open Items

To move only open issues/PRs:

```bash
gh issue list --repo dotnet/SqlClient --milestone "7.0.0-preview5" --state open --limit 1000 --json number | jq -r '.[].number' | xargs -I {} gh issue edit {} --repo dotnet/SqlClient --milestone "7.0.0-preview4"
```

## Alternative: Without jq (using grep/awk)

If `jq` is not available:

```bash
gh issue list --repo dotnet/SqlClient --milestone "7.0.0-preview5" --state all --limit 1000 | awk '{print $1}' | xargs -I {} gh issue edit {} --repo dotnet/SqlClient --milestone "7.0.0-preview4"
```

## Verification

To verify the move was successful:

```bash
# Check items remaining in source milestone
gh issue list --repo dotnet/SqlClient --milestone "7.0.0-preview5" --state all

# Check items in destination milestone
gh issue list --repo dotnet/SqlClient --milestone "7.0.0-preview4" --state all
```

## Safety Considerations

1. **Dry Run**: First list the items to see what will be moved:
   ```bash
   gh issue list --repo dotnet/SqlClient --milestone "7.0.0-preview5" --state all --limit 1000
   ```

2. **Backup**: Consider documenting the current state before moving:
   ```bash
   gh issue list --repo dotnet/SqlClient --milestone "7.0.0-preview5" --state all --limit 1000 > backup-preview5-items.txt
   ```

3. **Rate Limiting**: The GitHub API has rate limits. If you have many items, add a small delay:
   ```bash
   gh issue list --repo dotnet/SqlClient --milestone "7.0.0-preview5" --state all --limit 1000 --json number | jq -r '.[].number' | xargs -I {} sh -c 'gh issue edit {} --repo dotnet/SqlClient --milestone "7.0.0-preview4" && sleep 1'
   ```

## Notes

- The `gh issue list` command returns both issues and pull requests (PRs are treated as issues in the GitHub API)
- The `gh issue edit` command works for both issues and pull requests
- Milestone names are case-sensitive and must match exactly
- If a milestone doesn't exist, the command will fail with an error
