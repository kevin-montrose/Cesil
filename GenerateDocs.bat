@ECHO OFF

.\docfx\docfx.exe .\docfx.json

(
	echo ^<html^>
	echo ^<head^>
	echo ^<meta http-equiv="refresh" content="0;https://kevin-montrose.github.io/Cesil/api/Cesil.html" /^>
	echo ^</head^>
	echo ^</html^>
) > .\docs\index.html