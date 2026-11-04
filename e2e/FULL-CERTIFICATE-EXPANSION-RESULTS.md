# Full-certificate E2E expansion

## Result

2026-11-04: **1,285 PASS / 4 FAIL / 104 KNOWN-GAP / 60 OBSERVE** on slice D
`b3ed4d29d14fce448c5d14cc2695e865a56523ee`, across 49 authoritative runs.
The four failures reproduce the existing Windows Server 2019 Native SNI issue 5;
they are not reclassified as passes. No new full-certificate regression was observed
in the exercised paths.

A separate two-run smoke test on rebased D
`e6797d32093c5e608b90b83310e10d14856a132d` passed **42/42** checks.
Do not combine that smoke result with the older revision's platform coverage.

The requested matrix is **not entirely closed**: 24 Windows trusted-CA profile
configurations could not run without changing host trust, WS2019 Strict could not
reach the loopback-only SQL container, and an actual revocation-rejection path was
not exercised. These coverage boundaries are detailed below. There were no
infrastructure-blocked rows within the executed runs; unexecuted configurations
are reported separately rather than assigned invented test-row counts.

This report supplements [the October 30 host report](FULL-CERTIFICATE-E2E-RESULTS.md).
It does not replace that report or claim that old hash-API results validate this API.
Only this evidence summary is published; product source, slice PRs, private packages,
raw evidence, fixture keys and local orchestration scripts are not changed/published.

## Matrix and counting

| Area | Runs | PASS | FAIL | KNOWN-GAP | OBSERVE |
|---|---:|---:|---:|---:|---:|
| Windows x64, SQL container, untrusted CA | 24 | 688 | 0 | 104 | 36 |
| Linux x64 Managed net10, SQL container, trusted and untrusted CA | 16 | 496 | 0 | 0 | 24 |
| Windows x86 runtime, host SQL 1440 and 50904 | 3 | 63 | 0 | 0 | 0 |
| Windows Server 2019 x64, temporary VM-local SQL | 6 | 38 | 4 | 0 | 0 |
| **b3ed4d29d total** | **49** | **1,285** | **4** | **104** | **60** |
| e6797d320, separate Windows x64 host smoke | 2 | 42 | 0 | 0 | 0 |

These are raw TSV verdicts. The console harness's historical `PASS=` counter also
includes `OBSERVE`; it is not used for the strict PASS totals above.
An observation row checks certificate ownership/DER, cipher consistency and unexpected
exceptions, but does not assert the connection outcome or certificate-validation verdict.
Known-gap rows must match an explicit alternative expectation, including exact DER.

The final container matrix covers good, expired, wrong-EKU and revoked leaves, each
with ForceEncryption=0 and 1. Windows uses Managed net10, Native net10 and Native
net462; Linux uses Managed net10, each with and without temporary container CA trust.
All connection scenarios run both `Open` and `OpenAsync`.

| Container client | Certificate profile | PASS | FAIL | KNOWN-GAP | OBSERVE |
|---|---|---:|---:|---:|---:|
| Windows x64, untrusted | Good | 182 | 0 | 16 | 0 |
| Windows x64, untrusted | Expired | 142 | 0 | 56 | 12 |
| Windows x64, untrusted | Wrong EKU | 182 | 0 | 16 | 0 |
| Windows x64, untrusted | Revoked | 182 | 0 | 16 | 24 |
| Linux Managed, both trust states | Good | 124 | 0 | 0 | 0 |
| Linux Managed, both trust states | Expired | 124 | 0 | 0 | 8 |
| Linux Managed, both trust states | Wrong EKU | 124 | 0 | 0 | 0 |
| Linux Managed, both trust states | Revoked | 124 | 0 | 0 | 16 |

The initial revoked-profile harness returned before hostname and Optional/forced-
encryption checks. That local harness limitation was removed and all ten revoked
configurations were rerun. `complete-*` results replace, not supplement, their earlier
rows in the authoritative totals. Both sets of raw evidence are retained locally.
Thus 61 physical runs produced 51 authoritative runs including the newer-head smoke.

