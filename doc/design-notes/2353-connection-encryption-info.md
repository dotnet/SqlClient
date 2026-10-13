# Design note: #2353 — connection encryption information

| | |
|---|---|
| **Issue** | [#2353](https://github.com/dotnet/SqlClient/issues/2353) |
| **Status** | API proposal under review; delivered in slices (see [Implementation plan](#6-implementation-plan)) |
| **Primary consumer** | SQL Server Management Studio (SSMS); the API is general-purpose |
| **Public surface** | `SqlConnection.EncryptionInfo`, `SqlException.EncryptionInfo`, `SqlConnectionEncryptionInfo`, `SqlConnectionEncryptionMode`, `SqlServerCertificateValidationErrors` |

This note describes the proposed API for exposing the encryption that a connection
actually negotiated and the outcome of server certificate validation. It is a summary of
the proposal reviewed with the SSMS team and is intended to help reviewers of the
implementation pull requests.

---

## 1. Motivation

Today an application can only see the encryption it *requested* (`Encrypt`,
`TrustServerCertificate`, `HostNameInCertificate`, `ServerCertificate`). It cannot see
what was actually negotiated, and when certificate validation fails the only information
available is the localized exception message.

The proposal brings the negotiated encryption mode, TLS version, the server-presented
certificate, and structured validation results into one model that is available for both
successful and failed connection attempts. Tools such as SSMS can then show connection
security properties and build actionable failure messages from the driver's own
observations, without a second handshake.

## 2. Scenarios

| Scenario | Proposed capability | Value |
| --- | --- | --- |
| Show security information for an open connection | Read the negotiated encryption mode, TLS version, and presented server certificate from `connection.EncryptionInfo`. | Display the actual negotiated values. |
| Explain a failed certificate validation | Read typed validation errors, applicable chain-status flags, and the presented certificate from `exception.EncryptionInfo`, when captured. | Build user-facing messages from structured results rather than localized exception text. |
| Identify the certificate involved in the attempt | Call `GetServerCertificate()` to get the leaf certificate captured from that attempt. | Show subject, issuer, validity, SANs, and thumbprint of the certificate observed on that same connection attempt. |

## 3. Proposed API

Both `SqlConnection.EncryptionInfo` and `SqlException.EncryptionInfo` expose the same
`SqlConnectionEncryptionInfo` snapshot, which is read-only to the caller. The object is
always present; each field is `null` when the information is unavailable or not
applicable.

| Property on `SqlConnectionEncryptionInfo` | Type | Meaning |
| --- | --- | --- |
| `NegotiatedEncryptionMode` | `SqlConnectionEncryptionMode?` | Actual encryption outcome, not the requested `Encrypt` setting. |
| `NegotiatedEncryptionProtocol` | `SslProtocols?` | Negotiated TLS version, such as TLS 1.2 or TLS 1.3. |
| `NegotiatedCipherSuiteId` | `int?` on every target framework; CLS-compliant | IANA TLS cipher suite identifier selected by the server, such as `0x1302` (`TLS_AES_256_GCM_SHA384`). *Needs API review.* |
| `NegotiatedCipherSuite` | `TlsCipherSuite?`; .NET only; not CLS-compliant | The same value as `NegotiatedCipherSuiteId`, typed as `System.Net.Security.TlsCipherSuite`. *Needs API review.* |
| `ValidationErrors` | `SqlServerCertificateValidationErrors?` | Effective certificate-validation result under the connection's configured rules. |
| `ChainStatus` | `X509ChainStatusFlags?` | Applicable chain-validation flags, such as `UntrustedRoot`, `NotTimeValid`, or `Revoked`. |

| Method on `SqlConnectionEncryptionInfo` | Return type | Meaning |
| --- | --- | --- |
| `GetServerCertificate()` | `X509Certificate2?` | The leaf certificate the server presented, as a new instance on every call, or `null` when none was captured. Not the caller's configured certificate. |

```csharp
using System;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.Data.SqlClient
{
    public sealed partial class SqlConnection
    {
        // Snapshot captured for the current open connection. Always non-null; empty
        // before opening, while opening, and after closing or disposing.
        public SqlConnectionEncryptionInfo EncryptionInfo { get; }
    }

    public sealed partial class SqlException
    {
        // Snapshot captured for the connection attempt that produced this exception.
        // Always non-null. Its presence alone does not indicate a certificate failure.
        public SqlConnectionEncryptionInfo EncryptionInfo { get; }
    }

    public sealed class SqlConnectionEncryptionInfo
    {
        public SqlConnectionEncryptionMode? NegotiatedEncryptionMode { get; }
        public SslProtocols? NegotiatedEncryptionProtocol { get; }
        public int? NegotiatedCipherSuiteId { get; }
#if NET
        [CLSCompliant(false)]
        public System.Net.Security.TlsCipherSuite? NegotiatedCipherSuite { get; }
#endif
        public SqlServerCertificateValidationErrors? ValidationErrors { get; }
        public X509ChainStatusFlags? ChainStatus { get; }

        // New instance on every call; the caller owns and disposes it.
        public X509Certificate2? GetServerCertificate();
    }

    public enum SqlConnectionEncryptionMode
    {
        None = 0,      // Unencrypted connection.
        LoginOnly = 1, // Login is protected; subsequent session traffic is unencrypted.
        Full = 2,      // Full-session encryption.
        Strict = 3     // TDS 8.0 strict encryption.
    }

    [Flags]
    public enum SqlServerCertificateValidationErrors
    {
        None = 0,
        ServerCertificateMissing = 1,
        HostNameMismatch = 2,
        ChainError = 4,
        PinnedCertificateMismatch = 8
    }
}
```

Applications cannot construct `SqlConnectionEncryptionInfo`; the driver creates the
snapshots.

### Negotiated cipher suite

> **Needs API review.** This property is a proposal and has not yet been reviewed by the team.

`NegotiatedCipherSuiteId` is the 16-bit identifier from the IANA TLS Cipher Suites
registry for the suite the server selected in its ServerHello, for example `0x1302` for
`TLS_AES_256_GCM_SHA384`. It is not a Windows `ALG_ID`.

- **One shared member, plus a strong type on .NET:** `NegotiatedCipherSuiteId` is `int?`
  on every target framework. On .NET only, `NegotiatedCipherSuite` exposes the same value
  as `System.Net.Security.TlsCipherSuite?`, the type `SslStream.NegotiatedCipherSuite`
  uses. .NET Framework and .NET Standard 2.0 have no `NegotiatedCipherSuite` member,
  because `TlsCipherSuite` does not exist there.
- **Why not a per-target type for one property name:** the package ships a
  `netstandard2.0` reference assembly. If `NegotiatedCipherSuite` were `ushort?` there and
  `TlsCipherSuite?` on .NET, a library compiled against .NET Standard would bind to
  `Nullable<ushort> get_NegotiatedCipherSuite()`, which does not exist in the .NET
  implementation, and would fail at run time with `MissingMethodException`. With this
  shape, the only member present on every target has an identical signature everywhere.
- **CLS compliance:** `NegotiatedCipherSuiteId` is CLS-compliant and carries no
  attribute. `TlsCipherSuite` is backed by `ushort`, so `NegotiatedCipherSuite` is marked
  `[CLSCompliant(false)]`, as `SslStream.NegotiatedCipherSuite` is. The assembly stays
  `[CLSCompliant(true)]`.
- **Unknown identifiers are preserved:** `NegotiatedCipherSuiteId` returns the captured
  identifier unchanged, and `NegotiatedCipherSuite` casts it without `Enum.IsDefined`
  filtering, so newly registered or GREASE values are not dropped.
- **Internal contract:** the internal constructor and capture paths use `int?` (0 to
  0xFFFF) on every target, so the network-layer plumbing does not depend on the target
  framework.
- **`null` means unavailable:** no TLS, the value was not captured on the current code
  path, the network layer (for example, an older native SNI) does not report it, or the
  attempt failed before the value was known. It does not mean the server selected no
  suite.
- **Failures keep observed values:** a ServerHello can select a suite and the handshake
  can still fail afterward, for example during certificate validation. As with
  `NegotiatedEncryptionProtocol`, a value that was observed is kept in the snapshot
  attached to `SqlException`.
- **Out of scope:** the client's offered suite list, CAPI2-level chain detail, and
  key-exchange group or post-quantum information.

### Server certificate

Following the threat model review, the snapshot exposes the full certificate the server
presented instead of a hash of it. A hash only lets a caller correlate against a
certificate it already has; the certificate itself lets tools show subject, issuer,
validity, subject alternative names, and any thumbprint the caller wants to compute.

- **Leaf only, public data only:** the certificate is what the server sent in the TLS
  handshake. The chain is not included, and the certificate never has a private key.
- **Captured on success and on failure:** the certificate is captured whenever the server
  presents one, including with `TrustServerCertificate=true` and when validation fails
  (read it from `SqlException.EncryptionInfo`).
- **Diagnostics only:** returning a certificate is not a trust decision. Validation results
  are reported separately through `ValidationErrors` and `ChainStatus`, and callers must
  not use the certificate to override or bypass the driver's validation.
- **Storage:** the snapshot keeps a private copy of the DER bytes. Each call parses them
  with `X509CertificateLoader.LoadCertificate` (from `Microsoft.Bcl.Cryptography` on .NET
  Framework). Bytes that cannot be parsed are reported as `null`; the method does not
  throw. The bytes are never written to `ToString()`, exception messages, traces, or
  serialization.
- **Capture limit:** certificates larger than 64 KiB (DER) are not captured; the method
  returns `null` and the connection is unaffected.

#### Why a method rather than a property

- **Ownership:** `X509Certificate2` is disposable. Returning a new instance on each call
  makes the caller the clear owner. A shared instance would leave it unclear who disposes
  it, and disposing it would break other readers of the same snapshot.
- **Immutability:** on .NET Framework, `X509Certificate2` is mutable (`Import`, `Reset`).
  A shared instance would let one caller change what others see; a fresh instance per
  call keeps the snapshot immutable.
- **Cost:** each call parses the certificate. .NET design guidelines advise against
  hiding that kind of work, or returning a new object each time, behind a property getter.

### Certificate-validation outcomes

`SqlServerCertificateValidationErrors` is a flags enum; applicable errors can coexist.

| Value | Meaning |
| --- | --- |
| `None = 0` | Applicable validation ran and reported no error. |
| `ServerCertificateMissing = 1` | An authoritative validation result indicates that the server did not present its certificate. Not a missing local certificate file. |
| `HostNameMismatch = 2` | The certificate did not match the expected hostname after applying configured hostname rules. |
| `ChainError = 4` | Chain validation reported an error; `ChainStatus` supplies the captured detail. |
| `PinnedCertificateMismatch = 8` | The presented certificate did not match the certificate configured through `ServerCertificate`. |

A pin mismatch is distinct from a chain error: the existing pin check compares
certificate bytes, and this proposal does not change that comparison. A missing or unreadable configured file is not a completed pin
comparison and is not reported as a mismatch.

## 4. Behavior

- **Success and failure:** after `Open()` or `OpenAsync()` succeeds, read
  `connection.EncryptionInfo`. If opening throws `SqlException`, read
  `exception.EncryptionInfo`. Its presence alone does not identify a certificate
  failure.
- **Unknown is not success:** `null` means unavailable, not applicable, or skipped.
  Zero-valued `None` / `NoError` means the applicable validation ran without reporting
  errors. If `TrustServerCertificate` bypasses validation, the skipped validation fields
  remain `null`; a successful connection does not prove that certificate validation
  occurred.
- **Only applicable results:** the driver reports its effective verdict, not raw errors
  that configured hostname or pin rules resolved. For a pin-only validation path,
  `ChainStatus` remains `null` rather than reporting an invented chain failure. Absence
  of `Revoked` does not establish that revocation was checked.
- **Lifetime:** the connection snapshot is empty before opening, while opening, and after
  close or dispose. Previously retained snapshots and exception snapshots don't change.
  Pooled connections report the underlying physical connection's saved information;
  retry and redirect results refer only to the terminal attempt.
- **No additional validation:** reading these properties performs no handshake or
  certificate check and doesn't change trust decisions.
- **No message changes:** snapshot values are not added to exception messages,
  `ToString()`, or default telemetry. Applications control their own display and
  logging.
- **Serialization:** the snapshot is not serialized with `SqlException`; a deserialized
  exception returns an empty snapshot.
- **Diagnostics only:** the snapshot must not be used to make security decisions; the
  driver's own validation is authoritative.

### Negotiated mode mapping

`NegotiatedEncryptionMode` reflects the final state of the physical connection after
login, derived from the client `Encrypt` setting and the server's PRELOGIN response:

| Client `Encrypt` | Server PRELOGIN `ENCRYPTION` | Reported mode | Protocol |
| --- | --- | --- | --- |
| `Strict` (TDS 8.0) | — (TLS before PRELOGIN) | `Strict` | Negotiated TLS version |
| `Mandatory` / `true` | `ENCRYPT_OFF`, `ENCRYPT_ON`, `ENCRYPT_REQ` | `Full` | Negotiated TLS version |
| `Optional` / `false` | `ENCRYPT_ON` / `ENCRYPT_REQ` | `Full` | Negotiated TLS version |
| `Optional` / `false` | `ENCRYPT_OFF` (TLS removed after LOGIN7) | `LoginOnly` | Negotiated TLS version |
| `Optional` / `false` | `ENCRYPT_NOT_SUP` | `None` | `null` |

**Failure path.** Only values actually observed for the attempt are published; the requested
settings and the server's PRELOGIN response are never used as substitutes. On success the mode
is recorded once the TLS handshake (if any) completes. When TLS fails after a protocol was
observed, the attempt reports that protocol and the mode that was being negotiated, matching
the design-review rule that observed values are kept on certificate-validation failures. The
mode is never reported on a failed attempt without an observed protocol, so a PRELOGIN failure
or an early handshake failure leaves both `null` (Native SNI may still report the protocol it attempted; see below). Values are captured per physical login
attempt and reset for every routing, retry, and failover attempt.

- **Managed SNI** validates the server certificate inside the handshake, in the
  `RemoteCertificateValidationCallback`. `SslStream` agrees the protocol version before it
  raises the callback, so the callback records `SslStream.SslProtocol` with the certificate
  details. When validation fails `EnableSsl` fails; `TdsParser.EnableSsl` then records the mode
  through `EncryptionInfoCapture.RecordModeIfProtocolObserved` only if that protocol was
  captured. A protocol already determined for the attempt takes precedence over the one
  reported with the certificate details. Managed SNI is used only on .NET (`net10.0`).
- **Native SNI** (Schannel) defers server certificate verification: `EnableSsl` reports
  success with the negotiated protocol, and an untrusted or mismatched certificate only surfaces
  as an SSL-provider SNI error on the first post-handshake I/O (the LOGIN7 write). The mode and
  protocol from the completed handshake are kept. `TdsParser.ProcessSNIError`, the single place
  every SNI error becomes a `SqlError`, calls `EncryptionInfoCapture.DiscardUnobservedNegotiation`
  on SSL-provider errors during login, which drops the mode only when no protocol was observed.

These rules only touch mode and protocol; certificate fields are never affected. Once login
completes the capture is released, so SSL errors on an open connection never alter a published
snapshot.

**Cipher suite.** `EncryptionInfoCapture.RecordCipherSuite(int cipherSuite)` is the single
recording point for `NegotiatedCipherSuiteId` and, on .NET, `NegotiatedCipherSuite`. The capture stores the IANA identifier as an integer on
every target framework and passes it to the snapshot constructor, so `NegotiatedCipherSuiteId`
(every target) and the .NET-only `NegotiatedCipherSuite` (`TlsCipherSuite?`) read the same value
(Slice C records `(int)SslStream.NegotiatedCipherSuite`). Slice B adds only the plumbing: the value is reset
with the other fields at the start of every physical login attempt, flows into the snapshot
published on `SqlConnection` and attached to `SqlException`, and values outside 0–0xFFFF are
ignored instead of failing the login. No transport calls it in this slice, so the property stays
`null`; Slice C records it from `SslStream.NegotiatedCipherSuite` (Managed SNI) and Slice D from
native SNI, which needs a new SNI contract. On failure an observed cipher suite is kept, like the
protocol; `DiscardUnobservedNegotiation` neither clears nor infers it. A cipher suite is only
reported once a ServerHello selected it, which is itself evidence of the observed negotiation, so
it is not dropped when the protocol is unavailable.

### Managed SNI certificate capture

Managed SNI records certificate details from the `RemoteCertificateValidationCallback`
that runs during the TLS handshake. Capture reuses the callback's inputs and SqlClient's
existing verdict; it performs no extra validation or network work and doesn't change
trust decisions, messages, or tracing. Values are reset at the start of every TLS
handshake and recorded whether the handshake succeeds or fails. The callback also records the
TLS protocol agreed for the handshake (see **Failure path** above).

| Field | Source | `null` when |
| --- | --- | --- |
| `PresentedServerCertificateRawData` / `GetServerCertificate()` | A copy of the presented leaf certificate's DER encoding (`GetRawCertData()`); the first certificate presented in the attempt is kept | No certificate was presented, or its encoding exceeds 64 KiB (`ServerCertificateCapture.MaxRawCertificateSize`). Captured even when `TrustServerCertificate` bypasses validation, and on validation failure. |
| `ServerCertificateValidationErrors` | Effective SqlClient verdict: `ServerCertificateMissing`, `ChainError`, `HostNameMismatch` (after `HostNameInCertificate`), `PinnedCertificateMismatch` (`ServerCertificate`); `None` when validation passed | Validation was bypassed, or the configured `ServerCertificate` file could not be read. |
| `ServerCertificateChainStatus` | Bitwise OR of the original callback chain's `ChainStatus` flags (no chain rebuild); `NoError` when the chain was evaluated cleanly | Validation was bypassed, `ServerCertificate` pinning is used (the chain isn't consulted), no certificate was presented, or the platform reported a chain error without flags. |

The host name check can throw before the final verdict is recorded. For example, Managed SNI
named pipes validate against the target `.`, which `X509Certificate2.MatchesHostname` rejects
with an `ArgumentException`. Errors established before that check, such as `ChainError` and the
callback chain status, are recorded first so they survive the exception. The exception itself is
unchanged. When no error was established before the check, the fields stay `null` rather than
reporting `None`.

### Native SNI certificate capture

Native SNI (Windows) records the same details through two `sni.dll` exports,
`SNIGetCertValidationInfoWrapper` and `SNIGetServerCertificateHashWrapper`, that return
values SNI saved during the client TLS handshake. SqlClient queries them after the
handshake completes and again when it fails (before the handle closes). Querying performs
no validation or network work, never changes `SNIGetLastError`, and never throws: any
error leaves the fields `null` without changing the connection outcome or the thrown
exception.

`SNI_CERT_VALIDATION_INFO` is a 64-byte, blittable structure of `DWORD` fields. A field
is used only when its `dwAvailable` flag is set; structures with `cbSize < 64` or
`dwVersion < 1` are ignored, and trailing data from newer versions is ignored.

| Field | Native source (`dwAvailable` flag) | Mapping |
| --- | --- | --- |
| `PresentedServerCertificateHash256` | `rgbHash` (`HASH`, `cbHash == 32`) | 32 bytes copied |
| `ServerCertificateValidationErrors` | `dwValidationErrors` (`VALIDATION_ERRORS`) | Same bit values; unknown bits preserved |
| `ServerCertificateChainStatus` | `dwChainStatus` (`CHAIN_STATUS`) | Raw `CERT_TRUST_*` error status equals `X509ChainStatusFlags`; unknown bits preserved |
| `NegotiatedProtocol` | `dwProtocol` (`PROTOCOL`) | `SP_PROT_*` client bits mapped as for the handshake result; used only when the protocol was not already recorded (for example, after a failed handshake) |

**Older `sni.dll`:** a missing export is detected once (`EntryPointNotFoundException`) and
cached; the certificate fields then remain `null`. If only the hash export exists, the
hash is still captured.

**Known gaps:**

- In Schannel automatic validation (`Encrypt` without `HostNameInCertificate`), native SNI
  doesn't report validation errors or chain status on failure. Those fields remain `null`;
  they are never derived from HRESULTs or messages. The certificate hash is also
  unavailable on failure in this mode: Schannel invalidates the security context when its
  automatic validation rejects the certificate, so SNI can't query the presented
  certificate.
- In chain-policy validation (`HostNameInCertificate` set), a certificate that both fails
  the chain and doesn't match the host name reports only `ChainError`, while Managed SNI
  reports `HostNameMismatch | ChainError`. `CertVerifyCertificateChainPolicy` with
  `CERT_CHAIN_POLICY_SSL` returns a single error, and chain errors take precedence.

These exports are introduced by an upcoming native SNI package; until it ships, Native SNI
reports no certificate details.

**Cipher suite (Managed SNI).** Managed SNI reads `(int)SslStream.NegotiatedCipherSuite` at the
same two points as the protocol, on both the TCP and named-pipe handles (MARS passes through to
the physical handle):

- **After the handshake completes,** `TdsParser.EnableSsl` records the value from the session
  handle through `RecordCipherSuite`. This read is authoritative for a successful attempt.
- **Inside the validation callback,** the value is stored with the certificate capture.
  `SslStream` processes the ServerHello, which selects the suite, before it raises the callback,
  so the value is real there. The tests confirm it matches the suite a trusted handshake with the
  same server negotiates. It is adopted only when no suite was recorded for the attempt, so a
  certificate-validation failure still reports the observed suite.

Both reads are wrapped in `try`/`catch` so that capture never affects the handshake. A suite of
`0` (`TLS_NULL_WITH_NULL_NULL`, nothing agreed yet) is treated as unavailable. Without TLS, the
properties are `null`. The value flows through the SNI handles, `ServerCertificateCapture` and
`EncryptionInfoCapture` as an `int`. `NegotiatedCipherSuiteId` returns it unchanged, and on .NET
`NegotiatedCipherSuite` casts the same value to `TlsCipherSuite`.
Managed SNI runs only on `net10.0`; on `net462` this capture is not compiled
and `NegotiatedCipherSuiteId` stays `null` until native SNI support (Slice D).

## 5. Scope

The API reports negotiated encryption, TLS protocol, the server-selected cipher suite, the
server-presented leaf certificate, and certificate-validation results for successful and failed connection
attempts.

Intended coverage includes the supported .NET Framework and .NET targets, Managed and
Native SNI, and TCP and Named Pipes where SqlClient uses TLS.

## 6. Implementation plan

The feature is delivered as a stack of pull requests:

| Slice | Content |
| --- | --- |
| **A** | Public API surface only. Both getters return an empty snapshot. |
| **B** | Capture the negotiated mode, TLS protocol and cipher suite, and attach the snapshot to `SqlException` when opening fails. |
| **C** | Capture certificate details (presented certificate, validation errors, and chain status) in Managed SNI, on both successful and failed attempts. |
| **D** | Native SNI interop for the certificate details and cipher suite, degrading gracefully with older `sni.dll` versions. |
