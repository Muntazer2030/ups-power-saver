# Get-Libs.ps1 - downloads the third-party pieces needed to build:
#   LibreHardwareMonitor v0.9.6 (MPL-2.0) DLLs  and  PresentMon v2.6.0 (MIT) console tool.
# Each download is pinned to a SHA-256 hash, so a tampered file is rejected.
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

    # PresentMon (MIT) - reads game frame-present events for the FPS counter
    $pmUrl  = 'https://github.com/GameTechDev/PresentMon/releases/download/v2.6.0/PresentMon-2.6.0-x64.exe'
    $pmHash = 'B2A706BC6AD475749E3B7E3409263AA1E6906D45BDCF993F6DBC0F660188F1AF'
    $tools  = Join-Path $PSScriptRoot 'tools'
    New-Item -ItemType Directory -Force $tools | Out-Null
    $pm = Join-Path $tmp 'PresentMon.exe'
    Invoke-WebRequest $pmUrl -OutFile $pm
    if ((Get-FileHash $pm -Algorithm SHA256).Hash -ne $pmHash) { throw 'PresentMon SHA-256 mismatch - refusing to use the downloaded file.' }
    Copy-Item $pm (Join-Path $tools 'PresentMon.exe') -Force
    "PresentMon ready in $tools"
}
finally { Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue }
