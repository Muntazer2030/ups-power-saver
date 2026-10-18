# Get-Libs.ps1 - downloads LibreHardwareMonitor v0.9.6 (MPL-2.0) and extracts the DLLs needed to build.
# The download is pinned to a SHA-256 hash, so a tampered file is rejected.
$ErrorActionPreference = 'Stop'
$url  = 'https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases/download/v0.9.6/LibreHardwareMonitor.zip'
$hash = '086D9F1B5A99E643EDC2CFAAAC16051685B551E4C5AC0B32A57C58C0E529C001'
$lib  = Join-Path $PSScriptRoot 'lib'
$tmp  = Join-Path ([IO.Path]::GetTempPath()) ('lhm-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $tmp, $lib | Out-Null
try {
    $zip = Join-Path $tmp 'LibreHardwareMonitor.zip'
    Invoke-WebRequest $url -OutFile $zip
    if ((Get-FileHash $zip -Algorithm SHA256).Hash -ne $hash) { throw 'SHA-256 mismatch - refusing to use the downloaded file.' }
    Expand-Archive $zip (Join-Path $tmp 'x')
    Get-ChildItem (Join-Path $tmp 'x') -Filter *.dll |
        Where-Object Name -notmatch '^(Aga|OxyPlot|Microsoft\.Win32\.TaskScheduler)' |
        Copy-Item -Destination $lib -Force
    "Libraries ready in $lib"
}
finally { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
