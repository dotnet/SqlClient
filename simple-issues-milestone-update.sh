#!/bin/bash
# Simple script to execute gh commands for issue milestone updates
# Repository: dotnet/SqlClient
# Milestone: 7.0.0-preview4
# Updated: 2026-02-11 (After verification against current GitHub state)

# UPDATE: Issues #3716, #3736, #3523 already have the milestone ✅
# Only issue #3924 needs the milestone now:

gh issue edit 3924 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
