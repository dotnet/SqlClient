# Executive Summary — ICM 792529661

## Microsoft.Data.SqlClient native memory leak / OOM with `Encrypt=Strict`

**Prepared for:** Engineering leadership
**Author:** apdeshmukh
**Date:** 2026-06-05
**ICM:** 792529661 — *Sev 2.5 (declared outage), currently **MITIGATED** — fix identified and validated locally, **PR in review***
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
| **Incident state** | **Mitigated** (workaround in production); **fix identified and validated locally, PR in review** |
| **Customer impact** | Stopped — Data Sync is stable on the workaround |
| **Mitigation** | Customer switched `Encrypt=Strict` → `Encrypt=True` (Mandatory). This falls back to TLS 1.2 and does not trigger the leak. |
| **Mitigation cost** | The workaround **forfeits TLS 1.3**, which is an **SFI / security-compliance gap** for the customer — so it is acceptable only as a temporary measure, not a final state. |
| **Permanent fix** | **In hand — under code review.** Exact ownership bug identified in native SNI (`Ssl::Decrypt` TLS 1.3 `SEC_I_RENEGOTIATE` path + `Ssl::Handshake` channel-bindings re-entry). Fix is in **ADO PR `#7652`** (Saurabh Singh, *not yet merged*). I built a custom `Microsoft.Data.SqlClient.SNI.dll` from the PR branch and validated it against the in-house repro: the dominant ~32 KB-per-connection leak is **gone** (see §4 *Validation*). Because the fix lives in the shared native **SNI** layer, the same change covers both the public driver and the internal CTAIP fork. |
| **Risk** | Low while on workaround; remaining work is PR review + merge, package publish, and customer validation |

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

## 4. Root cause (identified — fix validated locally, PR in review)

Earlier in the investigation we believed the leak was **entirely** a Windows **SChannel** limitation
(TLS 1.3 session-ticket caching with no user-mode API to control it) — i.e. **not fixable** in our code.

**Deeper profiling (xperf heap stack traces) changed that conclusion.** The leak is actually **split**,
and **the majority is in our own native SNI code, not SChannel.** Critically, the **32 KB block that
dominates the customer Watson dump is the same `SNI_Packet` allocation** that dominates the xperf
repro — so the two analyses corroborate each other and point at our code:

| Source | Share of leak | Fixable by us? |
|---|---|---|
| **Our native SNI** — one `SNI_Packet` (~32 KB) retained per connection in `Tcp::ReadSync` *(= the 32 KB block seen in the Watson dump)* | **~31 KB/conn (~65%)** | **Yes — fixed in ADO PR `#7652`** |
| Windows SChannel — TLS 1.3 session/ticket state retained per connection | ~10 KB/conn (~20%) | Partially (mirror managed `SslStream` flags) |
| Other small/distributed allocations | ~6 KB/conn (~15%) | Likely |

**Why it only happens with `Encrypt=Strict`:** Strict mode wraps the whole connection in TLS 1.3
*before* any TDS traffic. That path drives an extra post-handshake leg in native SNI (TLS 1.3
`NewSessionTicket`, surfaced by Schannel as `SEC_I_RENEGOTIATE` from `DecryptMessage`) where a read
packet is never returned to the pool — one leaked ~32 KB packet per connection. TLS 1.2 (Mandatory)
delivers tickets *inside* the handshake, so it never takes this path and never leaks.

### The exact fix — ADO PR `#7652` (Saurabh Singh, not yet merged)

Two small, surgical changes in `src/Microsoft.Data.SqlClient/netfx/src/SNI/src/ssl.cpp` close the leak
end-to-end:

