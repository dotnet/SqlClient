# Executive Summary — ICM 792529661

## Microsoft.Data.SqlClient native memory leak / OOM with `Encrypt=Strict`

**Prepared for:** Engineering leadership
**Author:** apdeshmukh
**Date:** 2026-06-03
**ICM:** 792529661 — *Sev 2.5 (declared outage), currently **MITIGATED***
**Owning team:** SQL Server .NET Driver (Microsoft.Data.SqlClient)
**Reporting customer:** Azure SQL DB **Data Sync** service (`DataSyncHost.exe`), using the internal **CTAIP** driver build **5.2.0.9**
**Originating incident:** 786523343 (Data Sync) → driver incident 792529661
**Also at risk (same code path / architecture):** SQL DB **Node Agent** and **Elastic Jobs**

---

## 1. The situation in one paragraph

The Azure **Data Sync** service hit recurring **out-of-memory crashes** in production. Each
SQL connection opened with **`Encrypt=Strict`** (TDS 8.0 / TLS 1.3) on Windows **native SNI**
leaks roughly **~47 KB of native (unmanaged) memory** that is never reclaimed — even after the
connection is closed and disposed. Under Data Sync's high connection-churn workload this
accumulates to **~950 MB and climbing** over a few hours, eventually exhausting process memory.
Data Sync consumes SqlClient via the internal **CTAIP** build (5.2.0.9); the same leak path is
shared by **Node Agent** and **Elastic Jobs**. The leak surfaced only recently because Data Sync
**switched to `Encrypt=Strict`** (to meet a TLS 1.3 / SFI security requirement) — the CTAIP version
itself was unchanged for ~2 years. The incident was declared a **Sev 2.5 outage** and is currently
**mitigated via a workaround**, with a code fix under active development.

> **Why this matters beyond Data Sync:** the leak lives in the shared **native SNI layer**, not in the
> CTAIP-specific managed code (see §3). We reproduced it on the **public Microsoft.Data.SqlClient
> driver** with plain username/password auth — so the fix is **one SNI change that covers both the
> public driver and the internal CTAIP fork.**

---

## 2. Current status

| Item | Status |
|---|---|
| **Incident state** | **Mitigated** (workaround in production) |
| **Customer impact** | Stopped — Data Sync is stable on the workaround |
| **Mitigation** | Customer switched `Encrypt=Strict` → `Encrypt=True` (Mandatory). This falls back to TLS 1.2 and does not trigger the leak. |
| **Mitigation cost** | The workaround **forfeits TLS 1.3**, which is an **SFI / security-compliance gap** for the customer — so it is acceptable only as a temporary measure, not a final state. |
| **Permanent fix** | Not yet in hand. We have **narrowed the leak to the native SNI layer on the Strict/TLS 1.3 path**, but the **exact allocation site has not yet been pinpointed and the fix is still to be determined** (tracked by ADO `#45392`). When made, the fix will be in the shared native **SNI** layer, so a single change covers both the public driver and the internal CTAIP fork. |
| **Risk** | Low while on workaround; root-causing the exact SNI site and fix is the remaining work |

---

## 3. Customer impact & evidence

### Two-driver context (important)

There are **two drivers** in play, and they **share the same native SNI layer**:

| Driver | What it is | Cert-auth / CTAIP? | Used by Data Sync? |
|---|---|---|---|
| **Public Microsoft.Data.SqlClient** | The shipping public driver | No | No (but reproduces the leak) |
| **CTAIP fork** | Forked from the public driver ~2 years ago in ADO; adds certificate-based auth for internal infra | Yes | **Yes** |

Data Sync reported the OOM on the **CTAIP fork**, *and also* confirmed it on plain **username/password**
connections (not just cert auth). We reproduced the leak on the **public driver** with username/password
+ `Encrypt=Strict` and confirmed it is in the **native SNI layer** — **not** in either driver's managed
code. **Consequence: one SNI-layer fix resolves the leak for both the public driver and the CTAIP fork.**
The cert-auth functionality is irrelevant to the leak.

### Evidence

- **Confirmed in a customer Watson dump** (`DataSyncHost.exe`, 5h48m uptime):
  - **950 MB** leaked on the native process heap (**67% of all committed memory**).
  - Managed (.NET) heap was **< 1 MB** — proving the leak is **native**, not in our managed code.
  - The dominant leaked block is **32 KB**, recurring **~17,000 times** (~58% of the leak) — one
    per connection. *(The customer independently reported the same signature: tens of thousands of
    32 KB objects.)* The dump alone could not attribute that 32 KB block to a specific function
    (PageHeap was off, so no call stacks) — that attribution came from the in-house repro below.
