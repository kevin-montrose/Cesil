@ECHO OFF

echo Cleaning up old results

del .\TestCoverageResults\* /Q

echo This folder is here to receive code coverage results.  Ignore this file.>.\TestCoverageResults\README.txt

dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput=.\..\TestCoverageResults\Coverage.xml /p:SingleHit=true

echo Coverage complete

dotnet .\ReportGenerator\ReportGenerator.dll -reports:.\TestCoverageResults\Coverage.xml -targetdir:.\TestCoverageResults\ 
if %errorlevel% neq 0 exit /b %errorlevel%

echo Report generated

if not "%1" == "silent" start .\TestCoverageResults\index.htm