| # | Where | What changed | Why it fixes the leak |
|---|---|---|---|
| **1a** | `Ssl::Decrypt`, `SEC_I_RENEGOTIATE` branch | `BOOL fHasData = Buffers[1].BufferType == SECBUFFER_DATA;` → `... && Buffers[1].cbBuffer > 0;` | TLS 1.3 `NewSessionTicket` surfaces as `Buffers[1] = { SECBUFFER_DATA, cbBuffer = 0 }`. The pre-fix check treated that empty signal-buffer as “real data” and skipped the release branch — stranding the ~32 KB `SNI_Packet` allocated by `Tcp::ReadSync`. The tightened check now correctly routes this case into the release branch. |
| **1b** | Same branch | After `SNIPacketRelease(pPacket); pPacket = NULL;` also added `*ppPacket = NULL;`. Signature changed: `Decrypt(SNI_Packet* pPacket, ...)` → `Decrypt(SNI_Packet** ppPacket, ...)` (and mirrored in `Sign::Decrypt`). | The caller (`CryptoBase::DecryptLoop`, `PartialReadAsync`) still held a stale pointer to the released packet, which would have caused an access violation. Exposing the caller’s slot via the new double-pointer signature lets the release-branch null it safely. |
| **1c** | Same branch | Returns `SEC_E_INCOMPLETE_MESSAGE` instead of `ERROR_SUCCESS` after the release. | Drives the `ReadDone` loop to issue another `ReadAsync` so the connection now waits for the *real* server response instead of treating the empty NST as completed data. |
| **1d** | `CryptoBase::DecryptLoop` (lines ~430, ~437) | Updated to pass `ppNewPacket` (double-pointer) instead of `*ppNewPacket`. | Required mechanical update to match the new `Decrypt` signature. |
| **2** | `Ssl::Handshake`, call site of `SetChannelBindings` (~line 4850) | `if (!m_fIgnoreChannelBindings) { dwRet = SetChannelBindings(); ... }` → `if (!m_fIgnoreChannelBindings && NULL == m_pConn->m_pvChannelBindings) { dwRet = SetChannelBindings(); ... }` | When `Encrypt=Strict + Integrated Security=true`, the TLS 1.3 NST path re-enters `Handshake` and would re-call `SetChannelBindings()` over an already-populated `m_pvChannelBindings`, asserting/leaking. The guard skips the second call when bindings are already set. |

**Why one missing `SNIPacketRelease` cascades into ~40 KB per connection:** the leaked `SNI_Packet`
holds a `REF_Packet` reference on its owning `SNI_Conn::m_cRefTotal`. So long as that reference is
alive, `~SNI_Conn` (and therefore `~Ssl` → `DeleteSecurityContext` → `FreeCredentialsHandle`) never
runs — which is why the customer's Watson dump *also* showed the two `SPSslImportKey` allocations and
the `CreateUserContext` user-context block stuck around the same connection. **A single fix in
`Ssl::Decrypt` collapses all four leak stacks** that previously dominated the xperf top-30.

### Validation — custom-built SNI from PR `#7652`, in-house repro

I built `Microsoft.Data.SqlClient.SNI.dll` from the PR branch, dropped it into the
`StrictEncryptMemoryBenchmark` harness, and re-ran the same xperf `heap_report` workflow that
originally produced the leak fingerprint. The dominant SNI packet leak is **gone**:

| Metric | Pre-fix (master @ 1000 conns) | Post-fix (PR `#7652` @ **3000** conns) |
|---|---|---|
| Global outstanding heap (`Top Stacks by Outstanding Memory`) | **50 MB** | **7.8 MB** |
| **Per-connection outstanding** | **~50 KB** | **~2.6 KB** |
| 32 KB `SNI_Packet` via `Tcp::ReadSync` in top 30 | **1000 outstanding (100% leak)** | **absent from top 30** |
| `Ssl::Decrypt`-rooted leak stacks in top 30 | **3 entries (4.7 + 2.2 + 1.4 MB)** | **0 entries** |
| Net per-connection reduction | — | **~95%** |

The post-fix run scaled to **3× more connections** than the pre-fix run yet retained **6× *less*
outstanding native memory** — i.e. ~18× better per-connection. The residual ~2.6 KB/conn is consistent
with the Schannel and managed-runtime baseline, **not** the SNI bug.

> **Bottom line on root cause:** the exact ownership bug is identified, the fix is small (two
> targeted edits in `ssl.cpp`), the cascade explanation lines up with the Watson dump signatures,
> and the in-house repro confirms the leak is eliminated. Remaining work is **PR review + merge,
> package publish, and customer validation** — not further root-causing.

---

## 5. What we tried (and learned)

Seven Schannel-focused fix attempts (disable reconnects, session lifespan, `ApplyControlToken`,
cache flush, per-connection credentials, etc.) each reduced Schannel-side allocations by up to ~50%
but **did not move the needle on total process memory** — because they targeted only the ~20% Schannel
portion and **missed the dominant ~65% in our own packet handling.** This is exactly why the xperf
re-profiling was the turning point: it redirected effort from an unfixable external dependency to a
**fixable bug in our code** — the bug now identified and fixed in PR `#7652` (§4).

---

## 6. Path forward

