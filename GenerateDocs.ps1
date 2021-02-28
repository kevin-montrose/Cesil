$ErrorActionPreference = "Stop"

$contents = @'
<html>
<head>
<meta http-equiv="refresh" content="0;https://kevin-montrose.github.io/Cesil/api/Cesil.html" />
</head>
</html>
'@

.\docfx\docfx.exe .\docfx.json; Set-Content -Path '.\docs\index.html' -Value $contents