## What was asserted

For every applicable connection row:

- The leaf was fetched independently over the wire by a raw TDS 7/SslStream probe
  and, where supported, a direct TDS 8 TLS probe, not from registry/DMV metadata.
- `GetServerCertificate().RawData` matched that independently presented DER exactly.
- Repeated calls returned distinct caller-owned instances without private keys.
  Disposing one instance did not invalidate another or a subsequently returned instance.
  Connection snapshots were inspected after the connection had been disposed.
- Mode, observed TLS protocol, numeric cipher ID and validation/chain status were checked
  against the scenario expectation. On net10 the typed cipher property matched the
  numeric ID; net462 exposed only the numeric property.
- Exception `Message`, `ToString` and captured EventSource payloads contained no
  certificate DER, DER base64, DER hex or certificate thumbprint.

There were **1,360 exact DER-matching connection rows on b3ed4d29d**, plus 40 in the
newer-head smoke. Four additional WS2019 native failure rows had no certificate;
these are the four FAILs, not successful ownership/capture checks.
All **51** run-level exception/trace leakage checks passed.

Container scenarios include correct and wrong pins under Strict and Mandatory,
TrustServerCertificate=true, Strict ignoring that trust override, normal chain
validation, matching/wrong HostNameInCertificate, IP SAN on Windows, MARS, Optional
login-only encryption and Optional upgraded by server ForceEncryption.
Expired, wrong-EKU, untrusted, hostname and wrong-pin failure paths retained matching
certificates except for the explicitly listed WS2019 default native limitation.

The x86 runs were actual `ProcessArchitecture=X86`, not merely export inspection.
Each provider/TFM passed 21 checks across both host servers: trust bypass, correct pin,
wrong pin, untrusted chain, hostname mismatch, sync/async and leakage.

## Failures and existing limitations

### Four raw FAILs: WS2019 Native SNI issue 5

Native net10 and Native net462 each fail two certificate-capture expectations
(sync and async) for default Mandatory validation of the untrusted self-signed leaf:

- SQL connection correctly fails closed with `SEC_E_UNTRUSTED_ROOT` (`-2146893019`).
- Mode, TLS protocol, cipher ID, validation verdict and chain status are null.
- `GetServerCertificate()` returns null.

This reproduces the previously established WS2019 Schannel deferred-validation
limitation, now on the full-certificate API. It is an existing native/OS-path product
limitation, not an infrastructure failure, missing export or newly introduced DER-copy
regression. It remains unresolved.

On the same VM, both native builds successfully loaded and invoked
`SNIGetServerCertificateWrapper`; an invalid/null connection returned
`ERROR_INVALID_PARAMETER` (87), as expected. Actual TSC, correct-pin, wrong-pin,
explicit hostname-validation and trusted-success connection paths returned matching DER.
Managed net10 passed all 14 VM checks. Each native target passed 12 and failed 2.

### 104 existing native behavior gaps

- **64 hostname-verdict rows:** Native SNI did not report the wrong
  HostNameInCertificate when the actual endpoint name matched the certificate.
  With the untrusted CA these connections still failed with ChainError, but lacked
  the expected HostNameMismatch flag.
- **40 expired-pin rows:** Native SNI enforced leaf expiry before pin comparison,
  returning ChainError/NotTimeValid for expired correct/wrong pins and MARS pin cases.
  Managed SNI accepted an exact pin or reported the wrong pin.

These match alternative expectations from the earlier native baseline, rather than
new full-certificate failures. Every one retained the independently matching DER.
They remain visible as KNOWN-GAP, not PASS.

### Revocation observations are not rejection coverage

The refreshed CRL was valid for the run, and an independent OpenSSL CRL verification
rejected the revoked leaf with error 23, `certificate revoked`. The good leaf verified.
Nevertheless, trusted Linux Managed SNI accepted the revoked leaf with
ValidationErrors=None and ChainStatus=NoError in Mandatory and Strict modes.
These paths did not perform revocation rejection; the existing validation policy does
not enable that check. Untrusted-client failures establish a trust failure, not a
revocation failure.