| Priority | Action | Status / expected impact |
|---|---|---|
| **1 (highest ROI)** | **Land ADO PR `#7652`** — the two-edit native SNI fix in `ssl.cpp` (§4) | **In code review.** Validated locally with a custom-built `Microsoft.Data.SqlClient.SNI.dll`: per-connection outstanding native memory drops **~50 KB → ~2.6 KB (~95%)**; the four leaked-stack signatures from the customer dump are absent from the post-fix top-30 (§4 *Validation*). |
| 2 | Publish the SNI package and refresh the dependent driver builds (public driver and CTAIP fork) | One shared SNI build covers both consumers. |
| 3 | Re-apply the validated Schannel flag settings (`ApplyControlToken` + `dwSessionLifespan`) to mirror managed `SslStream` | Trims a further ~3–4 KB/conn on top of the SNI fix. |
| 4 | Validate fixed NuGet packages with the Data Sync team and close `#44851` | Confirms fix in customer workload; final step before incident closure. |
| Interim (.NET Core/8+) | Document `UseManagedSNIOnWindows=true` as an alternative workaround | Leak drops to ~2 KB/conn — redundant once the SNI fix ships, but useful for any blocked customer prior to release. |

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
| **ADO PR `#7652`** Fix memory leak in SNI for Strict / TLS 1.3 | Saurabh Singh | **In review** | The actual code fix — two edits in `ssl.cpp` (§4). Locally validated against the in-house repro: dominant leak gone. Covers both `#45390` and `#45392` once merged. |
| `#45390` Fix memory leak in SNI for Strict / TLS 1.3 | Priyanka Tiwari | Active | Tracked by PR `#7652`. |
| `#45392` **Fix the 32 KB TLS 1.3 memory leak in SNI** | (unassigned) | New | The dominant `SNI_Packet` leak — root-caused to `Ssl::Decrypt` `SEC_I_RENEGOTIATE` branch; fix in PR `#7652`. |
| `#45303` Locate actual leak in SNI on TLS 1.3 success path | Priyanka Tiwari | Active | Pinpointed (§4) — ready to close once PR `#7652` merges. |
| `#45391` Long-running Strict/TLS 1.3 stress tests + leak monitoring | Priyanka Tiwari | Active | Regression guard — will run against the merged PR. |
| `#44851` Implement & validate OOM fix (CTAIP) | Priyanka Tiwari | New | Final validation w/ customer once SNI package is refreshed. |
| `#45139` [S360] postmortem item; `#45387` draft retrospective | Apoorv Deshmukh | Active | Retrospective / fundamentals. |

**Reading of the board:** all the hard diagnostic work is closed out, the dominant SNI packet leak has
been root-caused, and the fix is **in code review (ADO PR `#7652`)** with local validation already
showing the leak gone. Because the fix lands in the shared SNI layer, it resolves both the public
driver and the CTAIP fork. The remaining open work is **PR merge, package publish, stress-test
regression run, and customer validation (`#44851`).**

> **Note:** the `#44850` cert-auth task and the earlier "new builds fail cert login" email reports were a
> **package-delivery/consumption issue** on how a dev build was handed to Data Sync — **not** a blocker for
> the OOM fix, and unrelated to the leak (which reproduces on plain username/password auth).

---

## 8. Bottom line for leadership

- **Customer is safe today** — outage mitigated, no ongoing impact — **but the workaround costs them
  TLS 1.3 (an SFI/security gap)**, so a real fix is needed to let them return to `Encrypt=Strict`.
- **Root cause identified — it is our own native SNI code.** The breakthrough was the xperf re-profiling
  that showed the leak is **mostly our own packet bug (~65%)**, not the previously-assumed unfixable
  Windows SChannel limitation — and the **32 KB block in the customer’s own dump matches the repro**,
  confirming we are chasing the right bug.
- **Fix is in hand and validated locally.** Two surgical edits in `ssl.cpp` (`Ssl::Decrypt`
  `SEC_I_RENEGOTIATE` branch + `Ssl::Handshake` channel-bindings re-entry guard) submitted as
  **ADO PR `#7652`** by Saurabh Singh. I built a custom `Microsoft.Data.SqlClient.SNI.dll` from the
  PR branch and re-ran the xperf repro: **per-connection outstanding native memory drops ~50 KB →
  ~2.6 KB (~95%)**, and the four leaked-stack signatures from the customer dump are gone from the
  top-30 — even at 3× the connection count of the original repro.
- **One fix, broad coverage:** the bug is in the **shared native SNI layer**, so PR `#7652` covers the
  **public driver** *and* the **internal CTAIP fork** — and the same code path is used by **Node Agent**
  and **Elastic Jobs**, so all of them benefit. (Cert-auth is irrelevant; the leak reproduces on plain
  username/password connections.)
- **PR is not yet merged.** Remaining work is **PR review + merge**, SNI package publish, stress-test
  regression run, and validation with the Data Sync team before closing the incident.

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
