# SqlClient OpenTelemetry dashboard demo (local only)

This demo runs the SqlClient OpenTelemetry PoC end to end and shows its spans and metrics on a
local dashboard. It follows the JDBC user-endpoint demo in microsoft/mssql-jdbc#3106
(`docs/otel-poc/user-endpoint-demo`). The workload uses the private per-connection route
(`OTel Endpoint=http://127.0.0.1:4318/v1/traces`) and needs no OpenTelemetry SDK in the app.

| Service | Image | Local URL |
| --- | --- | --- |
| SQL Server 2022 Developer | `mcr.microsoft.com/mssql/server:2022-CU20-ubuntu-22.04` | `tcp:127.0.0.1,14333` |
| OTel Collector (OTLP http) | `otel/opentelemetry-collector-contrib:0.111.0` | `http://127.0.0.1:4318` |
| Prometheus | `prom/prometheus:v2.55.0` | <http://127.0.0.1:9090> |
| Jaeger | `jaegertracing/all-in-one:1.62.0` | <http://127.0.0.1:16686> |
| Grafana (anonymous viewer) | `grafana/grafana:11.2.2` | <http://127.0.0.1:3000/d/sqlclient-user-endpoint> |

All ports are bound to `127.0.0.1`. Starting the SQL Server container accepts its license
(<https://go.microsoft.com/fwlink/?linkid=857698>).

## Prerequisites

- Docker Desktop with Linux containers, and Docker Compose v2.
- The .NET 10 SDK.
- Free local ports: 3000, 4318, 8889, 9090, 13133, 14333 and 16686.

## Run

From this folder:

```powershell
.\run.ps1                         # start the stack, then run the workload for 180 s
.\run.ps1 -Seconds 600            # run the workload for longer
.\run.ps1 -Action Workload        # rerun only the workload against the running stack
.\run.ps1 -Action Down            # stop and remove the containers, network and volumes
```

`run.ps1` generates the SQL password in process memory and passes it only through environment
variables. It never writes the password to a file or prints it. A workload rerun reads the
password back from the owned container. Local Docker administrators can still see it with
`docker inspect`, which is why this is a local demo only.

## Workload

Each loop iteration runs these scenarios, with a 500 ms pause between iterations:

| Scenario | Frequency | Expected outcome |
| --- | --- | --- |
| `OpenAsync` + `SELECT 1` | every iteration | success |
| `Open` + `WAITFOR DELAY '00:00:02'` | every 3rd | success, ~2 s statement |
| `OpenAsync` + `SELECT * FROM` | every 3rd | SQL error 102, `query_syntax_semantics` |
| Unknown login | every 4th | SQL error 18456, `authentication` |
| Unknown database | every 4th | SQL error 4060, `database_open` |

The service name is set by `OTEL_SERVICE_NAME=sqlclient-poc`.

## What to look at

- **Grafana**: the "SqlClient OTel Endpoint (private route)" dashboard has the same panels as
  the JDBC demo, filtered on `job="sqlclient-user-endpoint"` and the `mssql.*` metric labels.
- **Jaeger**: search service `sqlclient-poc`. Each trace has a `connection.open` or
  `statement.execute` root with child phase spans. Failed spans have status Error, with
  `error.type` (for example `sqlserver.18456`) and `sqlclient.error.category`. Span
  attributes keep the provisional `sqlclient.*` names; only the metric labels use `mssql.*`.
- **Prometheus**: for example, query
  `sum by (mssql_performance_activity, mssql_error_category) (db_client_operation_error_count_total)`.

## Known gaps

- The retry panel stays empty because the workload sets `ConnectRetryCount=0` and has no
  transient failures, so `db.client.operation.retry.count` is never recorded.
- On Windows, the native SNI hides DNS resolution and the socket connect. As a result,
  `connection.dns` and `connection.socket_connect` appear only with managed SNI, for example
  on Linux or with `Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows`.
- The JDBC demo's custom live UI (`fdh-ui`) is not included. It hardcodes the `mssql-jdbc`
  service and the JDBC span names.
- The private route exports `db.client.operation.duration` with seconds-based buckets
  (0.1 ms to 30 s), the same ones the driver's meter advises. Data exported before this
  change used the SDK's millisecond defaults. Mixing the two skews the p95 panel until the old
  series age out of the window, so run `.\run.ps1 -Action Down` first if you upgrade.
