#!/bin/bash

###############################################################################
# Script: verify-milestone-prs.sh
# Purpose: Verify which PRs have the 7.0.0-preview4 milestone assigned
# 
# Description:
#   This script queries GitHub to check the milestone status of PRs merged
#   since v7.0.0-preview3. Useful for verifying the update script results.
#
# Prerequisites:
#   1. GitHub CLI (gh) must be installed
#   2. You must be authenticated with gh (run: gh auth login)
#
# Usage:
#   ./verify-milestone-prs.sh
#
###############################################################################

set -euo pipefail

# Configuration
REPO="dotnet/SqlClient"
MILESTONE="7.0.0-preview4"

# Color codes
GREEN='\033[0;32m'
RED='\033[0;31m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

# All PRs merged since v7.0.0-preview3
ALL_PRS=(
    3749 3772 3773 3791 3794 3797 3811 3818 3826 3829
    3837 3841 3842 3853 3854 3856 3857 3859 3864 3865
    3869 3870 3872 3879 3890 3893 3895 3897 3900 3902
    3904 3905 3906 3908 3909 3911 3912 3919 3925 3928
    3929 3932 3933 3938
)

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Milestone Verification${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "Repository: ${REPO}"
echo -e "Milestone: ${MILESTONE}"
echo -e "Total PRs to check: ${#ALL_PRS[@]}"
echo -e "${BLUE}========================================${NC}\n"

# Check if gh is installed
if ! command -v gh &> /dev/null; then
    echo -e "${RED}ERROR: GitHub CLI (gh) is not installed${NC}"
    exit 1
fi

# Check if authenticated
if ! gh auth status &> /dev/null; then
    echo -e "${RED}ERROR: Not authenticated with GitHub CLI${NC}"
    exit 1
fi

WITH_MILESTONE=0
WITHOUT_MILESTONE=0

echo -e "${BLUE}PRs WITH milestone ${MILESTONE}:${NC}"
for pr in "${ALL_PRS[@]}"; do
    milestone=$(gh pr view "${pr}" --repo "${REPO}" --json milestone --jq '.milestone.title // "none"' 2>/dev/null || echo "error")
    
    if [[ "${milestone}" == "${MILESTONE}" ]]; then
        title=$(gh pr view "${pr}" --repo "${REPO}" --json title --jq '.title' 2>/dev/null || echo "Unknown")
        echo -e "  ${GREEN}✓${NC} #${pr} - ${title}"
        ((WITH_MILESTONE++))
    fi
done

echo -e "\n${BLUE}PRs WITHOUT milestone ${MILESTONE}:${NC}"
for pr in "${ALL_PRS[@]}"; do
    milestone=$(gh pr view "${pr}" --repo "${REPO}" --json milestone --jq '.milestone.title // "none"' 2>/dev/null || echo "error")
    
    if [[ "${milestone}" != "${MILESTONE}" ]]; then
        title=$(gh pr view "${pr}" --repo "${REPO}" --json title --jq '.title' 2>/dev/null || echo "Unknown")
        echo -e "  ${RED}✗${NC} #${pr} - ${title} ${YELLOW}(current: ${milestone})${NC}"
        ((WITHOUT_MILESTONE++))
    fi
done

echo -e "\n${BLUE}========================================${NC}"
echo -e "${BLUE}Summary${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "Total PRs: ${#ALL_PRS[@]}"
echo -e "${GREEN}With milestone: ${WITH_MILESTONE}${NC}"
echo -e "${RED}Without milestone: ${WITHOUT_MILESTONE}${NC}"
echo -e "${BLUE}========================================${NC}"

if [[ ${WITHOUT_MILESTONE} -gt 0 ]]; then
    echo -e "\n${YELLOW}Tip: Run ./update-milestone-7.0.0-preview4.sh to update the missing PRs${NC}"
fi
