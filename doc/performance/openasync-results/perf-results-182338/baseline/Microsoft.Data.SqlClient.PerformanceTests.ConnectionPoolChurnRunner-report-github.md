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
| **RapidOpenCloseSingleThread**      | **1000**             | **1**         | **4.968 ms** | **0.2115 ms** | **0.1766 ms** |                    **-** |                **-** |    **625 KB** |
| RapidOpenCloseSingleThreadAsync | 1000             | 1         | 5.538 ms | 0.3247 ms | 0.2879 ms |                    - |                - | 815.73 KB |
| **RapidOpenCloseSingleThread**      | **1000**             | **10**        | **5.257 ms** | **0.1835 ms** | **0.1532 ms** |                    **-** |                **-** | **651.67 KB** |
| RapidOpenCloseSingleThreadAsync | 1000             | 10        | 5.677 ms | 0.1445 ms | 0.1207 ms |                    - |                - | 839.17 KB |
| **RapidOpenCloseSingleThread**      | **1000**             | **100**       | **6.476 ms** | **0.4173 ms** | **0.3903 ms** |                    **-** |                **-** | **636.05 KB** |
| RapidOpenCloseSingleThreadAsync | 1000             | 100       | 6.015 ms | 0.1125 ms | 0.0878 ms |                    - |                - | 823.55 KB |
