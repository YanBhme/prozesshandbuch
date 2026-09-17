@echo off
setlocal
set "PH_TARGET=%LOCALAPPDATA%\Programs\Prozesshandbuch"
echo App lokal entfernen? Gemeinsame Prozessdaten bleiben erhalten.
choice /c JN /n /m "J = Ja, N = Nein: "
if errorlevel 2 exit /b 0
echo Bitte die App vorher schliessen.
powershell.exe -NoProfile -Command "$ErrorActionPreference='Stop'; $d=Join-Path ([Environment]::GetFolderPath('Desktop')) 'Prozesshandbuch.lnk'; $m=Join-Path ([Environment]::GetFolderPath('Programs')) 'Prozesshandbuch'; foreach($p in @($d,(Join-Path $m 'Prozesshandbuch.lnk'),(Join-Path $m 'Deinstallieren.lnk'))){if(Test-Path -LiteralPath $p){Remove-Item -LiteralPath $p}}"
if errorlevel 1 (
 echo Verknuepfungen konnten nicht entfernt werden. Bitte IT kontaktieren.
 pause
 exit /b 1
)
for %%F in (Prozesshandbuch.exe Prozesshandbuch-neu.exe Pruefung.exe LIESMICH.txt) do if exist "%PH_TARGET%\%%F" del "%PH_TARGET%\%%F"
if exist "%PH_TARGET%\Prozesshandbuch.exe" (
 echo Bitte App schliessen und erneut versuchen.
 pause
 exit /b 1
)
echo App entfernt. Die lokale Ordnerauswahl bleibt erhalten.
pause
(goto) 2>nul & del "%PH_TARGET%\Deinstallieren.cmd"
