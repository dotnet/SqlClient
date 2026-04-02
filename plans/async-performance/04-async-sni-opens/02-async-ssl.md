# Fix 2: Async SSL Handshake

**Priority:** Medium
**Complexity:** Medium
**Risk:** Low-Medium

## Problem

After TCP connection, the SSL handshake via `SslStream.AuthenticateAsClientAsync()` is called but
awaited synchronously. This blocks the thread during TLS negotiation, which can take 50-200ms
(especially with certificate validation and OCSP checks).

## Location

**File:**
`src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/ManagedSni/SniTcpHandle.netcore.cs`

- SSL initialization code in the constructor or a helper method
- `SslStream.AuthenticateAsClientAsync()` call

## Changes Required

### 1. Async SSL Initialization

```csharp
private async ValueTask EnableSslAsync(
    string serverName,
    SslClientAuthenticationOptions options,
    CancellationToken cancellationToken)
{
    var sslStream = new SniSslStream(_stream, leaveInnerStreamOpen: false,
        _readAsyncSemaphore, _writeAsyncSemaphore);

    await sslStream.AuthenticateAsClientAsync(options, cancellationToken)
        .ConfigureAwait(false);

    _sslStream = sslStream;
    _stream = sslStream;
}
```

### 2. Integration with Async Connect Flow

This becomes part of the async connection pipeline from Fix 1:

```csharp
// In SniTcpHandle.CreateAsync():
var handle = new SniTcpHandle();
await handle.ConnectAsync(...).ConfigureAwait(false);
if (requireSsl)
{
    await handle.EnableSslAsync(serverName, sslOptions, cancellationToken)
        .ConfigureAwait(false);
}
return handle;
```

## Testing

- Integration test: TLS connection established asynchronously
- Test with various `Encrypt` modes: Optional, Mandatory, Strict
- Certificate validation callback works correctly in async path
- TLS 1.2 and 1.3 both work

## Risk

- Low-Medium — `AuthenticateAsClientAsync` is a well-tested .NET API. The risk is in correctly
  wiring it into the connection pipeline.
