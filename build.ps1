$ErrorActionPreference = 'Stop'
$base = $PSScriptRoot
$out = Join-Path $base 'dist'
New-Item -ItemType Directory -Force $out | Out-Null
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path $csc)) { throw 'Windows .NET Framework compiler missing' }
$refs = @('/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.Web.Extensions.dll')
$source = Join-Path $base 'Prozesshandbuch.cs'
$logo = Join-Path $base 'Logo-Bruno-Gruettner.jpg'
& $csc /nologo /codepage:65001 /target:winexe /optimize+ "/win32manifest:$base\app.manifest" "/out:$out\Prozesshandbuch.exe" $refs "/resource:$logo,BrandLogo" $source
if ($LASTEXITCODE -ne 0) { throw 'App compilation failed' }
& $csc /nologo /codepage:65001 /target:exe /main:StorageTests "/out:$out\Tests.exe" $refs "/resource:$logo,BrandLogo" $source "$base\Tests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
& "$out\Tests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Storage tests failed' }
$package = Join-Path $out 'Paket'
New-Item -ItemType Directory -Force $package | Out-Null
Copy-Item "$out\Prozesshandbuch.exe" $package
Copy-Item "$base\LIESMICH.txt" $package
Copy-Item "$base\Installieren.cmd","$base\Deinstallieren.cmd","$base\Prozesshandbuch.cs","$base\Tests.cs","$base\app.manifest","$base\Logo-Bruno-Gruettner.jpg" $package
Compress-Archive -Path "$package\*" -DestinationPath "$out\Prozesshandbuch-Windows.zip" -Force
