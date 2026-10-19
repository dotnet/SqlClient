```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
Intel Xeon Platinum 8168 CPU 2.70GHz (Max: 2.69GHz), 1 CPU, 32 logical and 16 physical cores
.NET SDK 10.0.401
  [Host] : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Job=MediumRun  EnvironmentVariables=COMPlus_gcServer=1  Toolchain=InProcessEmitToolchain  
InvocationCount=30  IterationCount=50  LaunchCount=1  
RunStrategy=Throughput  UnrollFactor=1  WarmupCount=5  

```
| Method              | MARS  | Pooling | Mean         | Error      | StdDev     | Median       | Completed Work Items | Lock Contentions | Allocated |
|-------------------- |------ |-------- |-------------:|-----------:|-----------:|-------------:|---------------------:|-----------------:|----------:|
| **OpenConnection**      | **False** | **False**   | **6,264.609 μs** | **32.3821 μs** | **64.6706 μs** | **6,264.239 μs** |               **5.0000** |                **-** |  **52.27 KB** |
| OpenAsyncConnection | False | False   | 6,651.976 μs | 49.1379 μs | 98.1338 μs | 6,632.828 μs |               7.0333 |                - |  72.61 KB |
| **OpenConnection**      | **False** | **True**    |     **3.028 μs** |  **0.0930 μs** |  **0.1770 μs** |     **2.997 μs** |                    **-** |                **-** |   **1.16 KB** |
| OpenAsyncConnection | False | True    |     2.985 μs |  0.0840 μs |  0.1578 μs |     2.965 μs |                    - |                - |   1.36 KB |
| **OpenConnection**      | **True**  | **False**   | **6,295.258 μs** | **34.6031 μs** | **68.3032 μs** | **6,274.125 μs** |               **7.4000** |                **-** |  **71.81 KB** |
| OpenAsyncConnection | True  | False   | 6,682.685 μs | 45.1977 μs | 90.2647 μs | 6,683.157 μs |               9.0000 |                - |  84.07 KB |
| **OpenConnection**      | **True**  | **True**    |     **3.317 μs** |  **0.1367 μs** |  **0.2601 μs** |     **3.247 μs** |                    **-** |                **-** |   **1.17 KB** |
| OpenAsyncConnection | True  | True    |     3.409 μs |  0.1620 μs |  0.3197 μs |     3.277 μs |                    - |                - |   1.36 KB |
