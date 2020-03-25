``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 8.1 (6.3.9600.0)
Intel Core i7-4960HQ CPU 2.60GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
Frequency=2533206 Hz, Resolution=394.7567 ns, Timer=TSC
.NET Core SDK=3.0.100
  [Host]     : .NET Core 3.0.0 (CoreCLR 4.700.19.46205, CoreFX 4.700.19.46214), X64 RyuJIT
  DefaultJob : .NET Core 3.0.0 (CoreCLR 4.700.19.46205, CoreFX 4.700.19.46214), X64 RyuJIT


```
|  Method |      RowSet |         Mean |        Error |      StdDev | Ratio | RatioSD |     Gen 0 | Gen 1 | Gen 2 |   Allocated |
|-------- |------------ |-------------:|-------------:|------------:|------:|--------:|----------:|------:|------:|------------:|
|  **Static** |    **DeepRows** | **225,350.7 us** |    **915.42 us** |   **811.50 us** |  **1.00** |    **0.00** |         **-** |     **-** |     **-** | **15695.03 KB** |
| Dynamic |    DeepRows | 702,238.6 us | 10,128.76 us | 8,978.89 us |  3.12 |    0.04 | 1000.0000 |     - |     - | 94764.73 KB |
|         |             |              |              |             |       |         |           |       |       |             |
|  **Static** | **ShallowRows** |     **261.7 us** |      **3.01 us** |     **2.81 us** |  **1.00** |    **0.00** |         **-** |     **-** |     **-** |     **17.9 KB** |
| Dynamic | ShallowRows |     724.4 us |      3.01 us |     2.67 us |  2.77 |    0.04 |    0.9766 |     - |     - |    84.94 KB |