There are 40 revoked-chain observation rows and 20 extra expired-pin observation rows.
They verify capture and absence of leakage, but must not be presented as evidence that
the driver detects revoked certificates or captures certificates on a Revoked verdict.
Obtaining that negative verdict requires a supported revocation-enforcing client path;
a valid CRL alone does not provide it. No product-policy change was made to force one.

## Remaining coverage boundaries

| Boundary | Exact blocker / resource needed |
|---|---|
| Windows trusted container profiles | 4 profiles x 2 ForceEncryption settings x 3 provider/TFM combinations = 24 unexecuted configurations. Host CA trust changes are prohibited. Needs an isolated Windows client that can trust the throwaway CA and reach this fixture. |
| WS2019 Strict/TLS 1.3 or Strict pinning | The SQL container is bound to host loopback only; the private VM has no route to it. Needs an authorized reachable TDS 8 endpoint/network path. No host exposure or tunnel was introduced. TLS 1.3 must not be inferred for WS2019. |
| Actual revoked-certificate failure | The tested driver paths do not enforce CRL revocation. Trusted Linux accepts the leaf despite an independently verified revocation. No Revoked-failure capture claim is made. |

The VM's temporary trust test establishes ordinary trusted validation success on
Managed net10, Native net10 and Native net462 using its independently fetched local
SQL leaf. It is not a substitute for the missing trusted container profile matrix or
for corporate-PKI coverage.

## Code and binary identity

- Main revision: `b3ed4d29d14fce448c5d14cc2695e865a56523ee`.
- Newer smoke revision: `e6797d32093c5e608b90b83310e10d14856a132d`.
- Native packages: `Microsoft.Data.SqlClient.SNI` and `.SNI.runtime`
  `7.1.0-preview3.26303.1`, restored from the existing local cache.
- Deployed DLLs for both frameworks matched the supplied signed binary identities:

```text
x64 FFC43EB7C389FDDEBE3B176AFB5C528FAA3155C0A31632D4C6AC0EAB1C4D725E
x86 2941FD986EE95BFB65ED530B230B55F452F90FD6E5ACF19C8D7ED936740A5937
```

These are native binary hashes, not a reintroduction of the removed certificate-hash API.
Native processes logged their loaded DLL paths. Managed runs checked that native SNI
was not loaded. The VM also checked both deployed x64 hashes before testing.

Product sources were built from separate Git archives, not overlaid onto the published
E2E branch. All 1,390 tracked `src` blobs in the b3 snapshot and 1,410 in the newer
snapshot matched their respective Git revisions after checkout CRLF normalization.
There were zero source mismatches. Only copied local harness/orchestration files changed.

`git range-diff` reports all 11 slice-D commits unchanged (`=`) across the rebase.
The b3 results therefore support the unchanged D patch set at e6797d320.
However, the entire product trees are not identical: their bases differ, including
other source updates. The 42-check newer-head smoke is the direct evidence for that
complete revision; the full matrix was not repeated on it.

## Exact environments and observed TLS

| Environment | Tested identity |
|---|---|
| Windows host | Windows build 10.0.26300; .NET 10.0.14; x64 and actual x86 processes |
| Legacy Windows harness | net462 target on the installed .NET Framework runtime |
| Host SQL 2025 | Port 1440; 17.0.1145.4 |
| Host SQL 2022 | Port 50904; 16.0.1210.3 |
| SQL container | Cached `mcr.microsoft.com/mssql/server:2025-latest`, image `2c80e548ef65`; actual SQL 2025 RC1 **17.0.925.4**, Ubuntu 22.04.5 |
| Linux client container | Cached .NET runtime 10.0 image `939555726cae`; actual .NET **10.0.13**, x64 |
| WS2019 VM | Windows Server 2019 Datacenter 10.0.17763; image version `17763.9378.261025`; .NET 10.0.14, x64 |
| VM-local SQL | SQL Server 2025 Express RTM **17.0.1000.7** |

