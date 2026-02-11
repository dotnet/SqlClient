#!/bin/bash

###############################################################################
# Script: verify-all-milestones.sh
# Purpose: Verify all PRs and issues have 7.0.0-preview4 milestone
# 
# Description:
#   Checks all 44 PRs merged since v7.0.0-preview3 and 3 related issues
#   to confirm they have the 7.0.0-preview4 milestone assigned.
#
# Prerequisites:
#   1. GitHub CLI (gh) must be installed
#   2. You must be authenticated with gh (run: gh auth login)
#
# Usage:
#   ./verify-all-milestones.sh
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

# All 44 PRs merged since v7.0.0-preview3
ALL_PRS=(
    3749 3772 3773 3791 3794 3797 3811 3818 3826 3829
    3837 3841 3842 3853 3854 3856 3857 3859 3864 3865
    3869 3870 3872 3879 3890 3893 3895 3897 3900 3902
    3904 3905 3906 3908 3909 3911 3912 3919 3925 3928
    3929 3932 3933 3938
)

# Issues closed by those PRs
ALL_ISSUES=(
    3716  # Fixed by PRs 3749, 3857, 3854
    3736  # Fixed by PR 3912
    3523  # Fixed by PR 3929
    3924  # Fixed by PR 3938 (newly discovered)
)

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Milestone Verification Report${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "Repository: ${REPO}"
echo -e "Milestone: ${MILESTONE}"
echo -e "PRs to check: ${#ALL_PRS[@]}"
echo -e "Issues to check: ${#ALL_ISSUES[@]}"
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

echo -e "${GREEN}✓ GitHub CLI is installed and authenticated${NC}\n"

# Verify PRs
PRS_WITH_MILESTONE=0
PRS_WITHOUT_MILESTONE=0
PRS_WITH_MILESTONE_LIST=()
PRS_WITHOUT_MILESTONE_LIST=()

echo -e "${BLUE}Checking PRs...${NC}"
for pr in "${ALL_PRS[@]}"; do
    milestone=$(gh pr view "${pr}" --repo "${REPO}" --json milestone --jq '.milestone.title // "none"' 2>/dev/null || echo "error")
    
    if [[ "${milestone}" == "${MILESTONE}" ]]; then
        ((PRS_WITH_MILESTONE++))
        PRS_WITH_MILESTONE_LIST+=("$pr")
    else
        ((PRS_WITHOUT_MILESTONE++))
        PRS_WITHOUT_MILESTONE_LIST+=("$pr")
        title=$(gh pr view "${pr}" --repo "${REPO}" --json title --jq '.title' 2>/dev/null || echo "Unknown")
        echo -e "  ${YELLOW}⚠${NC} PR #${pr} - Missing milestone (current: ${milestone})"
        echo -e "     ${title}"
    fi
done

# Verify Issues
ISSUES_WITH_MILESTONE=0
ISSUES_WITHOUT_MILESTONE=0
ISSUES_WITH_MILESTONE_LIST=()
ISSUES_WITHOUT_MILESTONE_LIST=()

echo -e "\n${BLUE}Checking Issues...${NC}"
for issue in "${ALL_ISSUES[@]}"; do
    milestone=$(gh issue view "${issue}" --repo "${REPO}" --json milestone --jq '.milestone.title // "none"' 2>/dev/null || echo "error")
    
    if [[ "${milestone}" == "${MILESTONE}" ]]; then
        ((ISSUES_WITH_MILESTONE++))
        ISSUES_WITH_MILESTONE_LIST+=("$issue")
    else
        ((ISSUES_WITHOUT_MILESTONE++))
        ISSUES_WITHOUT_MILESTONE_LIST+=("$issue")
        title=$(gh issue view "${issue}" --repo "${REPO}" --json title --jq '.title' 2>/dev/null || echo "Unknown")
        echo -e "  ${YELLOW}⚠${NC} Issue #${issue} - Missing milestone (current: ${milestone})"
        echo -e "     ${title}"
    fi
done

# Print Summary
echo -e "\n${BLUE}========================================${NC}"
echo -e "${BLUE}Summary${NC}"
echo -e "${BLUE}========================================${NC}"

echo -e "\n${BLUE}PRs Status:${NC}"
echo -e "  Total PRs: ${#ALL_PRS[@]}"
echo -e "  ${GREEN}With milestone: ${PRS_WITH_MILESTONE}${NC}"
if [[ ${PRS_WITHOUT_MILESTONE} -gt 0 ]]; then
    echo -e "  ${RED}Without milestone: ${PRS_WITHOUT_MILESTONE}${NC}"
    echo -e "  ${YELLOW}Missing PRs: ${PRS_WITHOUT_MILESTONE_LIST[*]}${NC}"
else
    echo -e "  ${GREEN}✓ All PRs have the milestone!${NC}"
fi

echo -e "\n${BLUE}Issues Status:${NC}"
echo -e "  Total Issues: ${#ALL_ISSUES[@]}"
echo -e "  ${GREEN}With milestone: ${ISSUES_WITH_MILESTONE}${NC}"
if [[ ${ISSUES_WITHOUT_MILESTONE} -gt 0 ]]; then
    echo -e "  ${RED}Without milestone: ${ISSUES_WITHOUT_MILESTONE}${NC}"
    echo -e "  ${YELLOW}Missing Issues: ${ISSUES_WITHOUT_MILESTONE_LIST[*]}${NC}"
else
    echo -e "  ${GREEN}✓ All issues have the milestone!${NC}"
fi

echo -e "\n${BLUE}Overall Status:${NC}"
if [[ ${PRS_WITHOUT_MILESTONE} -eq 0 ]] && [[ ${ISSUES_WITHOUT_MILESTONE} -eq 0 ]]; then
    echo -e "  ${GREEN}✓✓✓ ALL ITEMS HAVE THE MILESTONE! ✓✓✓${NC}"
    EXIT_CODE=0
else
    echo -e "  ${RED}✗ Some items are missing the milestone${NC}"
    echo -e "  ${YELLOW}Run the update scripts to fix:${NC}"
    echo -e "    ./update-milestone-7.0.0-preview4.sh"
    echo -e "    ./simple-issues-milestone-update.sh"
    EXIT_CODE=1
fi

echo -e "${BLUE}========================================${NC}\n"

exit ${EXIT_CODE}
