# Fix 3: Optimize CancellationToken Handling

**Priority:** Low-Medium
**Complexity:** Low
**Risk:** Low

## Problem

Every async operation (`ReadAsync`, `ExecuteReaderAsync`, `OpenAsync`) registers a cancellation
callback via `CancellationToken.Register()`. This allocates a callback object per operation.
Issue #2408 shows measurable overhead when using `CancellationTokenSource`-derived tokens vs
`CancellationToken.None`.

Current pattern (found at multiple locations):

- `SqlDataReader.cs` line 4619
- `SqlCommand.NonQuery.cs` line 664
- `SqlCommand.Reader.cs` line 976
- `SqlConnection.cs` line 1976

```csharp
CancellationTokenRegistration registration = cancellationToken.Register(
    callback: SqlCommand.s_cancelIgnoreFailure,
    state: this);
```

## Location

**Files:** As listed above

## Changes Required

### 1. Skip Registration for CancellationToken.None

The most impactful and simplest change — `CancellationToken.None` can never be cancelled, so don't
register:

```csharp
CancellationTokenRegistration registration = default;
if (cancellationToken.CanBeCanceled)
{
    registration = cancellationToken.Register(
        SqlCommand.s_cancelIgnoreFailure, this);
}
```

Verify whether this is already done at each call site. If `CanBeCanceled` is checked, the fix for
`CancellationToken.None` is already in place.

### 2. Use UnsafeRegister Where Safe

`CancellationToken.UnsafeRegister()` (available in .NET 6+) avoids flowing `ExecutionContext`,
reducing allocation:

```csharp
#if NET
registration = cancellationToken.UnsafeRegister(
    static (state, ct) => ((SqlCommand)state!).CancelIgnoreFailure(),
    this);
#else
registration = cancellationToken.Register(
    SqlCommand.s_cancelIgnoreFailure, this);
#endif
```

This is safe here because the cancellation callback (`CancelIgnoreFailure`) doesn't need the calling
thread's security context.

### 3. Use Static Lambdas

Ensure cancellation callbacks use `static` lambdas to avoid closure allocations:

```csharp
// Avoid:
cancellationToken.Register(() => this.Cancel());

// Prefer:
cancellationToken.Register(static (obj) => ((SqlCommand)obj).Cancel(), this);
```

## Testing

- Benchmark: ReadAsync with CancellationTokenSource — measure allocation reduction
- Verify cancellation still works correctly after optimization
- .NET Framework path must continue using `Register()` (no `UnsafeRegister`)

## Risk

- Low — these are well-known allocation reduction patterns
- `UnsafeRegister` must only be used on .NET (not .NET Framework) and only when ExecutionContext
  flow is unnecessary
