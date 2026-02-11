#!/bin/bash

###############################################################################
# Script: update-milestone-7.0.0-preview4.sh
# Purpose: Update PRs merged since v7.0.0-preview3 with 7.0.0-preview4 milestone
# 
# Description:
#   This script uses the GitHub CLI (gh) to add the 7.0.0-preview4 milestone
#   to 25 PRs that were merged to main after the v7.0.0-preview3 tag but
#   don't currently have the milestone assigned.
#
# Prerequisites:
#   1. GitHub CLI (gh) must be installed
#   2. You must be authenticated with gh (run: gh auth login)
#   3. You must have write access to the dotnet/SqlClient repository
#
# Usage:
#   ./update-milestone-7.0.0-preview4.sh [--dry-run]
#
# Options:
#   --dry-run    Show what would be done without making changes
#
# Generated: 2026-02-11
# Tag: v7.0.0-preview3 (2025-12-08, commit: 5e14b56572f7c1700ee8bf8eb492cec1de9a79be)
###############################################################################

set -euo pipefail

# Configuration
REPO="dotnet/SqlClient"
MILESTONE="7.0.0-preview4"
DRY_RUN=false

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Parse command line arguments
if [[ $# -gt 0 ]] && [[ "$1" == "--dry-run" ]]; then
    DRY_RUN=true
    echo -e "${YELLOW}DRY RUN MODE - No changes will be made${NC}\n"
fi

# PRs that need the 7.0.0-preview4 milestone
# These were merged to main after v7.0.0-preview3 (2025-12-08) but lack the milestone
PRS=(
    3749  # Fixing NullReferenceException issue with SqlDataAdapter
    3797  # Use global.json to restrict .NET SDK use
    3811  # Add ADO pipeline dashboard summary tables
    3829  # Add 7.0.0-preview3 release notes and release note generation prompt.
    3841  # Introduce app context switch for setting MSF=true by default
    3853  # Fix LocalAppContextSwitches race conditions in tests
    3854  # Revert "Fixing NullReferenceException issue with SqlDataAdapter (#3749)"
    3856  # Test | Add flaky test quarantine zone
    3859  # Minor improvements to Managed SNI tracing
    3864  # Add Release compile step to PR pipelines
    3865  # Stress test pipeline: Add placeholder
    3869  # Tests | SqlError, SqlErrorCollection
    3879  # Release Notes for 5.1.9
    3893  # Fix CodeCov upload issues
    3895  # Add release notes for 6.1.4
    3897  # Add release notes for 6.0.5
    3900  # Cleanup, Merge | Revert public visibility of internal interop enums
    3905  # Reduce default test job timeout to 60 minutes
    3906  # Fail tests that run for more than 10 minutes
    3911  # Retired 5.1 pipelines, added some missing SNI pipelines.
    3919  # Updated 1ES inventory config to the latest schema.
    3925  # Create stub pipeline files for Abstractions and Azure packages
    3932  # Common MDS | Cleanup Manual Tests
    3933  # Fix MDS Official Pipeline
    3938  # Prevent actions from running in forks
)

# Counters
TOTAL=${#PRS[@]}
SUCCESS=0
FAILED=0
SKIPPED=0

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Milestone Update Script${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "Repository: ${REPO}"
echo -e "Milestone: ${MILESTONE}"
echo -e "Total PRs to update: ${TOTAL}"
echo -e "${BLUE}========================================${NC}\n"

# Check if gh is installed
if ! command -v gh &> /dev/null; then
    echo -e "${RED}ERROR: GitHub CLI (gh) is not installed${NC}"
    echo "Please install it from: https://cli.github.com/"
    exit 1
fi

# Check if authenticated
if ! gh auth status &> /dev/null; then
    echo -e "${RED}ERROR: Not authenticated with GitHub CLI${NC}"
    echo "Please run: gh auth login"
    exit 1
fi

echo -e "${GREEN}✓ GitHub CLI is installed and authenticated${NC}\n"

# Function to update a single PR
update_pr() {
    local pr_number=$1
    
    echo -n "Processing PR #${pr_number}... "
    
    # Get PR title for better logging
    local pr_title
    if pr_title=$(gh pr view "${pr_number}" --repo "${REPO}" --json title --jq '.title' 2>/dev/null); then
        echo ""
        echo -e "  ${BLUE}Title:${NC} ${pr_title}"
        
        if [[ "${DRY_RUN}" == true ]]; then
            echo -e "  ${YELLOW}[DRY RUN]${NC} Would update milestone to: ${MILESTONE}"
            ((SKIPPED++))
        else
            # Update the milestone
            if gh pr edit "${pr_number}" --repo "${REPO}" --milestone "${MILESTONE}" 2>&1; then
                echo -e "  ${GREEN}✓ Successfully updated milestone${NC}"
                ((SUCCESS++))
            else
                echo -e "  ${RED}✗ Failed to update milestone${NC}"
                ((FAILED++))
            fi
        fi
    else
        echo -e "${RED}✗ Failed to fetch PR details${NC}"
        ((FAILED++))
    fi
    
    echo ""
}

# Process each PR
for pr in "${PRS[@]}"; do
    update_pr "${pr}"
    # Add a small delay to avoid rate limiting
    sleep 0.5
done

# Print summary
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Summary${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "Total PRs: ${TOTAL}"

if [[ "${DRY_RUN}" == true ]]; then
    echo -e "${YELLOW}Dry run - would update: ${SKIPPED}${NC}"
else
    echo -e "${GREEN}Successfully updated: ${SUCCESS}${NC}"
    if [[ ${FAILED} -gt 0 ]]; then
        echo -e "${RED}Failed: ${FAILED}${NC}"
    fi
fi
echo -e "${BLUE}========================================${NC}"

# Exit with appropriate code
if [[ ${FAILED} -gt 0 ]] && [[ "${DRY_RUN}" == false ]]; then
    exit 1
fi

exit 0
