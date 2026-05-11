---
name: fetch-milestone-prs
description: Fetches all merged pull requests for a given GitHub milestone and saves their metadata as individual JSON files. Use this skill when asked to retrieve PRs for a milestone, gather PR data for release notes, or collect milestone PR metadata.
---

This skill retrieves all merged pull requests associated with a GitHub milestone and saves each PR's metadata as a separate JSON file for downstream processing (e.g., release notes generation, changelog creation).

## When to Use This Skill

- User asks to fetch or retrieve PRs for a milestone
- User wants to gather PR data before generating release notes
- User needs milestone PR metadata for analysis or categorization
- User mentions a milestone name and wants to see what PRs were included
- As a prerequisite step before invoking the `release-notes` prompt

## Prerequisites

- The `gh` CLI must be installed and authenticated (`gh auth login`)
- Python 3.8+ must be available

## Instructions

1. **Identify the milestone name** from the user's request. This is typically a version string like `7.0.0-preview4`, `6.1.5`, etc.

2. **Identify the repository** (optional). Defaults to `dotnet/SqlClient`. If the user specifies a different repo, pass it via `--repo`.

3. **Run the fetch script** located in this skill's directory:

   ```bash
   python .github/skills/fetch-milestone-prs/fetch-milestone-prs.py <milestone> [--repo OWNER/REPO] [--output-dir DIR]
   ```

   Examples:
   ```bash
   # Default: outputs to .milestone-prs/<milestone>/
   python .github/skills/fetch-milestone-prs/fetch-milestone-prs.py 7.0.0-preview4

   # Custom output directory
   python .github/skills/fetch-milestone-prs/fetch-milestone-prs.py 7.0.0-preview4 --output-dir ./my-prs

   # Different repo
   python .github/skills/fetch-milestone-prs/fetch-milestone-prs.py 7.0.0-preview4 --repo dotnet/efcore
   ```

4. **Verify the output** by checking the generated `_index.json` file in the output directory. It contains a summary of all fetched PRs.

5. **Report results** to the user:
   - Total merged PRs found
   - Total skipped (closed but not merged)
   - Output directory path
   - Mention that each PR is in a separate `<number>.json` file and the index is at `_index.json`

## Output Format

### Directory structure
```
.milestone-prs/<milestone>/
├── _index.json          # Summary index of all PRs
├── 1234.json            # Individual PR metadata
├── 1235.json
└── ...
```

### Individual PR file (`<number>.json`)
Each file contains:
```json
{
  "number": 1234,
  "title": "PR title",
  "author": "github-username",
  "author_association": "MEMBER",
  "labels": ["label1", "label2"],
  "assignees": ["user1"],
  "state": "closed",
  "merged": true,
  "merged_at": "2026-01-15T12:00:00Z",
  "merge_commit_sha": "abc123...",
  "created_at": "2026-01-10T10:00:00Z",
  "closed_at": "2026-01-15T12:00:00Z",
  "html_url": "https://github.com/dotnet/SqlClient/pull/1234",
  "body": "Full PR description markdown...",
  "comments_count": 5,
  "is_merged_pr": true,
  "has_public_api_label": false,
  "has_engineering_label": false,
  "has_test_label": true
}
```

### Index file (`_index.json`)
```json
{
  "milestone": "7.0.0-preview4",
  "repo": "dotnet/SqlClient",
  "total_closed": 76,
  "total_merged": 74,
  "total_skipped": 2,
  "prs": [
    {
      "number": 1234,
      "title": "PR title",
      "author": "github-username",
      "labels": ["label1"],
      "merged_at": "2026-01-15T12:00:00Z",
      "has_public_api_label": false,
      "has_engineering_label": false,
      "has_test_label": true
    }
  ]
}
```

## Derived Fields

The script computes these boolean flags for easy categorization:

| Field | Logic |
|-------|-------|
| `has_public_api_label` | Any label contains "Public API :new:" |
| `has_engineering_label` | Any label contains "Engineering" |
| `has_test_label` | Any label contains "Test" |

## Error Handling

- If the milestone is not found, the script prints available milestones and exits. Ask the user to verify the milestone name.
- If `gh` is not authenticated, the script will fail with an auth error. Instruct the user to run `gh auth login`.
- If a specific PR detail fetch fails, the script exits. This is rare but can happen with API rate limits — suggest waiting and retrying.
- On Windows, if encoding errors occur, the script uses `errors="replace"` to handle non-UTF-8 characters gracefully.

## Using Output with Other Workflows

The output files are designed to be consumed by:
- The `release-notes` prompt — tag the output directory (e.g., `@.milestone-prs/7.0.0-preview4/*`) when invoking it
- Manual analysis — read `_index.json` for a quick overview, individual files for PR details
- Any script or agent that needs structured PR metadata
