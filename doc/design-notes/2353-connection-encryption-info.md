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

The proposal brings the negotiated encryption mode, TLS version, a server-certificate
identifier, and structured validation results into one model that is available for both
successful and failed connection attempts. Tools such as SSMS can then show connection
security properties and build actionable failure messages from the driver's own
observations, without a second handshake.

## 2. Scenarios

| Scenario | Proposed capability | Value |
| --- | --- | --- |
| Show security information for an open connection | Read the negotiated encryption mode, TLS version, and presented server-certificate hash from `connection.EncryptionInfo`. | Display the actual negotiated values. |
| Explain a failed certificate validation | Read typed validation errors, applicable chain-status flags, and the certificate hash from `exception.EncryptionInfo`, when captured. | Build user-facing messages from structured results rather than localized exception text. |
| Identify the certificate involved in the attempt | Use a SHA-256 hash captured from that attempt's presented certificate. | Correlate the reported result with the certificate observed on that same connection attempt. |

## 3. Proposed API

Both `SqlConnection.EncryptionInfo` and `SqlException.EncryptionInfo` expose the same
`SqlConnectionEncryptionInfo` snapshot, which is read-only to the caller. The object is
always present; each field is `null` when the information is unavailable or not
applicable.

| Property on `SqlConnectionEncryptionInfo` | Type | Meaning |
| --- | --- | --- |
| `NegotiatedEncryptionMode` | `SqlConnectionEncryptionMode?` | Actual encryption outcome, not the requested `Encrypt` setting. |
| `NegotiatedEncryptionProtocol` | `SslProtocols?` | Negotiated TLS version, such as TLS 1.2 or TLS 1.3. |
| `PresentedServerCertificateHash256` | `ReadOnlyMemory<byte>?` | SHA-256 of the complete DER-encoded server-presented leaf certificate: 32 bytes when available. Not a public-key-only hash, and not the caller's configured certificate. |
| `ValidationErrors` | `SqlServerCertificateValidationErrors?` | Effective certificate-validation result under the connection's configured rules. |
| `ChainStatus` | `X509ChainStatusFlags?` | Applicable chain-validation flags, such as `UntrustedRoot`, `NotTimeValid`, or `Revoked`. |

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
        public ReadOnlyMemory<byte>? PresentedServerCertificateHash256 { get; }
        public SqlServerCertificateValidationErrors? ValidationErrors { get; }
        public X509ChainStatusFlags? ChainStatus { get; }
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

### Certificate hash

`PresentedServerCertificateHash256` is computed over the complete DER encoding of the
certificate the server presented. It identifies the certificate; validation results are
reported separately through `ValidationErrors` and `ChainStatus`. The snapshot keeps a
private copy of the bytes and exposes them as read-only memory.

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
certificate bytes, and this proposal does not change that comparison to use the
diagnostic hash. A missing or unreadable configured file is not a completed pin
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
or an early handshake failure leaves both `null`. Values are captured per physical login
attempt and reset for every routing, retry, and failover attempt.

- **Managed SNI** validates the server certificate inside the handshake, so a validation
  failure fails `EnableSsl`. In this slice Managed SNI does not capture the negotiated protocol
  on a failed handshake, so `TdsParser.EnableSsl` records no mode and both stay `null`. Slice C
  adds capture of `SslStream.SslProtocol` from the validation callback; after that, the failure
  path records the mode through `EncryptionInfoCapture.RecordModeIfProtocolObserved`.
- **Native SNI** (Schannel) defers server certificate verification: `EnableSsl` reports
  success with the negotiated protocol, and an untrusted or mismatched certificate only surfaces
  as an SSL-provider SNI error on the first post-handshake I/O (the LOGIN7 write). The mode and
  protocol from the completed handshake are kept. `TdsParser.ProcessSNIError`, the single place
  every SNI error becomes a `SqlError`, calls `EncryptionInfoCapture.DiscardUnobservedNegotiation`
  on SSL-provider errors during login, which drops the mode only when no protocol was observed.

These rules only touch mode and protocol; certificate fields are never affected. Once login
completes the capture is released, so SSL errors on an open connection never alter a published
snapshot.

## 5. Scope

The API reports negotiated encryption, TLS protocol, the server-presented certificate's
SHA-256 hash, and certificate-validation results for successful and failed connection
attempts.

Intended coverage includes the supported .NET Framework and .NET targets, Managed and
Native SNI, and TCP and Named Pipes where SqlClient uses TLS.

## 6. Implementation plan

The feature is delivered as a stack of pull requests:

| Slice | Content |
| --- | --- |
| **A** | Public API surface only. Both getters return an empty snapshot. |
| **B** | Capture the negotiated mode and TLS protocol, and attach the snapshot to `SqlException` when opening fails. |
| **C** | Capture certificate details (hash, validation errors, and chain status) in Managed SNI, on both successful and failed attempts. |
| **D** | Native SNI interop for the certificate details. |
