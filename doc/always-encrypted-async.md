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
prevents the retry and releases the original asynchronous operation's state.
Synchronous execution continues to use synchronous provider methods.

Metadata parsing, session cache policy, replay counters, and the existing
single-retry policy are unchanged. Cryptography and serialization can still run
synchronously. This change does not make key-store operations or enclave-package
generation asynchronous; that work is tracked separately in
[#4585](https://github.com/dotnet/SqlClient/pull/4585), as part of
[#3672](https://github.com/dotnet/SqlClient/issues/3672).
