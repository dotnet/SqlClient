# Preliminary OpenAsync BDN batch results

Preliminary instrumented measurements, not phase-compliance evidence or proof of blocked threads. Percentiles describe individual opens within each invocation, never pooled invocations or batch means. No outliers discarded; no regression gate or best-of-N reruns.

See **openasync-summary.md** and **openasync-summary.json** for per-invocation individual-open measurements. BDN means below are instrumented batch means.

| Benchmark | Parameters | Baseline batch ms | Current batch ms |
| --- | --- | ---: | ---: |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E1-Sync-1 | - | 4.7102 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E1-Async-1 | - | 5.3914 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E1-Sync-8 | - | 6.4203 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E1-Async-8 | - | 7.2830 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E1-Sync-32 | - | 8.7998 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E1-Async-32 | - | 9.2079 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E2-Sync-1 | - | 172.2683 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E2-Async-1 | - | 172.5572 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E2-Sync-8 | - | 340.5923 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E2-Async-8 | - | 364.0356 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E2-Sync-32 | - | 5687.5779 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E2-Async-32 | - | 1358.5284 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E3-Sync-1 | - | 185.9381 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E3-Async-1 | - | 187.7593 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E3-Sync-8 | - | 242.3697 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E3-Async-8 | - | 242.1799 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E3-Sync-32 | - | 473.6017 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E3-Async-32 | - | 508.3315 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E4-Sync-1 | - | 80.2979 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E4-Async-1 | - | 82.5615 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E4-Sync-8 | - | 131.9670 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E4-Async-8 | - | 129.1653 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E4-Sync-32 | - | 301.7194 |
| OpenAsyncScoreboardRunner | Case=OpenAsyncScoreboard-E4-Async-32 | - | 289.3995 |
