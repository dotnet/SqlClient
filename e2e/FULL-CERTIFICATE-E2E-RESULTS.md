# Full-certificate E2E results

## Result

2026-10-30: **312 PASS / 0 FAIL / 174 BLOCKED / 0 KNOWN-GAP**, across 23 runs.
No product regression was observed in the exercised cases.

The five new certificate-focused runs passed **105/105** checks. Each covers both real
SQL endpoints, synchronous and asynchronous opens, successful certificate pin validation,
pin mismatch, untrusted-chain failure, hostname mismatch and TrustServerCertificate=true,
plus the exception/trace leakage check. Successful validation here means **explicit leaf
pin validation**, not a newly established trusted CA chain.

The comparable original 18-run matrix is **207/0/174/0**, versus **329/0/52/0** previously.
Exactly **122 previously passing rows are now BLOCKED because the default SQL instance
is stopped**, including multi-server lifecycle/concurrency cases that require it.
The existing 52 Strict-related exclusions remain. The verifier compared individual
scenarios and their mode, protocol, cipher IDs, typed cipher, verdict and certificate
match/null classification; no unexpected change occurred in comparable passing rows.
The former hash-match column now represents exact certificate DER equality.

## Code and environment

- Product: slice D `cbee4b1a7b6eafd9226670d2720d893fee661abb`, with no product-source changes.
- Native packages: Microsoft.Data.SqlClient.SNI and .SNI.runtime
  `7.1.0-preview3.26303.1`, pinned exactly on this E2E branch.
  These packages must already be available in the local NuGet cache; no package binaries
  are published with this branch.
- Windows build `10.0.26300.0`, x64 client processes.
- Modern runtime: .NET `10.0.14`; legacy harness targets net462 and ran on the installed
  .NET Framework 4.8.1 runtime (release registry value `534296`).
- SQL 2022 on `tcp:localhost,50904`: `16.0.1210.3`, Developer Edition.
- SQL 2025 on `tcp:localhost,1440`: `17.0.1145.4`, Enterprise Developer Edition.
- Default instance on 1433 stayed stopped and was not probed or restarted.
- No shared server configuration, certificate, trust store or service changes; no VM starts.

The deployed x64 native DLL in **both** net10.0 and net462 output directories matched:

```text
FFC43EB7C389FDDEBE3B176AFB5C528FAA3155C0A31632D4C6AC0EAB1C4D725E
```

`dumpbin /exports` confirmed `SNIGetServerCertificateWrapper` and
`SNIGetCertValidationInfoWrapper`; the removed certificate-hash export was absent.
Each native process logged its actual loaded module path.

The stock 7.1.0 control DLL, copied into separate output directories and verified after
copy, matched:

```text
E61EE1F20BB345147F85820B0D03C2B154BF9CD6B4971708114BB4782FD35B26
```

Package-cache x86 and ARM64 DLL hashes also matched the supplied identities
(`2941FD986EE95BFB65ED530B230B55F452F90FD6E5ACF19C8D7ED936740A5937`
and `4E89C37DC0C2685134AB32676C2BA5B9D84F0CB7A359CAF9D046A4A5D9EDA9B0`).
That is identity verification only, **not x86 or ARM64 runtime coverage** of this revision.

## Matrix

| Runtime/provider | Run | PASS | FAIL | BLOCKED | KNOWN-GAP |
|---|---|---:|---:|---:|---:|
| net10 Managed | Main | 30 | 0 | 34 | 0 |
| net10 Managed | Extras | 9 | 0 | 4 | 0 |
| net10 Managed | Pool v2 extras | 9 | 0 | 4 | 0 |
| net10 Managed | Simulated no TLS | 3 | 0 | 0 | 0 |
| net10 Managed | Simulated TLS | 7 | 0 | 0 | 0 |
| net10 Managed | Real SQL certificate focus | 21 | 0 | 0 | 0 |
| net10 Native preview | Main | 30 | 0 | 34 | 0 |
| net10 Native preview | Extras | 9 | 0 | 8 | 0 |
| net10 Native preview | Pool v2 extras | 9 | 0 | 8 | 0 |
| net10 Native preview | Simulated no TLS | 3 | 0 | 0 | 0 |
| net10 Native preview | Simulated TLS | 7 | 0 | 0 | 0 |
| net10 Native preview | Real SQL certificate focus | 21 | 0 | 0 | 0 |
| net462 Native preview | Main | 30 | 0 | 34 | 0 |
| net462 Native preview | Extras | 9 | 0 | 8 | 0 |
| net462 Native preview | Pool v2 extras | 9 | 0 | 8 | 0 |
| net462 Native preview | Simulated no TLS | 3 | 0 | 0 | 0 |
| net462 Native preview | Real SQL certificate focus | 21 | 0 | 0 | 0 |
| net10 Native stock 7.1.0 | Main | 17 | 0 | 16 | 0 |
| net10 Native stock 7.1.0 | Simulated no TLS | 3 | 0 | 0 | 0 |
| net10 Native stock 7.1.0 | Real SQL certificate focus | 21 | 0 | 0 | 0 |
| net462 Native stock 7.1.0 | Main | 17 | 0 | 16 | 0 |
| net462 Native stock 7.1.0 | Simulated no TLS | 3 | 0 | 0 | 0 |
| net462 Native stock 7.1.0 | Real SQL certificate focus | 21 | 0 | 0 | 0 |
| **Total** | | **312** | **0** | **174** | **0** |

