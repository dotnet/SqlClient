#!/usr/bin/env python3
"""Ingest translated perf-results NDJSON files into Azure Data Explorer (Kusto).

Runs on the pipeline agent inside an ``AzureCLI@2`` task so that the Azure DevOps
service connection's service principal is already logged in via ``az``.  The
script therefore authenticates to Kusto with *az CLI* credentials and performs a
queued ingestion of the PerfRun and PerfBenchmarkResult NDJSON files produced by
``perf_to_kusto.py``.

Queued ingestion is asynchronous: ``ingest_from_file`` only *enqueues* the data
and returns success even when the data later fails to land (e.g. a missing JSON
mapping or a schema mismatch).  Those failures surface only in Kusto's ingestion
failure system, so the pipeline step used to go green while the database stayed
empty.  To avoid that silent-failure mode this script:

  * sets ``flush_immediately`` so the data-management service seals the batch
    right away instead of waiting out the (default 5 minute) batching window, and
  * verifies, after queuing, that the expected rows actually became queryable --
    polling the target tables and, on timeout, printing ``.show ingestion
    failures`` so the real error is visible in the build log (and failing the
    step).

Requires the ``azure-kusto-ingest`` package (``pip install azure-kusto-data
azure-kusto-ingest``); the pipeline step installs it before invoking this script.
"""

import argparse
import json
import os
import sys
import time


def _engine_uri(cluster):
    """Return the engine (query) endpoint, stripping any 'ingest-' prefix."""
    if "://" in cluster:
        scheme, rest = cluster.split("://", 1)
        if rest.startswith("ingest-"):
            rest = rest[len("ingest-"):]
        return f"{scheme}://{rest}"
    return cluster


def _ingest_uri(cluster):
    """Return the data-management (ingest) endpoint used by queued ingestion."""
    if "://" in cluster:
        scheme, rest = cluster.split("://", 1)
        if not rest.startswith("ingest-"):
            return f"{scheme}://ingest-{rest}"
    return cluster


def _ingest(cluster, database, table, mapping, data_file):
    from azure.kusto.data import KustoConnectionStringBuilder
    from azure.kusto.ingest import (
        QueuedIngestClient,
        IngestionProperties,
        FileDescriptor,
    )
    from azure.kusto.data.data_format import DataFormat
    from azure.kusto.ingest import ReportLevel

    if not os.path.exists(data_file) or os.path.getsize(data_file) == 0:
        print(f"Skipping {table}: '{data_file}' is missing or empty.")
        return 0

    kcsb = KustoConnectionStringBuilder.with_az_cli_authentication(_ingest_uri(cluster))
    client = QueuedIngestClient(kcsb)

    props = IngestionProperties(
        database=database,
        table=table,
        data_format=DataFormat.MULTIJSON,
        ingestion_mapping_reference=mapping,
        report_level=ReportLevel.FailuresAndSuccesses,
        # Seal the batch immediately: perf payloads are tiny and we want the rows
        # queryable in seconds (and any verification to run promptly), not after
        # the default multi-minute batching window.
        flush_immediately=True,
    )

    descriptor = FileDescriptor(data_file, os.path.getsize(data_file))
    client.ingest_from_file(descriptor, ingestion_properties=props)
    print(f"Queued ingestion of '{data_file}' -> {database}.{table} "
          f"(mapping '{mapping}').")
    return 0


def _count_rows(path):
    """Number of non-empty NDJSON lines in a file (0 when missing)."""
    if not os.path.exists(path):
        return 0
    with open(path, "r", encoding="utf-8") as fh:
        return sum(1 for line in fh if line.strip())


def _pipeline_ids(paths):
    """Collect the distinct PipelineRunId values from PerfRun NDJSON files."""
    ids = set()
    for path in paths:
        if not os.path.exists(path):
            continue
        with open(path, "r", encoding="utf-8") as fh:
            for line in fh:
                line = line.strip()
                if not line:
                    continue
                try:
                    pid = json.loads(line).get("PipelineRunId")
                except ValueError:
                    pid = None
                if pid:
                    ids.add(str(pid))
    return ids


