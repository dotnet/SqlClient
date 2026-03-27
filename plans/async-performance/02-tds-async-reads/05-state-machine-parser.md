# Fix 5: State Machine TDS Parser (Long-Term)

**Priority:** High impact but very long-term
**Complexity:** Very High
**Risk:** High

## Problem

The fundamental issue is that the TDS parser is written as a deeply nested synchronous call chain:

```text
TryRun()
  → TryProcessToken()
    → TryReadSqlValue()
      → TryReadPlpBytes()
        → TryPrepareBuffer()
          → TryReadNetworkPacket()
            → [NEEDS MORE DATA — can't yield here in sync code]
```

In sync mode, `TryReadNetworkPacket()` simply blocks until data arrives. In async mode, it can't
yield the entire call stack, so it takes a snapshot of the parser state, unwinds the entire stack,
and replays everything from the top when data arrives.

The correct fix is to restructure the parser as a **resumable state machine** that can yield at any
point where data is needed and resume from exactly that point — not from the start.

## Location

**Files:**

- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/TdsParser.cs` (~13,000+ lines)
- `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/TdsParserStateObject.cs` (~5,200+
  lines)

## Approach

### Option A: Compiler-Generated State Machine (async/await)

Make `TryRun()` and all its callees genuinely `async`:

```csharp
// Current:
internal TdsOperationStatus TryRun(RunBehavior run, ..., out bool dataReady)
{
    // 700+ lines of synchronous token processing
    // Returns TdsOperationStatus.NeedMoreData when stuck
}

// Proposed:
internal async ValueTask<bool> RunAsync(RunBehavior run, ...)
{
    // Same token processing logic
    // But instead of returning NeedMoreData:
    await stateObj.ReadNetworkPacketAsync();
    // Compiler generates the state machine for us
}
```

**Pros:**

- C# compiler handles state machine generation
- Incremental — can convert one `TryRead*` method at a time
- Well-understood pattern

**Cons:**

- `TryRun()` calls dozens of `TryRead*` methods — all must become async
- Sync callers need a sync wrapper (or separate sync path)
- Deep async call chains have overhead (state machine allocation per frame)
- The `TryRun()` method is ~700 lines with complex control flow

### Option B: Manual State Machine with Enum States

Restructure the token loop as an explicit state machine:

```csharp
enum ParserState
{
    ReadTokenType,
    ReadTokenLength,
    ProcessToken_Row_ReadColumnCount,
    ProcessToken_Row_ReadColumnValue,
    // ... many states
}

internal TdsOperationStatus RunStep(ref ParserState state, ...)
{
    switch (state)
    {
        case ParserState.ReadTokenType:
            if (!TryReadByte(out byte token))
                return NeedMoreData;
            state = ParserState.ReadTokenLength;
            goto case ParserState.ReadTokenLength;
        // ...
    }
}
```

**Pros:**

- No allocation overhead (state is a value type)
- Sync and async use the same code
- Maximally efficient — no replay at all

**Cons:**

- Enormous manual effort (hundreds of states)
- Very hard to maintain — any protocol change requires state machine updates
- Error-prone — manual state management is a classic bug source

### Option C: Incremental — Pipelines-Based Buffer Management

Use `System.IO.Pipelines` to manage the incoming data buffer:

```csharp
// Replace snapshot chain with a PipeReader
PipeReader _pipeReader;

// The parser reads from the pipe — if data isn't available, it awaits
ReadResult result = await _pipeReader.ReadAsync();
ReadOnlySequence<byte> buffer = result.Buffer;
// Parse from buffer — advance position — no replay needed
_pipeReader.AdvanceTo(consumed, examined);
```

**Pros:**

- `PipeReader` naturally supports "read what's available, wait for more"
- No snapshot/replay — the buffer is managed by the pipe
- Buffer lifecycle handled by pipe (pooled, ref-counted)
- Incremental — can introduce pipe at the SNI boundary first

**Cons:**

- Requires adapting SPIs to produce into a pipe
- All parsing code must work with `ReadOnlySequence<byte>` instead of `byte[]`
- Significant refactor of buffer management

### Recommended Approach

**Option C (Pipelines)** for the SNI → TdsParser boundary, combined with
**Option A (async/await)** for the parser methods. This gives:

- Pipe handles buffering (no snapshot chain)
- async/await handles yielding (no replay)
- Incremental migration path

## Incremental Migration Plan

### Phase 1: Introduce PipeReader at SNI Boundary

- Replace `_inBuff` with a `Pipe`
- SNI writes to `PipeWriter`, parser reads from `PipeReader`
- Keep `TryRead*` pattern but read from pipe instead of `_inBuff`

### Phase 2: Convert Leaf Methods to Async

- Start with `TryReadByte`, `TryReadInt16`, etc.
- These become `async ValueTask` with `PipeReader.ReadAsync()`
- Sync callers use `.GetAwaiter().GetResult()` (safe because pipe has data)

### Phase 3: Convert Token Processing to Async

- `TryProcessToken_*` methods become async
- Continue points become unnecessary (async/await handles suspension)

### Phase 4: Remove Snapshot Mechanism

- Once all paths are async-native, remove `StateSnapshot` entirely
- Remove `SnapshotStatus`, `_snapshot`, `_cachedSnapshot`

## Testing

- Each phase must pass all existing tests before proceeding
- Benchmark at each phase to verify performance improvement
- Protocol compliance testing against multiple SQL Server versions

## Risk

- High — this touches the core protocol implementation
- Must maintain backward compatibility with existing sync code paths
- Any regression could corrupt data or cause connection failures
- Should be behind an AppContext switch during development
