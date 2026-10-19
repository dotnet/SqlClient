```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
Intel Xeon Platinum 8168 CPU 2.70GHz (Max: 2.69GHz), 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Job=MediumRun  OutlierMode=DontRemove  EnvironmentVariables=COMPlus_gcServer=1  
Toolchain=InProcessEmitToolchain  InvocationCount=1  IterationCount=3  
LaunchCount=1  RunStrategy=Monitoring  UnrollFactor=1  
WarmupCount=0  

```
| Method     | Case                 | Mean     | Error      | StdDev   | Median   |
|----------- |--------------------- |---------:|-----------:|---------:|---------:|
| OpenCohort | OpenA(...)nc-32 [30] | 301.7 ms | 2,451.1 ms | 134.4 ms | 231.4 ms |
