# CI Builds by Branch

See also: [Internal CI](https://sqlclientdrivers.visualstudio.com/ADO.Net/_dashboards/dashboard/98f82958-c0c1-4a4f-9172-748291e9bb28)

## Triggers

All pipelines listed here operate on the
[GitHub SqlClient](https://github.com/dotnet/SqlClient) repo.

The branches listed below indicate the repo branch used for the triggered runs.
PR pipelines run on the topic branch associated to the PR.

|Pipeline|Branch|Config|PR Runs|Commit Runs|Scheduled Runs|Source|
|-|-|-|-|-|-|-|
|PR-SqlClient-Project|N/A|Debug|Yes|No|None|YAML|
|PR-SqlClient-Package|N/A|Debug|Yes|No|None|YAML|
|CI-SqlClient|[main](https://github.com/dotnet/SqlClient)|Release|No|Yes|Weekdays 01:00 UTC|YAML|
|CI-SqlClient-Package|[main](https://github.com/dotnet/SqlClient)|Release|No|Yes|Weekdays 03:00 UTC|YAML|
|CI-SqlClient|[release/6.1](https://github.com/dotnet/SqlClient/tree/release/6.1)|Release|No|Yes|Sunday 04:00 UTC|YAML|
|CI-SqlClient-Package|[release/6.1](https://github.com/dotnet/SqlClient/tree/release/6.1)|Release|No|Yes|Sunday 04:30 UTC|YAML|
|CI-SqlClient|[release/6.0](https://github.com/dotnet/SqlClient/tree/release/6.0)|Release|No|Yes|Sunday 06:00 UTC|YAML|
|CI-SqlClient-Package|[release/6.0](https://github.com/dotnet/SqlClient/tree/release/6.0)|Release|No|Yes|Sunday 06:30 UTC|YAML|
|Release 5.1|[release/5.1](https://github.com/dotnet/SqlClient/tree/release/5.1)|Release|Yes|Yes|Sunday 08:00 UTC|Classic UI|
