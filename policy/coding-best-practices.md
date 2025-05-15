# Coding Best Practices

This document describes some typical programming pitfalls and best practices
related to C# and .NET.  It will grow and change as we encounter new situations
and the codebase evolves.

## Correctness & Business Logic

- Validate if the code adheres to what it is supposed to do. Compare the
implementation with the specification and ask questions, to get clarity.

## Memory / Resource Management

- C# – Rent large buffers from ArrayPool&lt;T&gt; and return them promptly to cut down
  on heap allocations and improve throughput. Have the buffers been returned to
  the pool?
- Prefer Span&lt;T&gt; for stack allocated buffers.
- Double check the depth of the call stack if using async/await. Stack depth of
  unwinding async/await state-machines of more than 3 calls can cause
  performance issues.
- Resource cleanup Dispose(), Close(), or the equivalent to release files,
  sockets, database connections, and other external resources.

## Error Handling & Reliability  

- Prefer return values to throwing exceptions.  Returns are strongly typed and
  checked by the compiler.
- Document all exceptions that may be thrown from an API.
- Do not let undocumented exceptions escape from an API.
- Be permissive in what you accept, and strict it what you produce.

## Async, Concurrency & Thread Safety (if applicable)

- Avoid calling Task.Result, Task.Wait() or Task.GetAwaiter().GetResult(). If
  needed, exercise extreme caution. These can cause deadlocks.
- When awaiting a task with the await keyword, always use
  Task.ConfigureAwait(false) so that the task continuation may run on any thread
  rather than marshalling back to the original calling thread.

## Backward Compatibility

- Someone will always depend on your undocumented behaviour.  It is just as much
  an API promise as explicitly documented behaviour.
- Use language features to deprecate/obsolete APIs, and to issue warnings for
  notable upcoming breaking changes.

## Security Considerations

- Never log passwords, secrets, or connection strings with credentials.
- Validate inputs to avoid SQL injection, even on metadata calls.
- Are there any user inputs going into SQL or shell commands directly?
- Are secrets being logged or exposed in stack traces?
- Are TLS/certificate settings handled safely and explicitly?
- Are we sending unbounded data streams to server prior to authentication e.g.
  in feature extensions?

## Performance & Scalability  

- For major features or large PRs, always run the internal performance
  benchmarks or performance testsuite to determine if the new changes are
  causing any performance degradation.

## Observability (Logging / Tracing / Metrics)  

- Error messages should contain enough context to understand the state of the
  program and find the source code responsible for emitting it.
- Instrument changes for metrics/tracing/logging to aid future field diagnosis,
  debugging, and app behaviour analysis.

## Unit Tests / Integration

- Have you added unit tests and integration tests?
- Unit tests should not depend on external resources.  Code under unit test
  should permit mocked resources to be provided/injected.
- Avoid tests that rely on timing.

## Configuration & Feature Flags  

- Is the change considered a breaking change? If breaking change is not
  avoidable, has a Configuration/Feature flag been introduced for customers to
  revert to old behavior?

## Code Coverage expectations

- Ideally these expectations  should be codified into scanning tool
  configuration that lives in the repo.
- Specific metrics will be chosen based on realistic analysis of existing
  coverage and will be re-evaluated as testing matures.

## Pipeline runs

- Is the CI passing? If not, then have you looked at the failures, and found the
  cause for failures?
