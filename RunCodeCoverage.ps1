$ErrorActionPreference = "Stop"

Write-Output 'Cleaning up old results'

Remove-Item -Path '.\TestCoverageResults\*' -Recurse -Force

Write-Output 'This folder is here to receive code coverage results.  Ignore this file.' > .\TestCoverageResults\README.txt

Start-Process -Wait -NoNewWindow -FilePath 'dotnet' -ArgumentList 'test','/p:CollectCoverage=true','/p:CoverletOutputFormat=opencover','/p:CoverletOutput=.\..\TestCoverageResults\Coverage.xml','/p:SingleHit=true'

Write-Output 'Coverage complete'

Start-Process -Wait -NoNewWindow -FilePath 'dotnet' -ArgumentList '.\ReportGenerator\ReportGenerator.dll','-reports:.\TestCoverageResults\Coverage.xml','-targetdir:.\TestCoverageResults\'

if ($process.ExitCode -gt 0) {
	return
}

Write-Output 'Report generated'

if ($param1=$args[0] -ne 'silent') {
	Start-Process -Wait -FilePath '.\TestCoverageResults\index.htm' 
}
