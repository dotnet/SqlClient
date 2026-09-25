# Sources and adaptation notes

.NET sources reviewed on 2026-09-22; `microsoft/mssql-rs` sources on 2026-09-23.
Upstream links are pinned to the revisions examined so the origin of the guidance
stays auditable. They are examples of review practice, not authority over
SqlClient's policy or its current implementation.

## Lessons from other .NET repositories

| Verified source | Lesson used here |
| --- | --- |
| [runtime model-facing review skill][runtime-review] | Inspect callers, callees, sibling implementations, and platform variants; deduplicate findings and avoid speculative/CI-generated noise. |
| [runtime contribution guide][runtime-contributing] | Preserve both API signatures and behavior; include regression coverage. |
| [runtime API additions][runtime-api] and [reference-source guidance][runtime-ref] | Review implementation, reference surface, and tests together. Use SqlClient's actual ref/build layout, not runtime's commands. |
| [runtime coding style][runtime-style] | Existing local conventions take precedence over broad style preferences. |
| [runtime performance guidance][runtime-performance] | Consider allocation size/count, branching, and total work in library hot paths. |
| [runtime library testing][runtime-tests] | Platform/framework/architecture differences need relevant test selection, not assumptions from one local pass. |
| [EF Core contribution guide][ef-contributing] | Test the regression and distinguish source, binary, and behavioral compatibility. |
| [EF Core Copilot guidance][ef-copilot] | Read implementation rather than guessing from names or transplanting patterns; check affected API/test surfaces. |
| [EF Core build/test prerequisites][ef-tests] | Database-dependent tests require the actual database environment; unavailable infrastructure is not passing coverage. |

**SqlClient-specific synthesis:** the evidence gate, driver risk index, separate
coverage-gap reporting, severity rubric, and automated publication gates are
adaptations for this skill. Runtime's skill also permits plausible unconfirmed
risks; this skill deliberately uses a stricter gate for automated inline findings.
Neither repository mandates this exact output format or publishing policy.

Do not copy runtime's multi-model orchestration, API approval machinery, current
TFMs, or temporary validation workarounds. Do not copy EF's public-by-default
type policy, query baselines, specification-test hierarchy, or test-runner commands.
Do not turn application-level ADO.NET advice into an invariant of driver internals.

## Lessons from microsoft/mssql-rs

The Rust TDS driver's guidance is useful because it records both false-positive
review patterns and overlooked wire-state failures. These adaptations are integrated
into the workflow, driver checks, and reporting guide rather than a separate review.

| Verified source | SqlClient adaptation |
| --- | --- |
| [Review skill: process and verification][mssql-review] | Read inline threads, review bodies, and top-level replies. Use current-head CI evidence, equivalent baseline runs, and optional isolated mutations to investigate whether tests really guard the change. Recheck dated facts rather than copying budgets or failure lists. |
| [Review skill: overlooked failures][mssql-review] | Trace serialization errors across actual flush boundaries, including command/transaction/connection consequences. Check worked examples and tests whose setup, expected-value calculation, or unrelated error makes them pass vacuously. |
| [Review skill: performance and reviewer adjudication][mssql-review] | Require timing for timing claims; distinguish scans/copies/allocations. Verify another reviewer's diagnosis, proposed fix, and cited locations independently. |
| [ODBC engineering instructions: parity and entry points][mssql-odbc] | Record the intended SqlClient baseline and measurement configuration. Trace caller normalization, settings round trips, public entry points, native ownership, and what a test can actually observe on the wire. |
| [Repository Copilot instructions][mssql-instructions] | Check existing cross-platform implementations and CI before proposing rejection guards. Verify affected components are actually in the selected build/test scope, and reuse existing fixtures and simulated servers. |
| [Review posting guide][mssql-posting] | Distinguish a top-level review from inline delivery; verify posted comments with pagination and the review ID, using the host's authorized channel. |

Do not import Cargo/Tokio commands, Rust future-size budgets, ODBC SQLSTATE or
Driver Manager rules, binding-specific FFI macros, coverage percentages, mandatory
issue-link rules, or unpublished/pre-1.0 compatibility exceptions. SqlClient is a
shipped ADO.NET provider: its supported contracts and MS-TDS remain authoritative,
not another driver's behavior. Native SNI ownership must follow its own ABI.
Do not copy mutation commands into read-only workflows, auto-start SQL Server, or
require a mutation experiment for every coverage request. Unavailable private
reference sources do not invalidate a defect independently established in SqlClient.

## Official documentation for deeper questions

