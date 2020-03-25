``` ini

BenchmarkDotNet=v0.12.0, OS=Windows 8.1 (6.3.9600.0)
Intel Core i7-4960HQ CPU 2.60GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
Frequency=2533206 Hz, Resolution=394.7567 ns, Timer=TSC
.NET Core SDK=3.0.100
  [Host]     : .NET Core 3.0.0 (CoreCLR 4.700.19.46205, CoreFX 4.700.19.46214), X64 RyuJIT
  DefaultJob : .NET Core 3.0.0 (CoreCLR 4.700.19.46205, CoreFX 4.700.19.46214), X64 RyuJIT


```
| Method |      RowSet |   Library |         Mean |       Error |      StdDev |     Gen 0 | Gen 1 | Gen 2 |   Allocated |
|------- |------------ |---------- |-------------:|------------:|------------:|----------:|------:|------:|------------:|
|    **Run** |    **DeepRows** |     **Cesil** | **162,536.5 us** | **1,027.75 us** |   **961.36 us** |         **-** |     **-** |     **-** |  **7745.22 KB** |
|    **Run** |    **DeepRows** | **CsvHelper** | **217,906.4 us** | **3,475.35 us** | **3,250.84 us** | **1000.0000** |     **-** |     **-** | **77463.04 KB** |
|    **Run** | **ShallowRows** |     **Cesil** |     **195.6 us** |     **1.30 us** |     **1.22 us** |         **-** |     **-** |     **-** |     **9.91 KB** |
|    **Run** | **ShallowRows** | **CsvHelper** |   **7,799.5 us** |    **15.59 us** |    **12.17 us** |         **-** |     **-** |     **-** |   **212.17 KB** |
