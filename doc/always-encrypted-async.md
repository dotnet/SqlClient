# Asynchronous enclave attestation

Asynchronous `SqlCommand` execution awaits enclave session lookup, attestation
parameter preparation, session creation, and retry-time session invalidation.
This applies to `ExecuteReaderAsync`, `ExecuteNonQueryAsync`,
`ExecuteScalarAsync`, `ExecuteXmlReaderAsync`, and their shared APM and
`SqlBatch` execution paths.

The execution cancellation token is passed to enclave provider operations,
including attestation-service requests. Cancellation or provider failure during
attestation preparation stops execution before the user query is sent and releases
describe-encryption state. Cancellation or failure during retry-time invalidation
or subsequent attestation preparation releases the original asynchronous
operation's connection reservation before reporting completion.
Synchronous execution continues to use synchronous provider methods.

Applications must serialize operations on a non-MARS connection. Execution
revalidates the connection after provider I/O and before queued TDS setup, so
another active command or reader prevents sharing its physical session.

Metadata parsing, session cache policy, replay counters, and the existing
single-retry policy are unchanged. Cryptography and serialization can still run
synchronously. This change does not make key-store operations or enclave-package
generation asynchronous; that work is tracked separately in
[#4585](https://github.com/dotnet/SqlClient/pull/4585), as part of
[#3672](https://github.com/dotnet/SqlClient/issues/3672).
