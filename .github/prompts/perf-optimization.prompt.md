---
name: perf-optimization
description: Investigate and implement performance improvements in Microsoft.Data.SqlClient.
argument-hint: <area or issue to optimize, e.g. "TdsParser allocation reduction" or issue number>
agent: agent
tools: ['edit/createFile', 'edit/editFiles', 'read/readFile', 'codebase/search']
---

Investigate and optimize performance for: "${input:area}".

Performance is critical for a database driver — every allocation, copy, and synchronization on the hot path affects all consumers. Follow this structured approach:

## 1. Identify the Hotspot
- If a GitHub issue is provided, fetch the issue details and any profiling data.
- Determine the code area to optimize in `src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/`.
- Identify whether this is:
  - **Allocation reduction** — reducing GC pressure on hot paths
  - **CPU optimization** — reducing computation or branching
  - **I/O optimization** — reducing network round-trips or buffer copies
  - **Lock contention** — reducing synchronization overhead
  - **Caching** — reusing computed results or objects

## 2. Analyze Current Implementation
- Read the relevant source files to understand the current code path.
- Look for common performance anti-patterns:
  - `new byte[]` on hot paths → use `ArrayPool<T>.Shared`
  - Excessive `string` concatenation → use `StringBuilder` or `string.Create`
  - Repeated object creation → use `static readonly` cached instances
  - LINQ on hot paths → use manual loops
  - Boxing of value types → use generic methods
  - Unnecessary `async` state machines → use `ValueTask` or synchronous paths when result is available
  - Allocating `XmlWriterSettings`, `JsonSerializerOptions`, etc. per-call → cache as `static readonly`
  - `new MemoryCacheEntryOptions()` per-call → share or reuse instances
  - Reflection-based serialization → use source-generated serializers (`[JsonSerializable]`)

## 3. Plan the Optimization
Before making changes, document:
- **What** is being optimized (specific class/method)
- **Why** it matters (frequency of execution, measured or estimated impact)
- **How** it will be optimized (which pattern applied)
- **Risk** assessment (could this change behavior? thread safety implications?)

### Common Optimization Patterns in This Codebase

| Pattern | When to Use | Example |
|---------|-------------|---------|
| `ArrayPool<T>.Shared` | Temporary byte/char buffers | TDS packet buffers |
| `Span<T>` / `Memory<T>` | Avoiding array copies | Parsing TDS tokens |
| `static readonly` instances | Immutable config objects | `XmlWriterSettings`, `MemoryCacheEntryOptions` |
| `ValueTask<T>` | Async methods that often complete synchronously | Read operations with buffered data |
| Source-generated serializers | JSON serialization | `[JsonSerializable(typeof(T))]` context |
| `string.Create` | Building strings with known length | Connection string formatting |
| Object pooling | Frequently allocated/disposed objects | `StringBuilder` pool |
| Lazy initialization | Expensive objects used conditionally | SSL stream setup |

## 4. Implement the Optimization
- Make changes in `src/Microsoft.Data.SqlClient/src/`. Do NOT modify legacy directories.
- Ensure the optimization compiles for ALL target frameworks (`net462`, `net8.0`, `net9.0`).
- Use `#if NET` for APIs only available in .NET 8+ (e.g., `SearchValues<T>`, newer `Span` overloads).
- Preserve exact behavioral semantics — the optimization must be invisible to callers.
- Pay special attention to thread safety when caching or sharing instances.

## 5. Validate
- Verify all existing tests pass — performance changes must not alter behavior.
- Check for thread safety if shared state is introduced.
- If feasible, create a micro-benchmark to demonstrate the improvement.
- Review for regressions on other platforms (if using `#if` guards).

## 6. Write Tests (if needed)
- If the optimization changes internal structure, add unit tests to verify the new code path.
- For cache/pool implementations, add tests for concurrent access.
- For `ArrayPool` usage, verify buffers are returned (no leaks).

## 7. Prepare for PR
- Use commit message format: `Perf | <description> (#issue)`
- Summarize the optimization with before/after analysis (allocations, throughput, or timing).
- Provide a checklist:
  - [ ] No behavior changes (optimization is transparent)
  - [ ] Thread-safe if shared state introduced
  - [ ] Compiles for all target frameworks
  - [ ] All existing tests pass
  - [ ] Follows coding style (`policy/coding-style.md`)

