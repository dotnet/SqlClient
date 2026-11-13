# OpenAsync experiment archive

This directory preserves the compact study outputs with the experimental PR, independently
of the focused automatic-performance-pipeline PR.

- `authenticated-report.html`: self-contained WSL SQL RTT report with inline SVG graphs.
- `authenticated-findings.md`: findings, limitations, and subsequent coverage improvements.
- `wsl-sql-rtt/summary.csv`: 240 authenticated Linux experiments, including V1/V2/non-pooled.
- `report.html` and `findings.md`: earlier Windows/negative-control study; its historical
  statement that the authenticated sweep is pending is superseded by the authenticated report.
- `sql-baseline/` and `stalled-controls/`: summaries of that earlier 63-run study.
- `path-coverage-managed/` and `path-coverage-native/`: 40 single-repetition Windows
  execution validations of the expanded success-path matrix, not a comparative performance claim.
- `measured-OpenAsyncHealthExperiment.cs`: exact earlier measurement source, before the
  first-worker retention correction; the active source is under PerformanceTests.

Stable case IDs in CSVs are expanded into full configuration names in the authenticated report.
The reports retain the measured revision and assembly-hash provenance. All graphics are inline
in the HTML; standalone SVGs are included for reuse.

The large raw JSON and log archives remain attached in the originating session's Library:
`authenticated-zero-rtt-evidence.zip`, `authenticated-latency-evidence.zip`, and the earlier
`evidence.zip`. They are deliberately not stored as large generated Git blobs. This compact
archive preserves the source, findings, all summary rows, and graph inputs, not every raw probe.
