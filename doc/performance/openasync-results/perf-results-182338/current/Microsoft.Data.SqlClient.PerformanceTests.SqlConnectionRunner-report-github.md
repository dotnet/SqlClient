```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
Intel Xeon Platinum 8168 CPU 2.70GHz (Max: 2.69GHz), 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Job=MediumRun  EnvironmentVariables=COMPlus_gcServer=1  Toolchain=InProcessEmitToolchain  
InvocationCount=30  IterationCount=50  LaunchCount=1  
RunStrategy=Throughput  UnrollFactor=1  WarmupCount=5  

```
| Method              | MARS  | Pooling | Mean         | Error      | StdDev      | Median       | Completed Work Items | Lock Contentions | Allocated |
|-------------------- |------ |-------- |-------------:|-----------:|------------:|-------------:|---------------------:|-----------------:|----------:|
| **OpenConnection**      | **False** | **False**   | **6,321.975 μs** | **79.7427 μs** | **149.7762 μs** | **6,282.973 μs** |               **5.0000** |                **-** |  **50.43 KB** |
| OpenAsyncConnection | False | False   | 6,610.870 μs | 39.7793 μs |  75.6842 μs | 6,613.686 μs |               7.0000 |                - |  70.77 KB |
| **OpenConnection**      | **False** | **True**    |     **3.019 μs** |  **0.0689 μs** |   **0.1328 μs** |     **2.998 μs** |                    **-** |                **-** |      **1 KB** |
| OpenAsyncConnection | False | True    |     3.428 μs |  0.1857 μs |   0.3666 μs |     3.258 μs |                    - |                - |    1.2 KB |
| **OpenConnection**      | **True**  | **False**   | **6,343.586 μs** | **35.8855 μs** |  **69.1392 μs** | **6,329.011 μs** |               **7.4000** |                **-** |  **69.96 KB** |
| OpenAsyncConnection | True  | False   | 6,611.904 μs | 45.7329 μs |  88.1117 μs | 6,596.077 μs |               9.0000 |                - |  82.26 KB |
| **OpenConnection**      | **True**  | **True**    |     **3.574 μs** |  **0.0872 μs** |   **0.1639 μs** |     **3.552 μs** |                    **-** |                **-** |      **1 KB** |
| OpenAsyncConnection | True  | True    |     3.434 μs |  0.0977 μs |   0.1882 μs |     3.405 μs |                    - |                - |    1.2 KB |
