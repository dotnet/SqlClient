#!/bin/bash
# Script to move all items from one milestone to another in the dotnet/SqlClient repository
# Usage: ./move-milestone-items.sh <source-milestone> <target-milestone>
#
# Example:
#   ./move-milestone-items.sh "7.0.0-preview5" "7.0.0-preview4"

set -e

REPO="dotnet/SqlClient"

if [ $# -ne 2 ]; then
    echo "Usage: $0 <source-milestone> <target-milestone>"
    echo "Example: $0 '7.0.0-preview5' '7.0.0-preview4'"
    exit 1
fi

SOURCE_MILESTONE="$1"
TARGET_MILESTONE="$2"

echo "Moving all items from milestone '$SOURCE_MILESTONE' to '$TARGET_MILESTONE' in repo $REPO"
echo ""

# Check if gh is installed
if ! command -v gh &> /dev/null; then
    echo "Error: gh CLI is not installed. Please install it first."
    exit 1
fi

# Check if jq is installed
if ! command -v jq &> /dev/null; then
    echo "Error: jq is not installed. Please install it first."
    exit 1
fi

# List items to be moved
echo "Fetching items from milestone '$SOURCE_MILESTONE'..."
ITEMS=$(gh issue list --repo "$REPO" --milestone "$SOURCE_MILESTONE" --state all --limit 1000 --json number | jq -r '.[].number')

if [ -z "$ITEMS" ]; then
    echo "No items found in milestone '$SOURCE_MILESTONE'"
    exit 0
fi

ITEM_COUNT=$(echo "$ITEMS" | wc -l)
echo "Found $ITEM_COUNT item(s) to move"
echo ""

# Confirm before proceeding
read -p "Do you want to proceed with moving these items? (y/N) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Aborted."
    exit 0
fi

# Move items
echo "Moving items..."
COUNTER=0
for ITEM_NUM in $ITEMS; do
    COUNTER=$((COUNTER + 1))
    echo "[$COUNTER/$ITEM_COUNT] Moving issue/PR #$ITEM_NUM..."
    gh issue edit "$ITEM_NUM" --repo "$REPO" --milestone "$TARGET_MILESTONE"
    # Small delay to avoid rate limiting
    sleep 0.5
done

echo ""
echo "Successfully moved $ITEM_COUNT item(s) from '$SOURCE_MILESTONE' to '$TARGET_MILESTONE'"
echo ""
echo "Verification:"
echo "  Source milestone: gh issue list --repo $REPO --milestone \"$SOURCE_MILESTONE\" --state all"
echo "  Target milestone: gh issue list --repo $REPO --milestone \"$TARGET_MILESTONE\" --state all"