def _scalar(client, database, query):
    resp = client.execute(database, query)
    return int(resp.primary_results[0].rows[0][0])


def _is_authorization_error(exc):
    """True when an exception looks like a Kusto authorization/permission denial
    (as opposed to a transient network error), so the operator can be told to
    grant the querying role rather than chase a connectivity problem."""
    if exc is None:
        return False
    text = str(exc).lower()
    needles = (
        "forbidden", "unauthorized", "not authorized", "does not have permission",
        "principal", "403", "e_access", "access denied",
    )
    return any(n in text for n in needles)


def _dump_failures(client, database):
    cmd = (".show ingestion failures "
           "| where Table in ('PerfRun', 'PerfBenchmarkResult') "
           "| where FailedOn > ago(2h) "
           "| project FailedOn, Table, ErrorCode, FailureKind, Details, OperationId "
           "| order by FailedOn desc | take 25")
    try:
        resp = client.execute_mgmt(database, cmd)
        rows = resp.primary_results[0].rows
        if not rows:
            print("No recent ingestion failures reported by Kusto "
                  "(data may still be flushing).", file=sys.stderr)
            return
        print("Recent Kusto ingestion failures:", file=sys.stderr)
        for r in rows:
            print(f"  {r[0]} {r[1]} [{r[2]}/{r[3]}] {r[4]} (op {r[5]})",
                  file=sys.stderr)
    except Exception as exc:  # noqa: BLE001 - diagnostics best-effort
        print(f"  (could not query ingestion failures: {exc})", file=sys.stderr)