Both active servers passed their first health probe before each real-SQL run.
There were no new infrastructure-timeout exclusions. Timeout classification remains
BLOCKED, with raw errors retained locally, rather than product FAIL.

## Certificate and cipher observations

- An independent `SslStream` over TDS PRELOGIN fetched each presented leaf DER.
  Both leaf encodings were 1028 bytes. No registry/configuration certificate lookup was used.
- Each applicable `GetServerCertificate()` result matched those exact DER bytes.
  Every inspection checked multiple distinct caller-owned instances, no private key,
  identical DER across calls and unchanged surviving/new instances after disposing one.
  A retained connection snapshot also remained usable after disposing its connection.
- Managed and new Native SNI retained the leaf on untrusted-chain, hostname and pin
  validation failures, as well as successful pin validation and TSC=true connections.
- Stock SNI returned null certificates without an escaping missing-entry-point exception.
  Legacy mode/protocol/error behaviour remained unchanged, and cipher IDs stayed null.
- Both real servers negotiated **TLS 1.2**, cipher **0xC030**
  (`TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384`), matching independent probes.
  There is no claim of TLS 1.3 coverage for these servers.
- `NegotiatedCipherSuiteId` was checked on both frameworks; numeric parity with the
  .NET-only `TlsCipherSuite? NegotiatedCipherSuite` was verified in 162 emitted observations.
- Null cipher/certificate behaviour with successful mode=None was exercised using the
  repository TDS simulator, **not a real SQL Server**.
- No full DER hex, DER base64, thumbprint or binary DER event payload appeared in captured
  SqlException.Message, ToString or SqlClient EventSource traces.

The DER negative control swapped the two independently obtained certificates: all four
sync/async comparison checks failed as intended (process exit 1), with correct cipher
comparisons still present. Eight injected exception/trace leakage controls were also
rejected, including a real byte-array EventSource payload. These deliberately failing
controls are **excluded** from the product matrix.

## Remaining coverage boundaries

Real Strict/TLS 1.3 and CA-chain-validated success remain unavailable with the current
auto-generated fallback certificates. No server/certificate/trust changes were authorized.
The stopped default instance accounts for the newly blocked baseline rows.

This revision was not rerun on WS2019, WS2016, ARM64, macOS, Azure SQL, a real availability
group, or a corporate-PKI server. Earlier evidence for those environments, where present,
is for earlier product revisions and does not validate this full-certificate API.
In particular, the previously observed WS2019 native default certificate-failure capture
limitation remains unresolved; these local passes do not close it.

## Reproduction and evidence

Prerequisites: the exact preview packages in the local NuGet cache, stock 7.1.0 x64
control DLLs, .NET 10 SDK/runtime, .NET Framework, and the two running SQL instances with
Windows authentication. Do not start the default instance to reproduce these counts.

Executed successfully:

```powershell
dotnet build e2e\EncryptionInfoE2E\EncryptionInfoE2E.csproj -c Release --nologo -v:q
dotnet e2e\EncryptionInfoE2E\bin\Release\net10.0\EncryptionInfoE2E.dll --mode probe --sni managed --health-gate --skip-strict --default-stopped --certdir e2e\full-certificate-cbee4b1\certs
.\e2e\run-full-certificate.ps1
.\e2e\verify-full-certificate.ps1
.\e2e\verify-certificate-leak-controls.ps1
```

Build result: passed, with two harness-only obsolete X509Certificate2 constructor warnings.
Matrix result: 23 successful process exits and totals above. Row verifier: passed.
Leakage controls require PowerShell running on .NET 10 and passed all eight controls.
The runner refuses to overwrite existing runs/control binaries; use fresh local output
locations (`-Evidence` and `-StockOutput`) for a repeat. Its `-StockRoot` parameter selects a prepared control tree with
the stock DLLs under the same net462/net10.0 relative paths as the driver outputs.

Public evidence:

- `e2e/full-certificate-cbee4b1/summary.tsv`
- `e2e/full-certificate-cbee4b1/identity.json`

Local-only evidence retained in this worktree:

- `e2e/full-certificate-cbee4b1/net*.log` and `net*.tsv`: raw per-run output and rows.
- `e2e/full-certificate-cbee4b1/probe.log`, `certs/`: independent probes and public DER fixtures.
- `e2e/full-certificate-cbee4b1/baseline-changes.tsv`: 122 infrastructure-related verdict changes.
- `e2e/full-certificate-cbee4b1/negative-der.*` and `negative-leak-controls.*`.
- `e2e/cipher-id-a5e71a/`: prior baseline; unchanged and not republished.

Publication intentionally excludes private native package binaries, historical lab scripts,
raw internal logs and certificate fixtures. Their local copies and historical commits are
preserved; the public E2E branch is a clean squashed delta on the exact slice D commit.
No slice PR or user-edited review document was changed.
