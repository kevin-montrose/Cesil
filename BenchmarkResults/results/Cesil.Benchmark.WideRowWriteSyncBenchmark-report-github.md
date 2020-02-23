``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 8.1 (6.3.9600.0)
Intel Core i7-4960HQ CPU 2.60GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
Frequency=2533207 Hz, Resolution=394.7565 ns, Timer=TSC
.NET Core SDK=3.0.100
  [Host]     : .NET Core 3.0.0 (CoreCLR 4.700.19.46205, CoreFX 4.700.19.46214), X64 RyuJIT
  DefaultJob : .NET Core 3.0.0 (CoreCLR 4.700.19.46205, CoreFX 4.700.19.46214), X64 RyuJIT


```
| Method |      RowSet |   Library |         Mean |     Error |    StdDev |    Gen 0 | Gen 1 | Gen 2 |   Allocated |
|------- |------------ |---------- |-------------:|----------:|----------:|---------:|------:|------:|------------:|
|    **Run** |    **DeepRows** |     **Cesil** | **104,564.1 us** | **318.95 us** | **298.34 us** |        **-** |     **-** |     **-** |  **2634.08 KB** |
|    **Run** |    **DeepRows** | **CsvHelper** | **148,433.9 us** | **446.76 us** | **396.04 us** | **333.3333** |     **-** |     **-** | **38708.45 KB** |
|    **Run** | **ShallowRows** |     **Cesil** |     **105.6 us** |   **0.22 us** |   **0.19 us** |        **-** |     **-** |     **-** |     **5.42 KB** |
|    **Run** | **ShallowRows** | **CsvHelper** |  **16,684.0 us** |  **70.35 us** |  **62.36 us** |        **-** |     **-** |     **-** |   **321.45 KB** |
