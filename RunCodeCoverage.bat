@ECHO OFF

echo Cleaning up old results

del .\TestCoverageResults\* /Q

FOR /F "tokens=* USEBACKQ" %%F IN (`where dotnet`) DO (
  SET dotnetpath=%%F
)

echo Path to dotnet: %dotnetpath%

.\OpenCover\OpenCover.Console.exe -target:"%dotnetpath%" -targetargs:"test" -output:".\TestCoverageResults\Coverage.xml" -register:user -threshold:1 -filter:"+[Cesil]* -[Cesil]*Attribute" -searchdirs:".\Cesil.Tests\bin\Debug\netcoreapp3.0"
if %errorlevel% neq 0 exit /b %errorlevel%

echo Coverage complete

dotnet .\ReportGenerator\ReportGenerator.dll -reports:.\TestCoverageResults\Coverage.xml -targetdir:.\TestCoverageResults\ 
if %errorlevel% neq 0 exit /b %errorlevel%

echo Report generated

if not "%1" == "silent" start .\TestCoverageResults\index.htm