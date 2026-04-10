# Connection Pool Shutdown

Goal: Implement `Shutdown()` in `ChannelDbConnectionPool` so that pool group pruning and `AppDomain` unload correctly tear down pool v2 instances.

**Status:** Not started — basic shutdown deferred from the [connection-pool-transactions](../connection-pool-transactions/outline.md) PR, which will layer transaction-aware behavior (stasis, root survival) on top.

## Background

The design for `Shutdown()` was developed as part of the transaction support design:
- [Design document § Shutdown](../connection-pool-transactions/03-design/design.md#5-shutdown--implement) — implementation sketch, channel completion, idle drain
- [REQ-6.1](../connection-pool-transactions/02-requirements/requirements.md) — Root connection survival during shutdown
- [REQ-6.2](../connection-pool-transactions/02-requirements/requirements.md) — Post-transaction destroy on shutdown
- [REQ-6.4](../connection-pool-transactions/02-requirements/requirements.md) — Non-root connections destroyed on shutdown

## Phasing

### Phase 1: Basic Shutdown (this PR)
Non-transaction-aware shutdown — set state, complete channel, drain idle connections, destroy returned connections.

- [ ] Set `State = ShuttingDown`
- [ ] Complete the idle connection channel writer (`_idleConnectionWriter.TryComplete()`)
- [ ] Drain idle connections from channel and dispose them
- [ ] `ReturnInternalConnection` already routes to `RemoveConnection` when `ShuttingDown` — verify this works

### Phase 2: Transaction-Aware Shutdown (connection-pool-transactions PR)
Add stasis support so transaction roots survive shutdown until their transaction completes.

- [ ] Add stasis branches in `ReturnInternalConnection` (route `IsTransactionRoot` to stasis instead of destroy during shutdown)
- [ ] Add `IsTxRootWaitingForTxEnd` guard in `RemoveConnection`
- [ ] Verify `PutObjectFromTransactedPool` destroys connections when `State != Running` (already works)
- [ ] Add unit tests for root survival during shutdown
