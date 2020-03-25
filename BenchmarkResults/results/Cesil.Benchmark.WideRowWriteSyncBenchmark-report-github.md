``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 8.1 (6.3.9600.0)
Intel Core i7-4960HQ CPU 2.60GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
Frequency=2533206 Hz, Resolution=394.7567 ns, Timer=TSC
.NET Core SDK=3.0.100
  [Host]     : .NET Core 3.0.0 (CoreCLR 4.700.19.46205, CoreFX 4.700.19.46214), X64 RyuJIT
  DefaultJob : .NET Core 3.0.0 (CoreCLR 4.700.19.46205, CoreFX 4.700.19.46214), X64 RyuJIT


```
| Method |      RowSet |   Library |         Mean |       Error |      StdDev |    Gen 0 | Gen 1 | Gen 2 |   Allocated |
|------- |------------ |---------- |-------------:|------------:|------------:|---------:|------:|------:|------------:|
|    **Run** |    **DeepRows** |     **Cesil** | **100,460.3 us** |   **243.41 us** |   **203.26 us** |        **-** |     **-** |     **-** |  **2635.35 KB** |
|    **Run** |    **DeepRows** | **CsvHelper** | **144,204.5 us** | **1,979.82 us** | **1,851.92 us** | **500.0000** |     **-** |     **-** | **38707.59 KB** |
|    **Run** | **ShallowRows** |     **Cesil** |     **101.6 us** |     **0.41 us** |     **0.36 us** |        **-** |     **-** |     **-** |     **5.41 KB** |
|    **Run** | **ShallowRows** | **CsvHelper** |  **16,150.1 us** |    **35.07 us** |    **32.81 us** |        **-** |     **-** |     **-** |    **322.3 KB** |
