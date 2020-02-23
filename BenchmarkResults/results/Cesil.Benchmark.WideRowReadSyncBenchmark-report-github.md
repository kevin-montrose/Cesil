``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 8.1 (6.3.9600.0)
Intel Core i7-4960HQ CPU 2.60GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
Frequency=2533207 Hz, Resolution=394.7565 ns, Timer=TSC
.NET Core SDK=3.0.100
  [Host]     : .NET Core 3.0.0 (CoreCLR 4.700.19.46205, CoreFX 4.700.19.46214), X64 RyuJIT
  DefaultJob : .NET Core 3.0.0 (CoreCLR 4.700.19.46205, CoreFX 4.700.19.46214), X64 RyuJIT


```
| Method |      RowSet |   Library |         Mean |       Error |      StdDev |     Gen 0 | Gen 1 | Gen 2 |   Allocated |
|------- |------------ |---------- |-------------:|------------:|------------:|----------:|------:|------:|------------:|
|    **Run** |    **DeepRows** |     **Cesil** | **167,736.4 us** | **1,417.45 us** | **1,325.89 us** |         **-** |     **-** |     **-** |   **7745.6 KB** |
|    **Run** |    **DeepRows** | **CsvHelper** | **227,033.6 us** | **1,912.32 us** | **1,695.22 us** | **1000.0000** |     **-** |     **-** | **77462.29 KB** |
|    **Run** | **ShallowRows** |     **Cesil** |     **203.8 us** |     **0.47 us** |     **0.44 us** |         **-** |     **-** |     **-** |     **9.96 KB** |
|    **Run** | **ShallowRows** | **CsvHelper** |   **8,173.3 us** |    **41.46 us** |    **38.78 us** |         **-** |     **-** |     **-** |   **212.34 KB** |
