# Fundamental Improvements — Architectural Analysis

Deeper, architectural analysis of Microsoft.Data.SqlClient async/data-path design — the structural
opportunities *beneath* the [04-quick-wins](../04-quick-wins/README.md) tactical fixes. Where quick
wins are low-blast-radius patches, these explore whether whole layers are load-bearing or incidental,
and what a cleaner foundation could look like.

(Named "improvements" rather than "flaws" — the goal is forward-looking redesign, not blame.)

Test coverage and unit-test effort for these designs is analyzed in
[07-test-coverage](../07-test-coverage/README.md). The measurement rig, baselines, and regression
guards these changes must be validated against are specified in
[06-benchmarking](../06-benchmarking/README.md).

## Analyses

| # | Topic |
| --- | --- |
| [01](01-managed-sni-and-read-path.md) | Managed SNI abstraction value, the copy chain, zero-copy gaps, streaming vs buffering, and `ExecuteReader` variants |
| [02](02-zero-copy-thin-reader.md) | A concrete zero-copy read path and thin transport reader — `Memory<byte>` reads, in-place header overlay, collapsing the staging copy, and a thin TCP path with MARS/TLS branches |

## Reference (protocol background)

Shared explainers we refer to when arguing *why* a fundamental change is worthwhile:

| Doc | Topic |
| --- | --- |
| [transports-tcp-vs-named-pipes](reference/transports-tcp-vs-named-pipes.md) | Which transports exist, where they are available, and when each is used |
| [tds-7.4-tls-over-tds](reference/tds-7.4-tls-over-tds.md) | Legacy TLS-over-TDS encryption flow (`0x12` tunnelling) |
| [tds-8.0-tls-first](reference/tds-8.0-tls-first.md) | Strict / TLS-first encryption flow (ALPN `tds/8.0`) |
| [mars-session-multiplexing](reference/mars-session-multiplexing.md) | MARS and the SMUX demultiplexer |
| [ssrp](reference/ssrp.md) | SQL Server Resolution Protocol (UDP 1434 instance lookup) |

## Method

Findings are grounded in a source-level investigation of
`src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/` (managed SNI, TDS parser, reader). File
and line references are relative to that source root. Graphify reports and the 01-initial analysis
provide corroborating structure.
