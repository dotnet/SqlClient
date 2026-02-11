#!/bin/bash
# Simple script to execute gh commands for issue milestone updates
# No error handling - just runs each command sequentially
# Repository: dotnet/SqlClient
# Milestone: 7.0.0-preview4

gh issue edit 3716 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh issue edit 3736 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
gh issue edit 3523 --repo dotnet/SqlClient --milestone "7.0.0-preview4"
