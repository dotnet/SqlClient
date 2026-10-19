```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9448/25H2/2025Update/HudsonValley2) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

Job=MediumRun  OutlierMode=DontRemove  EnvironmentVariables=COMPlus_gcServer=1  
Toolchain=InProcessEmitToolchain  InvocationCount=1  IterationCount=3  
LaunchCount=1  RunStrategy=Monitoring  UnrollFactor=1  
WarmupCount=2  

```
| Method     | Case                 | Mean     | Error     | StdDev   |
|----------- |--------------------- |---------:|----------:|---------:|
| OpenCohort | OpenA(...)ync-1 [30] | 77.70 ms | 169.22 ms | 9.276 ms |
