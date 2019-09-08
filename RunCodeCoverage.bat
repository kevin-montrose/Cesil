@ECHO OFF

del .\TestCoverageResults\* /Q
.\OpenCover\OpenCover.Console.exe -target:"c:\Program Files\dotnet\dotnet.exe" -targetargs:"test" -output:".\TestCoverageResults\Coverage.xml" -register:user -threshold:1 -filter:"+[Cesil]* -[Cesil]*Attribute" -searchdirs:".\Cesil.Tests\bin\Debug\netcoreapp3.0"
dotnet .\ReportGenerator\ReportGenerator.dll -reports:.\TestCoverageResults\Coverage.xml -targetdir:.\TestCoverageResults\ 
open .\TestCoverageResults\index.htm