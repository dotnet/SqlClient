```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
Intel Xeon Platinum 8168 CPU 2.70GHz (Max: 2.69GHz), 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Job=MediumRun  EnvironmentVariables=COMPlus_gcServer=1  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=15  LaunchCount=1  
RunStrategy=Throughput  UnrollFactor=1  WarmupCount=1  

```
| Method                          | OpsPerInvocation | PoolDepth | Mean     | Error     | StdDev    | Completed Work Items | Lock Contentions | Allocated |
|-------------------------------- |----------------- |---------- |---------:|----------:|----------:|---------------------:|-----------------:|----------:|
| **RapidOpenCloseSingleThread**      | **1000**             | **1**         | **4.935 ms** | **0.1879 ms** | **0.1666 ms** |                    **-** |                **-** | **476.56 KB** |
| RapidOpenCloseSingleThreadAsync | 1000             | 1         | 5.833 ms | 0.9415 ms | 0.7862 ms |                    - |                - |  667.3 KB |
| **RapidOpenCloseSingleThread**      | **1000**             | **10**        | **6.668 ms** | **0.3831 ms** | **0.3584 ms** |                    **-** |                **-** |  **479.8 KB** |
| RapidOpenCloseSingleThreadAsync | 1000             | 10        | 6.906 ms | 0.1662 ms | 0.1388 ms |                    - |                - |  667.3 KB |
| **RapidOpenCloseSingleThread**      | **1000**             | **100**       | **6.298 ms** | **0.2005 ms** | **0.1778 ms** |                    **-** |                **-** |  **479.8 KB** |
| RapidOpenCloseSingleThreadAsync | 1000             | 100       | 4.369 ms | 0.4749 ms | 0.4210 ms |                    - |                - | 589.17 KB |