- **Reproduced in-house** with purpose-built benchmark apps (SqlClient, System.Data.Odbc, native ODBC,
  and a direct-SNI harness). The repro **reproduces the same 32 KB-per-connection leak** seen in the
  customer dump — which is what makes it a faithful stand-in for the production issue — and let us
  profile it (xperf) to **attribute that 32 KB block to our own native SNI code** (see §4).

**Reproduction matrix (confirmed):**

| Configuration | Leak |
|---|---|
| Native SNI + `Encrypt=Strict` (TLS 1.3) | **~47 KB/connection** |
| Native SNI + `Encrypt=Mandatory` (TLS 1.2) | None |
| **Managed** SNI + `Encrypt=Strict` (TLS 1.3) | **~2 KB/connection** (~24× better) |

---

## 4. Root cause (updated — this is the key finding)

Earlier in the investigation we believed the leak was **entirely** a Windows **SChannel** limitation
(TLS 1.3 session-ticket caching with no user-mode API to control it) — i.e. **not fixable** in our code.

**Deeper profiling (xperf heap stack traces) changed that conclusion.** The leak is actually **split**,
and **the majority is in our own native SNI code, not SChannel.** Critically, the **32 KB block that
dominates the customer Watson dump is the same `SNI_Packet` allocation** that dominates the xperf
repro — so the two analyses corroborate each other and point at our code:

| Source | Share of leak | Fixable by us? |
|---|---|---|
| **Our native SNI** — one `SNI_Packet` (~32 KB) retained per connection in `Tcp::ReadSync` *(= the 32 KB block seen in the Watson dump)* | **~31 KB/conn (~65%)** | **Yes** |
| Windows SChannel — TLS 1.3 session/ticket state retained per connection | ~10 KB/conn (~20%) | Partially (mirror managed `SslStream` flags) |
| Other small/distributed allocations | ~6 KB/conn (~15%) | Likely |

**Why it only happens with `Encrypt=Strict`:** Strict mode wraps the whole connection in TLS 1.3
*before* any TDS traffic. That path appears to drive an extra handshake/renegotiation leg in native SNI
where a read packet's buffer is never returned to the pool — one leaked ~32 KB packet per connection.
TLS 1.2 (Mandatory) does not take this path, so it does not leak.

> **Still open — the exact leak site is not yet pinpointed.** Profiling tells us the dominant ~32 KB
> block is an `SNI_Packet` allocated on the native read path, but we have **not yet identified the precise
> line/ownership bug in SNI that drops it, nor confirmed the fix.** Identifying the exact location *and*
> implementing the fix is tracked under ADO **`#45392`** — this is the primary open engineering task.

**Why managed SNI is ~24× better:** Managed `SslStream` reuses pooled buffers across all connections,
so it has near-zero per-connection native cost. Native SNI grows its pool instead of reusing it.

---

## 5. What we tried (and learned)

Seven Schannel-focused fix attempts (disable reconnects, session lifespan, `ApplyControlToken`,
cache flush, per-connection credentials, etc.) each reduced Schannel-side allocations by up to ~50%
but **did not move the needle on total process memory** — because they targeted only the ~20% Schannel
portion and **missed the dominant ~65% in our own packet handling.** This is exactly why the xperf
re-profiling was the turning point: it redirected effort from an unfixable external dependency to a
**fixable bug in our code.**

---

## 6. Path forward

| Priority | Action | Expected impact |
|---|---|---|
| **1 (highest ROI)** | **Pinpoint the exact leaked `SNI_Packet` site** on the TLS 1.3 read path and fix it (native SNI) — tracked by `#45392`; the precise location/fix is **not yet determined** | **~31 KB/conn → ~0 (expected)** |
| 2 | Re-apply the validated Schannel flag settings (`ApplyControlToken` + `dwSessionLifespan`) to mirror managed `SslStream` | trims a further ~3–4 KB/conn |
| 3 | Validate fixed NuGet packages with the Data Sync team | confirm fix in customer workload |
| Interim (.NET Core/8+) | Document `UseManagedSNIOnWindows=true` as an alternative workaround | leak drops to ~2 KB/conn |

**Note on .NET Framework:** The customer (Data Sync) runs on **.NET Framework**, which has **no managed-SNI
fallback** — so the native code fix (Action 1) is the real, durable solution for them. The `Encrypt=True`
workaround holds in the meantime.

---

## 7. Work-tracking status (ADO Issue #45067 — 23 child tasks)

