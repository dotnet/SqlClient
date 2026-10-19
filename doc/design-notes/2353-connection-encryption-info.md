# Design note: #2353 — connection encryption information

| | |
|---|---|
| **Issue** | [#2353](https://github.com/dotnet/SqlClient/issues/2353) |
| **Status** | Public API approved; implementation in progress in slices (see [Implementation plan](#6-implementation-plan)) |
| **Primary consumer** | SQL Server Management Studio (SSMS); the API is general-purpose |
| **Public surface** | `SqlConnection.EncryptionInfo`, `SqlException.EncryptionInfo`, `SqlConnectionEncryptionInfo`, `SqlConnectionEncryptionMode`, `SqlServerCertificateValidationErrors` |

This note describes the approved API for exposing the encryption that a connection
actually negotiated and the outcome of server certificate validation. Its public API
shape was reviewed with the SSMS team. The implementation is being delivered in slices, as
listed in the implementation plan.

---

## 1. Motivation

Today an application can only see the encryption it *requested* (`Encrypt`,
`TrustServerCertificate`, `HostNameInCertificate`, `ServerCertificate`). It cannot see
what was actually negotiated, and when certificate validation fails the only information
available is the localized exception message.

The API brings the negotiated encryption mode, TLS version, the server-presented
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

## 3. Approved API

Both `SqlConnection.EncryptionInfo` and `SqlException.EncryptionInfo` expose the same
`SqlConnectionEncryptionInfo` snapshot, which is read-only to the caller. The object is
always present; each field is `null` when the information is unavailable or not
applicable.

| Property on `SqlConnectionEncryptionInfo` | Type | Meaning |
| --- | --- | --- |
| `ChainStatus` | `X509ChainStatusFlags?` | Applicable chain-validation flags, such as `UntrustedRoot`, `NotTimeValid`, or `Revoked`. |
| `NegotiatedCipherSuite` | `TlsCipherSuite?`; .NET only; not CLS-compliant | The same value as `NegotiatedCipherSuiteId`, typed as `System.Net.Security.TlsCipherSuite`. |
| `NegotiatedCipherSuiteId` | `int?` on every target framework; CLS-compliant | IANA TLS cipher suite identifier selected by the server, such as `0x1302` (`TLS_AES_256_GCM_SHA384`). |
| `NegotiatedEncryptionMode` | `SqlConnectionEncryptionMode?` | Actual encryption outcome, not the requested `Encrypt` setting. |
| `NegotiatedEncryptionProtocol` | `SslProtocols?` | Negotiated TLS version, such as TLS 1.2 or TLS 1.3. |
| `ValidationErrors` | `SqlServerCertificateValidationErrors?` | Effective certificate-validation result under the connection's configured rules. |

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
        public X509ChainStatusFlags? ChainStatus { get; }
#if NET
        [CLSCompliant(false)]
        public System.Net.Security.TlsCipherSuite? NegotiatedCipherSuite { get; }
#endif
        public int? NegotiatedCipherSuiteId { get; }
        public SqlConnectionEncryptionMode? NegotiatedEncryptionMode { get; }
        public SslProtocols? NegotiatedEncryptionProtocol { get; }
        public SqlServerCertificateValidationErrors? ValidationErrors { get; }

        // New instance on every call; the caller owns and disposes it.
        public X509Certificate2? GetServerCertificate();
    }

    public enum SqlConnectionEncryptionMode : byte
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

### How the encryption mode is negotiated

`NegotiatedEncryptionMode` reports both how encryption was negotiated and how much of the
connection it covers, so callers do not need to infer it from the TDS version:

| Value | How it is negotiated | Traffic covered by TLS |
| --- | --- | --- |
| `None` | TDS 7.x PRELOGIN; neither side requested encryption and no login TLS was set up. | None. |
| `LoginOnly` | TDS 7.x PRELOGIN `ENCRYPT_OFF` from both sides; TLS protects only the login. | The LOGIN7 packet only; later session traffic is plaintext. |
| `Full` | TDS 7.x PRELOGIN `ENCRYPT_ON`/`ENCRYPT_REQ`; TLS is set up after PRELOGIN. | Everything after PRELOGIN, including login and session traffic. PRELOGIN itself is plaintext. |
| `Strict` | TDS 8.0 (`Encrypt=Strict`); TLS is set up before any TDS packet. | All TDS traffic, including PRELOGIN. |

The requested `Encrypt` setting is not echoed back; the value reflects the outcome the
driver observed.

### Negotiated cipher suite

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
- **Unknown identifiers are preserved:** both properties return the captured identifier
  unchanged, without `Enum.IsDefined` filtering, so newly registered or GREASE values are
  not dropped.
- **Internal contract:** on .NET, the internal constructor takes `TlsCipherSuite?`, so the
  value stays strongly typed from `SslStream` to the public API; both properties are
  assigned from it. On .NET Framework, it takes an `int?` that must be in the range 0 to
  0xFFFF.
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
  Framework). Bytes that cannot be decoded are reported as `null`. The bytes are never
  written to `ToString()`, exception messages, traces, or
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
certificate bytes, and this API does not change that comparison. A missing or unreadable
configured file is not a completed pin comparison and is not reported as a mismatch.

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
the design-review rule that observed values are kept on certificate-validation failures. On a
TLS failure the mode is never reported without an observed protocol, so an early handshake
failure leaves both `null`, and so does a TDS 7.x PRELOGIN failure that happens before any mode is
known. When no TLS is negotiated (`Optional` against a server that replies `ENCRYPT_NOT_SUP`), the
mode `None` is recorded at PRELOGIN with a `null` protocol, so a later LOGIN7 failure (for example
error 18456) on that attempt reports mode `None` and a `null` protocol. With `Strict`, TLS completes before
PRELOGIN, so a later PRELOGIN or login failure on that attempt reports the observed `Strict` mode
and protocol. Values are captured per physical login attempt and reset for every routing, retry,
and failover attempt, and for the reconnect `TdsParser.Connect` makes when the server reports an
instance failure in its PRELOGIN response.

**Failure scope.** `SqlException.EncryptionInfo` is attached only to failures of physical
connection establishment. In practice that means exceptions thrown from the login sequence in
`SqlConnectionInternal.OpenLoginEnlist`, which includes PRELOGIN, TLS, LOGIN7, routing, and
failover. Some failures happen after login has completed, even when they surface from `Open` or
`OpenAsync`. Examples are automatic `System.Transactions` enlistment, both at the end of a
non-pooled login and while a pooled connection is activated (`Activate` → `Enlist` →
`GetDTCAddress`/propagation). Others are session-reset
errors on pooled checkout, explicit `EnlistTransaction` calls, and command execution. These are
not connection-establishment failures, so their exceptions keep `Empty`. For a pooled connection,
the negotiated values belong to an earlier, successful physical login and remain unchanged on that
physical connection. Attaching them to a transaction error would describe a different operation
than the one that failed. This also keeps implicit and explicit enlistment failures consistent.

- **Managed SNI** validates the server certificate inside the handshake, in the
  `RemoteCertificateValidationCallback`. `SslStream` agrees the protocol version before it
  raises the callback, so the callback records `SslStream.SslProtocol` with the certificate
  details. When validation fails `EnableSsl` fails; `TdsParser.EnableSsl` then records the mode
  through `EncryptionInfoCapture.RecordModeIfProtocolObserved` only if that protocol was
  captured. A protocol already determined for the attempt takes precedence over the one
  reported with the certificate details. Managed SNI is used only on .NET (`net10.0`).
- **Native SNI.** With the current SNI package, native SNI also rejects an untrusted server
  certificate during the handshake, so this slice reports neither mode nor protocol for that
  failure (the tests assert this for both SNI implementations). The rule that covers a deferred
  validation failure is conditional: if an SSL-provider SNI error occurs during login after a
  protocol was observed, `TdsParser.ProcessSNIError`, the single place every SNI error becomes a
  `SqlError`, calls `EncryptionInfoCapture.DiscardUnobservedNegotiation`, which keeps the observed
  mode and protocol and drops the mode only when no protocol was observed. Populated values are
  not promised for an untrusted-certificate failure.

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
| `PresentedServerCertificateHash256` | SHA-256 of the presented leaf certificate's DER encoding (`GetRawCertData()`) | No certificate was presented. Captured even when `TrustServerCertificate` bypasses validation. |
| `ServerCertificateValidationErrors` | Effective SqlClient verdict: `ServerCertificateMissing`, `ChainError`, `HostNameMismatch` (after `HostNameInCertificate`), `PinnedCertificateMismatch` (`ServerCertificate`); `None` when validation passed | Validation was bypassed, or the configured `ServerCertificate` file could not be read. |
| `ServerCertificateChainStatus` | Bitwise OR of the original callback chain's `ChainStatus` flags (no chain rebuild); `NoError` when the chain was evaluated cleanly | Validation was bypassed, `ServerCertificate` pinning is used (the chain isn't consulted), no certificate was presented, or the platform reported a chain error without flags. |

The host name check can throw before the final verdict is recorded. For example, Managed SNI
named pipes validate against the target `.`, which `X509Certificate2.MatchesHostname` rejects
with an `ArgumentException`. Errors established before that check, such as `ChainError` and the
callback chain status, are recorded first so they survive the exception. The exception itself is
unchanged. When no error was established before the check, the fields stay `null` rather than
reporting `None`.

Native SNI does not yet report certificate details; those fields remain `null` there.

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
| **D** | Native SNI interop for the certificate details and cipher suite. |
