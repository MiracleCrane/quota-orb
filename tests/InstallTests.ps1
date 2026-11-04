$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path ([IO.Path]::GetTempPath()) ('quota-orb-install-' + [guid]::NewGuid().ToString('N'))
$watched = @((Join-Path ([Environment]::GetFolderPath('Desktop')) 'Quota Orb.lnk'),(Join-Path ([Environment]::GetFolderPath('Programs')) 'Quota Orb.lnk'),(Join-Path ([Environment]::GetFolderPath('Startup')) 'Quota Orb.lnk'))
function Snapshot {
    @($watched | ForEach-Object { if(Test-Path -LiteralPath $_){(Get-FileHash -LiteralPath $_).Hash}else{'absent'} }) -join '|'
}
$before = Snapshot
& (Join-Path $repo 'install.ps1') -Destination $root -NoShortcuts
$status = (& (Join-Path $repo 'Get-QuotaOrb.ps1') -Destination $root) | ConvertFrom-Json
if (-not $status.installed -or $status.running -or $status.statusFresh) { throw 'Component query must report its own installation only.' }
if ($status.processIds.Count -ne 0) { throw 'A stopped component must return an empty PID array.' }
& (Join-Path $repo 'install.ps1') -Destination $root -NoShortcuts
if ((Snapshot) -ne $before) { throw 'Component install modified a shortcut.' }
[IO.File]::WriteAllText((Join-Path $root 'keep.txt'),'preserve user files')
& (Join-Path $repo 'uninstall.ps1') -Destination $root
if ((Test-Path -LiteralPath (Join-Path $root 'QuotaOrb.exe')) -or -not (Test-Path -LiteralPath (Join-Path $root 'keep.txt'))) { throw 'Uninstall must remove only component files.' }
if ((Snapshot) -ne $before) { throw 'Component uninstall modified a shortcut.' }
$rejected = $false
try { & (Join-Path $repo 'install.ps1') -Destination ($root+'-invalid') -NoShortcuts -CodexShortcutPath 'not-a-real-shortcut.lnk' } catch { $rejected = $true }
if (-not $rejected -or (Test-Path -LiteralPath ($root+'-invalid'))) { throw 'Conflicting shortcut modes must fail before making changes.' }
Write-Output 'PASS: component install/reinstall/query/uninstall preserve shortcuts and unknown files; conflicting modes rejected.'
