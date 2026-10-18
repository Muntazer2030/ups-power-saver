# Build.ps1 - compiles "UPS Power Saver.exe" (single file) with the C# compiler built into Windows.
# Run from PowerShell:  .\source\Build.ps1     (add -Package to also create the release zip in dist\)
param([switch]$Package)
$ErrorActionPreference = 'Stop'
$src  = $PSScriptRoot
$root = Split-Path $src
$out  = Join-Path $root 'UPS Power Saver.exe'
$csc  = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path "$src\lib\LibreHardwareMonitorLib.dll") -or -not (Test-Path "$src\tools\PresentMon.exe")) { & "$src\Get-Libs.ps1" }
$res = @(Get-ChildItem "$src\lib\*.dll" | ForEach-Object { "/resource:$($_.FullName),dll_$($_.Name)" })
$res += "/resource:$src\tools\PresentMon.exe,tool_PresentMon.exe"
& $csc /nologo /codepage:65001 /target:winexe /platform:x64 /optimize+ "/out:$out" "/win32manifest:$src\app.manifest" "/win32icon:$src\app.ico" `
    "/lib:$src\lib" "/r:$src\lib\LibreHardwareMonitorLib.dll" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll `
    $res "$src\Program.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
"Built: $out"

if ($Package) {
    $ver  = (Get-Content (Join-Path $root 'VERSION') -Raw).Trim()
    $dist = Join-Path $root 'dist'; $stage = Join-Path $dist "UPS-Power-Saver-v$ver"
    Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force $stage | Out-Null
    Copy-Item $out, "$root\README.md", "$root\LICENSE", "$root\THIRD-PARTY-NOTICES.md" $stage
    Copy-Item "$root\docs" $stage -Recurse
    Compress-Archive -Path "$stage\*" -DestinationPath "$dist\UPS-Power-Saver-v$ver.zip"
    "Release zip: $dist\UPS-Power-Saver-v$ver.zip"
}
