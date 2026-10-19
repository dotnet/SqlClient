```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
Intel Xeon Platinum 8168 CPU 2.70GHz (Max: 2.69GHz), 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Job=MediumRun  OutlierMode=DontRemove  EnvironmentVariables=COMPlus_gcServer=1  
Toolchain=InProcessEmitToolchain  InvocationCount=1  IterationCount=3  
LaunchCount=1  RunStrategy=Monitoring  UnrollFactor=1  
WarmupCount=2  

```
| Method     | Case                 | Mean     | Error    | StdDev  |
|----------- |--------------------- |---------:|---------:|--------:|
| OpenCohort | OpenA(...)ync-1 [30] | 435.9 ms | 10.72 ms | 0.59 ms |
