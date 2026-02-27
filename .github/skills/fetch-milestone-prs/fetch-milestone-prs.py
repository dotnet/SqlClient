#!/usr/bin/env python3
"""
Fetch all merged PR metadata for a given GitHub milestone.

Usage:
    python fetch-milestone-prs.py <milestone> [--repo OWNER/REPO] [--output-dir DIR]

Examples:
    python fetch-milestone-prs.py 7.0.0-preview4
    python fetch-milestone-prs.py 7.0.0-preview4 --output-dir ./pr-data
    python fetch-milestone-prs.py 7.0.0-preview4 --repo dotnet/SqlClient

Requires:
    - gh CLI (https://cli.github.com/) authenticated via `gh auth login`
    - OR set GITHUB_TOKEN environment variable

Each PR is saved as a separate JSON file: <output-dir>/<pr-number>.json
A summary index is saved as: <output-dir>/_index.json
"""

import argparse
import json
import os
import subprocess
import sys
from pathlib import Path


def run_gh_api(endpoint, method="GET"):
    """Call the GitHub REST API via `gh api`."""
    cmd = ["gh", "api", "--method", method, endpoint]
    result = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, encoding="utf-8", errors="replace")
    if result.returncode != 0:
        print(f"Error calling gh api {endpoint}:", file=sys.stderr)
        print(result.stderr, file=sys.stderr)
        sys.exit(1)
    return json.loads(result.stdout)


def run_gh_api_paginated(endpoint):
    """Call the GitHub REST API with pagination via `gh api --paginate`."""
    cmd = ["gh", "api", "--paginate", endpoint]
    result = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, encoding="utf-8", errors="replace")
    if result.returncode != 0:
        print(f"Error calling gh api {endpoint}:", file=sys.stderr)
        print(result.stderr, file=sys.stderr)
        sys.exit(1)
    # --paginate concatenates JSON arrays, producing valid JSON
    return json.loads(result.stdout)


def find_milestone_number(repo, milestone_title):
    """Look up the milestone number by its title."""
    milestones = run_gh_api_paginated(
        f"/repos/{repo}/milestones?state=all&per_page=100"
    )
    for ms in milestones:
        if ms["title"] == milestone_title:
            return ms["number"]
    print(f"Milestone '{milestone_title}' not found.", file=sys.stderr)
    print("Available milestones:", file=sys.stderr)
    for ms in sorted(milestones, key=lambda m: m["title"]):
        print(f"  - {ms['title']} (#{ms['number']}, {ms['state']})", file=sys.stderr)
    sys.exit(1)


def fetch_milestone_prs(repo, milestone_number):
    """Fetch all closed (merged) PRs for the milestone."""
    # GitHub Issues API returns both issues and PRs; filter to PRs with pull_request key
    issues = run_gh_api_paginated(
        f"/repos/{repo}/issues?milestone={milestone_number}&state=closed&per_page=100"
    )
    prs = [i for i in issues if "pull_request" in i]
    return prs


def fetch_pr_details(repo, pr_number):
    """Fetch full PR details including merge info."""
    return run_gh_api(f"/repos/{repo}/pulls/{pr_number}")


def extract_pr_metadata(issue_data, pr_detail):
    """Extract the fields we care about into a clean structure."""
    labels = [l["name"] for l in issue_data.get("labels", [])]
    assignees = [a["login"] for a in issue_data.get("assignees", [])]

    merged = pr_detail.get("merged", False)
    merged_at = pr_detail.get("merged_at")
    merge_commit = pr_detail.get("merge_commit_sha")

    return {
        "number": issue_data["number"],
        "title": issue_data["title"],
        "author": issue_data["user"]["login"],
        "author_association": issue_data.get("author_association", ""),
        "labels": labels,
        "assignees": assignees,
        "state": issue_data["state"],
        "merged": merged,
        "merged_at": merged_at,
        "merge_commit_sha": merge_commit,
        "created_at": issue_data["created_at"],
        "closed_at": issue_data["closed_at"],
        "html_url": issue_data["html_url"],
        "body": issue_data.get("body", ""),
        "comments_count": issue_data.get("comments", 0),
        # Derived fields for release notes categorization
        "is_merged_pr": merged,
        "has_public_api_label": "Public API :new:" in labels,
        "has_engineering_label": any("Engineering" in l for l in labels),
        "has_test_label": any("Test" in l for l in labels),
    }


def main():
    parser = argparse.ArgumentParser(
        description="Fetch all merged PR metadata for a GitHub milestone."
    )
    parser.add_argument("milestone", help="Milestone title (e.g., '7.0.0-preview4')")
    parser.add_argument(
        "--repo",
        default="dotnet/SqlClient",
        help="GitHub repo in OWNER/REPO format (default: dotnet/SqlClient)",
    )
    parser.add_argument(
        "--output-dir",
        default=None,
        help="Output directory (default: .milestone-prs/<milestone>)",
    )
    args = parser.parse_args()

    output_dir = args.output_dir or os.path.join(
        ".milestone-prs", args.milestone
    )
    output_path = Path(output_dir)
    output_path.mkdir(parents=True, exist_ok=True)

    print(f"Looking up milestone '{args.milestone}' in {args.repo}...")
    ms_number = find_milestone_number(args.repo, args.milestone)
    print(f"  Found milestone #{ms_number}")

    print(f"Fetching PRs for milestone #{ms_number}...")
    issues = fetch_milestone_prs(args.repo, ms_number)
    print(f"  Found {len(issues)} closed PRs/issues")

    index = []
    merged_count = 0
    skipped_count = 0

    for i, issue_data in enumerate(issues):
        pr_number = issue_data["number"]
        print(
            f"  [{i+1}/{len(issues)}] Fetching PR #{pr_number}: {issue_data['title'][:60]}..."
        )

        pr_detail = fetch_pr_details(args.repo, pr_number)
        metadata = extract_pr_metadata(issue_data, pr_detail)

        if not metadata["merged"]:
            print(f"    Skipped (closed but not merged)")
            skipped_count += 1
            continue

        merged_count += 1

        # Write individual PR file
        pr_file = output_path / f"{pr_number}.json"
        with open(pr_file, "w", encoding="utf-8") as f:
            json.dump(metadata, f, indent=2, ensure_ascii=False)

        # Add to index
        index.append(
            {
                "number": metadata["number"],
                "title": metadata["title"],
                "author": metadata["author"],
                "labels": metadata["labels"],
                "merged_at": metadata["merged_at"],
                "has_public_api_label": metadata["has_public_api_label"],
                "has_engineering_label": metadata["has_engineering_label"],
                "has_test_label": metadata["has_test_label"],
            }
        )

    # Sort index by PR number
    index.sort(key=lambda x: x["number"])

    # Write index file
    index_file = output_path / "_index.json"
    with open(index_file, "w", encoding="utf-8") as f:
        json.dump(
            {
                "milestone": args.milestone,
                "repo": args.repo,
                "total_closed": len(issues),
                "total_merged": merged_count,
                "total_skipped": skipped_count,
                "prs": index,
            },
            f,
            indent=2,
            ensure_ascii=False,
        )

    print(f"\nDone! {merged_count} merged PRs saved to {output_path}/")
    print(f"  Skipped {skipped_count} closed-but-not-merged items")
    print(f"  Index: {index_file}")


if __name__ == "__main__":
    main()
