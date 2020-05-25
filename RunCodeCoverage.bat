@ECHO OFF

echo Cleaning up old results

del .\TestCoverageResults\* /Q

echo This folder is here to receive code coverage results.  Ignore this file.>.\TestCoverageResults\README.txt

FOR /F "tokens=* USEBACKQ" %%F IN (`where dotnet`) DO (
  SET dotnetpath=%%F
)

echo Path to dotnet: %dotnetpath%

if "%2" == "" (
  SET register=-register:user
) ELSE (
  if "%2" == "none" (
  	SET register=
  ) ELSE (
  	SET register=-register:%2
  )
)

echo Registration method: %register%

.\OpenCover\OpenCover.Console.exe -target:"%dotnetpath%" -targetargs:"test" -output:".\TestCoverageResults\Coverage.xml" %register% -threshold:1 -filter:"+[Cesil]* -[Cesil]*Attribute" -searchdirs:".\Cesil.Tests\bin\Debug\netcoreapp3.0"
if %errorlevel% neq 0 exit /b %errorlevel%

echo Coverage complete

dotnet .\ReportGenerator\ReportGenerator.dll -reports:.\TestCoverageResults\Coverage.xml -targetdir:.\TestCoverageResults\ 
if %errorlevel% neq 0 exit /b %errorlevel%

echo Report generated

if not "%1" == "silent" start .\TestCoverageResults\index.htm