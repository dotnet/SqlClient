```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9711) (Hyper-V)
AMD EPYC 7763 2.44GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.402
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Job=MediumRun  OutlierMode=DontRemove  EnvironmentVariables=COMPlus_gcServer=1  
Toolchain=InProcessEmitToolchain  InvocationCount=1  IterationCount=3  
LaunchCount=1  RunStrategy=Monitoring  UnrollFactor=1  
WarmupCount=2  

```
| Method     | Case                 | Mean    | Error    | StdDev   |
|----------- |--------------------- |--------:|---------:|---------:|
| OpenCohort | OpenA(...)ync-1 [30] | 7.574 s | 0.3975 s | 0.0218 s |
