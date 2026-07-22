#!/usr/bin/env python3
"""Ingest translated perf-results NDJSON files into Azure Data Explorer (Kusto).

Runs on the pipeline agent inside an ``AzureCLI@2`` task so that the Azure DevOps
service connection's service principal is already logged in via ``az``.  The
script therefore authenticates to Kusto with *az CLI* credentials and performs a
queued ingestion of the PerfRun and PerfBenchmarkResult NDJSON files produced by
``perf_to_kusto.py``.

Requires the ``azure-kusto-ingest`` package (``pip install azure-kusto-data
azure-kusto-ingest``); the pipeline step installs it before invoking this script.
"""

import argparse
import os
import sys


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

    # Queued ingestion targets the data-management ("ingest-") endpoint.
    ingest_uri = cluster
    if "://" in cluster:
        scheme, rest = cluster.split("://", 1)
        if not rest.startswith("ingest-"):
            ingest_uri = f"{scheme}://ingest-{rest}"

    kcsb = KustoConnectionStringBuilder.with_az_cli_authentication(ingest_uri)
    client = QueuedIngestClient(kcsb)

    props = IngestionProperties(
        database=database,
        table=table,
        data_format=DataFormat.MULTIJSON,
        ingestion_mapping_reference=mapping,
        report_level=ReportLevel.FailuresAndSuccesses,
    )

    descriptor = FileDescriptor(data_file, os.path.getsize(data_file))
    client.ingest_from_file(descriptor, ingestion_properties=props)
    print(f"Queued ingestion of '{data_file}' -> {database}.{table} "
          f"(mapping '{mapping}').")
    return 0


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
    args = parser.parse_args(argv)

    try:
        import azure.kusto.ingest  # noqa: F401
    except ImportError:
        print("ERROR: azure-kusto-ingest is not installed. "
              "Run 'pip install azure-kusto-data azure-kusto-ingest'.",
              file=sys.stderr)
        return 2

    for data_file in [f.strip() for f in args.run_file.split(",") if f.strip()]:
        _ingest(args.cluster, args.database, args.run_table,
                args.run_mapping, data_file)
    for data_file in [f.strip() for f in args.results_file.split(",") if f.strip()]:
        _ingest(args.cluster, args.database, args.results_table,
                args.results_mapping, data_file)

    print("Ingestion requests submitted (queued).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
