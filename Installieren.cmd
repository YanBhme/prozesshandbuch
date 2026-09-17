@echo off
setlocal
chcp 65001 >nul
title Prozesshandbuch installieren
set "PH_SOURCE=%~dp0"
set "PH_TARGET=%LOCALAPPDATA%\Programs\Prozesshandbuch"
set "PH_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%PH_CSC%" set "PH_CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%PH_CSC%" (
 echo Der Windows-.NET-Compiler fehlt. Bitte eure IT kontaktieren.
 pause
 exit /b 1
)
echo Prozesshandbuch Version 0.6.0 wird erstellt. Bitte eine laufende Version vorher schliessen.
if not exist "%PH_TARGET%" mkdir "%PH_TARGET%"
if errorlevel 1 goto failed
"%PH_CSC%" /nologo /codepage:65001 /target:winexe /win32manifest:"%PH_SOURCE%app.manifest" /optimize+ /out:"%PH_TARGET%\Prozesshandbuch-neu.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /resource:"%PH_SOURCE%Logo-Bruno-Gruettner.jpg",BrandLogo "%PH_SOURCE%Prozesshandbuch.cs"
if errorlevel 1 goto failed
"%PH_CSC%" /nologo /codepage:65001 /target:exe /main:StorageTests /out:"%PH_TARGET%\Pruefung.exe" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /resource:"%PH_SOURCE%Logo-Bruno-Gruettner.jpg",BrandLogo "%PH_SOURCE%Prozesshandbuch.cs" "%PH_SOURCE%Tests.cs"
if errorlevel 1 goto failed
"%PH_TARGET%\Pruefung.exe"
if errorlevel 1 goto failed
move /y "%PH_TARGET%\Prozesshandbuch-neu.exe" "%PH_TARGET%\Prozesshandbuch.exe" >nul
if errorlevel 1 goto failed
copy /y "%PH_SOURCE%LIESMICH.txt" "%PH_TARGET%\LIESMICH.txt" >nul
copy /y "%PH_SOURCE%Deinstallieren.cmd" "%PH_TARGET%\Deinstallieren.cmd" >nul
powershell.exe -NoProfile -Command "$ErrorActionPreference='Stop'; $w=New-Object -ComObject WScript.Shell; $d=[Environment]::GetFolderPath('Desktop'); $m=Join-Path ([Environment]::GetFolderPath('Programs')) 'Prozesshandbuch'; [IO.Directory]::CreateDirectory($m)|Out-Null; foreach($p in @((Join-Path $d 'Prozesshandbuch.lnk'),(Join-Path $m 'Prozesshandbuch.lnk'))){$s=$w.CreateShortcut($p);$s.TargetPath=Join-Path $env:PH_TARGET 'Prozesshandbuch.exe';$s.WorkingDirectory=$env:PH_TARGET;$s.Save()}; $u=$w.CreateShortcut((Join-Path $m 'Deinstallieren.lnk'));$u.TargetPath=Join-Path $env:PH_TARGET 'Deinstallieren.cmd';$u.Save()"
if errorlevel 1 (
 echo App erstellt, aber Verknuepfung fehlgeschlagen. Bitte IT kontaktieren.
 echo Die App liegt in: %PH_TARGET%
 pause
 exit /b 1
)
echo Installation abgeschlossen.
start "" "%PH_TARGET%\Prozesshandbuch.exe"
exit /b 0
:failed
echo Installation nicht abgeschlossen. Bitte diese Fehlermeldung an eure IT geben.
echo Es wurden keine Daten auf dem Netzlaufwerk veraendert.
pause
exit /b 1