The investigation is tracked under [ADO Issue 45067](https://sqlclientdrivers.visualstudio.com/ADO.Net/_workitems/edit/45067)
with 23 child tasks. **The investigation, reproduction, and profiling phases are essentially complete
(16 tasks closed); the remaining open work is the actual SNI code fix, stress tests, and the retrospective.**

**Done (closed):**

| Theme | Tasks |
|---|---|
| Reproduce the leak locally (Strict/TDS 8.0) | `#45068` |
| Cross-driver confirmation via ODBC + native C++/SNI repros | `#45069`, `#45073` |
| Profile to find the leak — **xperf `heap_report` was decisive** (VS profiler = managed only; PerfView/UMDH inconclusive) | `#45071` |
| Investigate SChannel-side fixes | `#45070` |
| **Cross-validate lab repro against Watson dump** production numbers (the 32 KB match) | `#45299` |
| Isolate leak to the TLS 1.3 path (disable TLS 1.3 test) | `#45300` |
| Confirm leak is per-fresh-handshake (pooling matrix) | `#45301` |
| Document disproved fix attempts (do-not-retry) | `#45302` |
| Handoff package: docs + repro tooling | `#45304` |
| SNI release/5.2.5 official build; early CTAIP root-cause + package delivery; Sev management | `#44932`, `#44162`, `#44163`, `#44948`, `#44990` |

**In progress / remaining (active + new):**

| Task | Owner | State | Notes |
|---|---|---|---|
| `#45390` Fix memory leak in SNI for Strict / TLS 1.3 | Priyanka Tiwari | Active | Primary fix work item |
| `#45392` **Fix the 32 KB TLS 1.3 memory leak in SNI** | (unassigned) | New | The dominant `SNI_Packet` leak — the ~65% / 32 KB block |
| `#45303` Locate actual leak in SNI on TLS 1.3 success path | Priyanka Tiwari | Active | Pinpointing exact allocation site |
| `#45391` Long-running Strict/TLS 1.3 stress tests + leak monitoring | Priyanka Tiwari | Active | Regression guard |
| `#44851` Implement & validate OOM fix (CTAIP) | Priyanka Tiwari | New | Final validation w/ customer |
| `#45139` [S360] postmortem item; `#45387` draft retrospective | Apoorv Deshmukh | Active | Retrospective / fundamentals |

**Reading of the board:** all the hard diagnostic work is closed out, and the team has correctly
split the remaining fix into the **dominant 32 KB SNI packet leak (`#45392`)** plus the **SChannel-side
residual (`#45390`/`#45303`)** — matching the root-cause split in §4. Because the fix lands in the shared
SNI layer, it resolves both the public driver and the CTAIP fork. The remaining open work is landing the
SNI fix, adding the stress-test regression guard, and validating with the customer (`#44851`).

> **Note:** the `#44850` cert-auth task and the earlier "new builds fail cert login" email reports were a
> **package-delivery/consumption issue** on how a dev build was handed to Data Sync — **not** a blocker for
> the OOM fix, and unrelated to the leak (which reproduces on plain username/password auth).

---

## 8. Bottom line for leadership

- **Customer is safe today** — outage mitigated, no ongoing impact — **but the workaround costs them
  TLS 1.3 (an SFI/security gap)**, so a real fix is needed to let them return to `Encrypt=Strict`.
- **We now know the leak is mostly in our own code (so it is fixable by us).** The breakthrough was
  discovering the leak is **mostly our own native SNI packet bug (~65%)**, not the previously-assumed
  unfixable Windows SChannel limitation — and the **32 KB block in the customer's own dump matches the
  repro**, confirming we are chasing the right bug.
- **The exact leak site and fix are still to be determined.** We have narrowed it to an `SNI_Packet` on
  the native TLS 1.3 read path, but **have not yet pinpointed the precise ownership bug or confirmed a
  fix.** ADO **`#45392`** was opened specifically to identify the exact location and implement the fix —
  this is the main remaining engineering task, with a secondary Schannel tuning pass to follow.
- **One fix, broad coverage:** the bug is in the **shared native SNI layer**, so a single fix covers the
  **public driver** *and* the **internal CTAIP fork** — and the same code path is used by **Node Agent**
  and **Elastic Jobs**, so all of them benefit. (Cert-auth is irrelevant; the leak reproduces on plain
  username/password connections.)
- **Next milestone:** land the native SNI packet fix, ship a validated NuGet package, and confirm with
  the Data Sync team before downgrading/closing the incident.

---

## Supporting artifacts

| Artifact | Location |
|---|---|
| Watson dump findings | `.copilot-session-notes/icm-792529661-watson-dump-findings.md` |
| Investigation running notes (incl. xperf finding) | `.copilot-session-notes/icm-792529661-strict-oom.md` |
| Technical deep-dive | `docs/icm-792529661-investigation-summary.md` |
| Repro benchmarks | `tools/StrictEncryptMemoryBenchmark`, `tools/OdbcMemoryBenchmark`, `tools/OdbcMemoryBenchmarkNative`, `tools/SniNativeRepro` |
| Data Sync team email thread | `docs/Re Reg Datasync OOM issue when using CTAIP MDS client.txt` |
| ADO work item | [Issue 45067](https://sqlclientdrivers.visualstudio.com/ADO.Net/_workitems/edit/45067) |
