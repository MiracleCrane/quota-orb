param(
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs\QuotaOrb'),
    [switch]$StartWithWindows
)
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'artifacts'
if (-not (Test-Path -LiteralPath (Join-Path $source 'QuotaOrb.exe'))) { & (Join-Path $PSScriptRoot 'build.ps1') }
$destinationFull = [IO.Path]::GetFullPath($Destination)
$target = Join-Path $destinationFull 'QuotaOrb.exe'
$running = @(Get-Process QuotaOrb -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $target })
if ($running.Count) { throw 'Please exit Quota Orb from its options menu before updating.' }
New-Item -ItemType Directory -Path $destinationFull -Force | Out-Null
foreach ($name in @('QuotaOrb.exe','QuotaOrb.exe.config')) { Copy-Item -LiteralPath (Join-Path $source $name) -Destination $destinationFull -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination $destinationFull -Force
$shell = New-Object -ComObject WScript.Shell
$folders = @([Environment]::GetFolderPath('Desktop'), (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'))
if ($StartWithWindows) { $folders += [Environment]::GetFolderPath('Startup') }
foreach ($folder in $folders) {
    $link = $shell.CreateShortcut((Join-Path $folder 'Quota Orb.lnk'))
    $link.TargetPath = $target
    $link.WorkingDirectory = $destinationFull
    $link.Description = 'Codex 额度悬浮球'
    $link.Save()
}
Write-Output "Installed to $destinationFull"
Write-Output 'Start from the Quota Orb shortcut. Settings are stored in %LOCALAPPDATA%\QuotaOrb.'