The floating image tag is not treated as proof of the SQL build; the server banner is.
The Windows Server 2019 resource was explicitly authorized after the initial local-only
scope, used only for this run, and was not an existing shared SQL server.

| Area | TLS 1.2 connection rows | TLS 1.3 connection rows |
|---|---:|---:|
| Windows x64 container | 612 | 168 |
| Linux container | 280 | 208 |
| Windows x86 hosts | 60 | 0 |
| WS2019 | 32 | 0 |
| Newer-head host smoke | 40 | 0 |

Four WS2019 native failures did not publish a TLS protocol and are excluded from this
table. Windows Managed Strict selected TLS 1.2 in these runs; native Strict and Linux
Strict exercised TLS 1.3, including correct and wrong pins. The independent probes
observed container TLS 1.2 cipher `0xC02F`, TLS 1.3 cipher `0x1302`, and host/VM
TLS 1.2 cipher `0xC030`. No Azure SQL TLS claim is made.

## Cleanup and scope

No host SQL configuration, certificates or trust stores were modified. The default
host instance on 1433 was already running when this expansion began; it was excluded
and left alone. Its state differs from the October 30 report, not because this run
started it. Existing unrelated Docker containers were left untouched.

Task-owned SQL/client/CRL containers and the task Docker network were removed.
Temporary Linux trust existed only inside removed containers. The VM's temporary
trusted leaf was removed and its absence verified before VM deletion.

The temporary Azure resource group, VM, OS disk, NIC, storage account and policy-created
VM extensions were deleted. Cleanup verification checks resource-group absence and
absence of all seven inventoried resource IDs, and confirms that the reused shared
subnet still exists. No shared network resource was deleted.

## Local evidence and commands

Raw evidence is retained locally under `e2e\full-certificate-expansion-b3ed4d\`:

- `final-summary.tsv` and `final-rows.tsv`: authoritative, de-duplicated totals/rows.
- `win-*.tsv`, `linux-*.tsv`, `complete-*-revoked-*.tsv`: container results.
- `x86-*.tsv`, `newhead-*.tsv`, `ws2019-results\net*.tsv`: other runtime results.
- Matching `.log` files, probe `ciphers.tsv`/public certificates and SQL server logs.
- `source-identity.json`, `deployed-dlls-final.json`, `d-range-diff.txt`.
- `revocation-proof.log`, `ws2019-results\identity.txt`,
  `ws2019-results\trust-cleanup.txt`.
- `azure-cleanup-verified.json`, `docker-cleanup-verified.txt`.

Local orchestration commands (run from the repository root):

```powershell
$w = 'e2e\full-certificate-expansion-b3ed4d'
& "$w\run-containers.ps1"
& "$w\run-containers.ps1" -Profiles revoked -Forces 0,1 -Tag 'complete-'
```

Both completed; the second corrected the old revoked-profile early return and replaces
its ten initial result files in the totals. The runner refuses to overwrite existing
logs, uses an in-memory disposable SQL password, and removes its containers/network.
It never changes host trust.

The copied snapshot harness was built with `dotnet build`, Release,
`-p:TargetOs=Windows_NT` and exact
`-p:SniVersionRange=[7.1.0-preview3.26303.1]`; Linux was published for `net10.0` with
`-p:TargetOs=Unix`. Both builds succeeded. x86 tests used the x86 .NET executable and
x86 legacy output. VM tests used the saved local `ws2019-run.ps1`; setup, identity,
per-run exit codes and trust cleanup are preserved with the results.

The prior 329/0/52 cipher baseline and October 30 312/0/174 full-certificate matrix
have different scopes and scenario counts; subtracting these expansion totals from
either would not be a valid regression calculation. Existing native limitations
are distinguished above from new capture regressions, of which none was observed.
