$ErrorActionPreference = "Stop"

Write-Output 'Cleaning up old results'

Remove-Item -Path '.\TestCoverageResults\*' -Recurse -Force

Write-Output 'This folder is here to receive code coverage results.  Ignore this file.' > .\TestCoverageResults\README.txt

Write-Output 'Running Code Coverage'

dotnet test --collect "XPlat Code Coverage" -r .\TestCoverageResults -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.SingleHit=true DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.DoesNotReturnAttribute=DoesNotReturnAttribute

$CoverageFile = Get-ChildItem -Path ".\TestCoverageResults\" -r -Filter *.xml

Write-Output 'Coverage Complete, output: ' + $CoverageFile.FullName

dotnet .\ReportGenerator\ReportGenerator.dll -reports:$CoverageFile.FullName -targetdir:.\TestCoverageResults\

Write-Output 'Report generated'

if ($param1=$args[0] -ne 'silent') {
	Start-Process -Wait -FilePath '.\TestCoverageResults\index.htm' 
}