Only in workflows that authorize external documentation access, use Learn search
when the applicable contract/version is unclear, then fetch the relevant page or
MS-TDS section. The draft-only `code-review` prompt does not allow these lookups:
use supplied documentation or report the evidence gap. These queries locate public
contracts; they do not replace reading the implementation. Never include private
diffs or logs in queries.

| Question | Lookup | Starting page |
| --- | --- | --- |
| Pool identity, clearing, or reuse | Search `"SQL Server connection pooling" "Microsoft.Data.SqlClient"` | [Connection pooling][pooling] |
| Sync/async and legacy API differences | Fetch the linked page, then the affected API's reference page | [Asynchronous programming][async] |
| Timeout meaning and zero semantics | Search `"Microsoft.Data.SqlClient.SqlCommand.CommandTimeout"` | [CommandTimeout][timeout] |
| Connection retry versus command replay | Search `"Configurable retry logic" "SqlClient"` | [Configurable retry logic][retry] |
| Packet/token framing or cancellation | `MS-TDS packet header ATTENTION acknowledgment` | [MS-TDS specification][tds] |

Broad searches can return unrelated pages or the older provider. Keep the quoted
phrases above, or fetch the known starting page directly when results are noisy.

Search via `microsoft_docs_search`, fetch via `microsoft_docs_fetch`, and use
`microsoft_code_sample_search` with C# only when an API usage example is needed.
The CLI fallback is documented in [SKILL.md](../SKILL.md). Verify snippets against
the reviewed provider/framework before using them as evidence.

[runtime-review]: https://github.com/dotnet/runtime/blob/206bf81aa71b157cb03ae7ed1a42d1ed7d3aa2dc/.github/skills/code-review/SKILL.md
[runtime-contributing]: https://github.com/dotnet/runtime/blob/206bf81aa71b157cb03ae7ed1a42d1ed7d3aa2dc/CONTRIBUTING.md
[runtime-api]: https://github.com/dotnet/runtime/blob/206bf81aa71b157cb03ae7ed1a42d1ed7d3aa2dc/docs/coding-guidelines/adding-api-guidelines.md
[runtime-ref]: https://github.com/dotnet/runtime/blob/206bf81aa71b157cb03ae7ed1a42d1ed7d3aa2dc/docs/coding-guidelines/updating-ref-source.md
[runtime-style]: https://github.com/dotnet/runtime/blob/206bf81aa71b157cb03ae7ed1a42d1ed7d3aa2dc/docs/coding-guidelines/coding-style.md
[runtime-performance]: https://github.com/dotnet/runtime/blob/206bf81aa71b157cb03ae7ed1a42d1ed7d3aa2dc/docs/coding-guidelines/performance-guidelines.md
[runtime-tests]: https://github.com/dotnet/runtime/blob/206bf81aa71b157cb03ae7ed1a42d1ed7d3aa2dc/docs/workflow/testing/libraries/testing.md
[ef-contributing]: https://github.com/dotnet/efcore/blob/ce3905a358af0b60d24e0b15fa9da9b2e809cc11/.github/CONTRIBUTING.md
[ef-copilot]: https://github.com/dotnet/efcore/blob/ce3905a358af0b60d24e0b15fa9da9b2e809cc11/.github/copilot-instructions.md
[ef-tests]: https://github.com/dotnet/efcore/blob/ce3905a358af0b60d24e0b15fa9da9b2e809cc11/docs/getting-and-building-the-code.md
[mssql-review]: https://github.com/microsoft/mssql-rs/blob/f64092bf0354ce7ca3603d3145ce03e794311d87/.github/skills/code-review/SKILL.md
[mssql-odbc]: https://github.com/microsoft/mssql-rs/blob/f64092bf0354ce7ca3603d3145ce03e794311d87/.github/instructions/mssql-odbc.instructions.md
[mssql-instructions]: https://github.com/microsoft/mssql-rs/blob/f64092bf0354ce7ca3603d3145ce03e794311d87/.github/copilot-instructions.md
[mssql-posting]: https://github.com/microsoft/mssql-rs/blob/f64092bf0354ce7ca3603d3145ce03e794311d87/.github/skills/code-review/posting.md
[pooling]: https://learn.microsoft.com/sql/connect/ado-net/sql-server-connection-pooling
[async]: https://learn.microsoft.com/sql/connect/ado-net/asynchronous-programming
[timeout]: https://learn.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlcommand.commandtimeout
[retry]: https://learn.microsoft.com/sql/connect/ado-net/configurable-retry-logic
[tds]: https://learn.microsoft.com/openspecs/windows_protocols/ms-tds