def _verify(cluster, database, run_table, results_table, pipeline_ids,
            expected_run, expected_results, timeout_s, interval_s):
    """Poll the target tables until the expected rows appear.

    Returns 0 on success, 1 when rows are provably missing after the timeout.
    When verification queries cannot run at all (e.g. the ingestion principal
    lacks query rights) this is best-effort: it warns and returns 0 so the step
    is not failed on a permission gap, since the ingestion itself was queued.
    """
    if not pipeline_ids or (expected_run == 0 and expected_results == 0):
        print("Nothing to verify (no rows were queued).")
        return 0

    from azure.kusto.data import KustoClient, KustoConnectionStringBuilder

    client = KustoClient(
        KustoConnectionStringBuilder.with_az_cli_authentication(_engine_uri(cluster)))

    id_list = ", ".join(f'"{i}"' for i in sorted(pipeline_ids))
    run_query = f"{run_table} | where PipelineRunId in ({id_list}) | count"
    # PerfBenchmarkResult has no PipelineRunId column, but DerivedRunId embeds it
    # as the trailing '|<PipelineRunId>' segment.
    res_conds = " or ".join(f'DerivedRunId endswith "|{i}"' for i in sorted(pipeline_ids))
    res_query = f"{results_table} | where {res_conds} | count"

    print(f"Verifying ingestion for PipelineRunId in [{id_list}] "
          f"(expecting {run_table}>={expected_run}, {results_table}>={expected_results}); "
          f"timeout {timeout_s}s ...")

    deadline = time.time() + timeout_s
    query_ever_ok = False
    last_exc = None
    run_have = res_have = -1
    while True:
        try:
            run_have = _scalar(client, database, run_query)
            res_have = _scalar(client, database, res_query)
            query_ever_ok = True
        except Exception as exc:  # noqa: BLE001 - transient/permission, retried
            last_exc = exc
            print(f"  (verification query failed, will retry: {exc})")

        if query_ever_ok:
            print(f"  {run_table}={run_have}/{expected_run}, "
                  f"{results_table}={res_have}/{expected_results}")
            if run_have >= expected_run and res_have >= expected_results:
                print("Verified: all expected rows are queryable in Kusto.")
                return 0

        if time.time() >= deadline:
            break
        time.sleep(interval_s)

    if not query_ever_ok:
        detail = str(last_exc) if last_exc is not None else "no further detail"
        if _is_authorization_error(last_exc):
            # Queued ingestion only needs the 'Database Ingestor' role; verification *queries* the
            # data back and additionally needs 'Database Viewer'.  A principal with Ingestor-only
            # rights ingests fine but cannot verify, so name the missing role instead of blaming the
            # network.  The ingestion itself was queued, so this stays a warning (exit 0).
            print("##vso[task.logissue type=warning]Kusto ingestion was queued, but the ingestion "
                  "principal is not authorized to query the database, so ingestion could not be "
                  "verified. Grant the service connection's service principal the 'Database Viewer' "
                  "role on this database (in addition to 'Database Ingestor'). "
                  f"Details: {detail}", file=sys.stderr)
        else:
            print("##vso[task.logissue type=warning]Could not verify Kusto ingestion "
                  "(query endpoint unreachable or the verification query failed). Data was queued; "
                  f"check the database manually. Details: {detail}", file=sys.stderr)
        return 0

    print(f"##vso[task.logissue type=error]Kusto ingestion did not complete: "
          f"{run_table}={run_have}/{expected_run}, "
          f"{results_table}={res_have}/{expected_results} after {timeout_s}s.",
          file=sys.stderr)
    _dump_failures(client, database)
    return 1


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cluster", required=True,
                        help="Kusto cluster URI, e.g. https://<name>.<region>.kusto.windows.net")
    parser.add_argument("--database", required=True)
    parser.add_argument("--run-file", required=True,
                        help="PerfRun NDJSON file(s), comma-separated.")
    parser.add_argument("--results-file", required=True,
                        help="PerfBenchmarkResult NDJSON file(s), comma-separated.")
    parser.add_argument("--run-table", default="PerfRun")
    parser.add_argument("--results-table", default="PerfBenchmarkResult")
    parser.add_argument("--run-mapping", default="PerfRun_json_mapping")
    parser.add_argument("--results-mapping", default="PerfBenchmarkResult_json_mapping")
    parser.add_argument("--verify-timeout", type=int,
                        default=int(os.environ.get("KUSTO_VERIFY_TIMEOUT", "300")),
                        help="Seconds to wait for ingested rows to become queryable "
                             "(0 disables verification).")
    parser.add_argument("--verify-interval", type=int,
                        default=int(os.environ.get("KUSTO_VERIFY_INTERVAL", "15")),
                        help="Polling interval in seconds for verification.")
    args = parser.parse_args(argv)

    try:
        import azure.kusto.ingest  # noqa: F401
    except ImportError:
        print("ERROR: azure-kusto-ingest is not installed. "
              "Run 'pip install azure-kusto-data azure-kusto-ingest'.",
              file=sys.stderr)
        return 2

    run_files = [f.strip() for f in args.run_file.split(",") if f.strip()]
    results_files = [f.strip() for f in args.results_file.split(",") if f.strip()]

    expected_run = sum(_count_rows(f) for f in run_files)
    expected_results = sum(_count_rows(f) for f in results_files)
    pipeline_ids = _pipeline_ids(run_files)

    for data_file in run_files:
        _ingest(args.cluster, args.database, args.run_table,
                args.run_mapping, data_file)
    for data_file in results_files:
        _ingest(args.cluster, args.database, args.results_table,
                args.results_mapping, data_file)

    print("Ingestion requests submitted (queued).")

    if args.verify_timeout <= 0:
        print("Verification disabled (--verify-timeout <= 0).")
        return 0

    return _verify(args.cluster, args.database, args.run_table, args.results_table,
                   pipeline_ids, expected_run, expected_results,
                   args.verify_timeout, args.verify_interval)


if __name__ == "__main__":
    raise SystemExit(main())
