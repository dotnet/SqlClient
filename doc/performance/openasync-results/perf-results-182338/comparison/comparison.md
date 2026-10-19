# OpenAsync controls — existing BDN workloads

Report-only M1/M2 controls: SqlConnection and ConnectionPoolChurn. Existing workload and BDN job settings are unchanged. One pass; no regression-triggered reruns.

| Benchmark | Method | Parameters | Baseline mean ms | Current mean ms |
| --- | --- | --- | ---: | ---: |
| ConnectionPoolChurnRunner | RapidOpenCloseSingleThread | OpsPerInvocation=1000&PoolDepth=10 | 5.2568 | 6.6682 |
| ConnectionPoolChurnRunner | RapidOpenCloseSingleThreadAsync | OpsPerInvocation=1000&PoolDepth=10 | 5.6766 | 6.9063 |
| SqlConnectionRunner | OpenAsyncConnection | MARS=False&Pooling=True | 0.0030 | 0.0034 |
| SqlConnectionRunner | OpenConnection | MARS=True&Pooling=True | 0.0033 | 0.0036 |
| ConnectionPoolChurnRunner | RapidOpenCloseSingleThreadAsync | OpsPerInvocation=1000&PoolDepth=1 | 5.5381 | 5.8329 |
| SqlConnectionRunner | OpenConnection | MARS=False&Pooling=False | 6.2646 | 6.3220 |
| SqlConnectionRunner | OpenConnection | MARS=True&Pooling=False | 6.2953 | 6.3436 |
| SqlConnectionRunner | OpenAsyncConnection | MARS=True&Pooling=True | 0.0034 | 0.0034 |
| SqlConnectionRunner | OpenConnection | MARS=False&Pooling=True | 0.0030 | 0.0030 |
| SqlConnectionRunner | OpenAsyncConnection | MARS=False&Pooling=False | 6.6520 | 6.6109 |
| ConnectionPoolChurnRunner | RapidOpenCloseSingleThread | OpsPerInvocation=1000&PoolDepth=1 | 4.9679 | 4.9348 |
| SqlConnectionRunner | OpenAsyncConnection | MARS=True&Pooling=False | 6.6827 | 6.6119 |
| ConnectionPoolChurnRunner | RapidOpenCloseSingleThread | OpsPerInvocation=1000&PoolDepth=100 | 6.4764 | 6.2981 |
| ConnectionPoolChurnRunner | RapidOpenCloseSingleThreadAsync | OpsPerInvocation=1000&PoolDepth=100 | 6.0151 | 4.3686 |
