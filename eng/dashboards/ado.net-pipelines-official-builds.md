# Official Builds

## Triggers

The pipelines listed here operate on a variety of repos.  Use the branch links
to navigate to each repo.

The branches listed below indicate the repo branch used for the triggered runs.

|Pipeline|Branch|Config|PR Runs|Commit Runs|Scheduled Runs|Source|
|-|-|-|-|-|-|-|
|dotnet-sqlclient-Official|[internal/main](https://sqlclientdrivers.visualstudio.com/ADO.Net/_git/dotnet-sqlclient)|Release|No|No|Mon-Fri 03:30 UTC, Sun 04:30 UTC|YAML|
|Microsoft.Data.SqlClient.sni-Official|[master](https://sqlclientdrivers.visualstudio.com/ADO.Net/_git/Microsoft.Data.SqlClient.sni)|Release|No|Yes|Mon-Fri 23:00 UTC, Sun 04:00 UTC|YAML|
|akv-official|N/A|Release|No|No|No|YAML|
|mds-official|N/A|Release|No|No|TBD|YAML|
|Localization-CI|[master](https://sqlclientdrivers.visualstudio.com/ADO.Net/_git/Microsoft.Data.SqlClient), [main](https://github.com/dotnet/SqlClient)|Release|No|No|Daily 19:00 UTC|Classic UI|
|Docs-Build-Pack-Publish|[internal/main](https://sqlclientdrivers.visualstudio.com/ADO.Net/_git/dotnet-sqlclient)|Release|No|No|Mon 02:00 UTC|Classic UI|
|SNI-MDS run tests|[main](https://github.com/dotnet/SqlClient)|Release|No|No|No|Classic UI|
|dotnet-sqlclient-fuzztest|[ConfigFuzz](https://sqlclientdrivers.visualstudio.com/ADO.Net/_git/Microsoft.Data.SqlClient?path=%2F&version=GBConfigFuzz&_a=contents)|Release|No|No|Sun,Wed,Fri 01:00 UTC|Classic UI|
