param(
    [string]$Destination = (Join-Path $env:LOCALAPPDATA 'Programs\QuotaOrb'),
    [string[]]$CodexShortcutPath = @()
)
$ErrorActionPreference = 'Stop'
if (-not ('QuotaOrb.InstallationPaths' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace QuotaOrb {
    public static class InstallationPaths {
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder path, uint capacity, uint flags);
        public static string ExistingFile(string path) {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                var final = new StringBuilder(32768);
                uint length = GetFinalPathNameByHandle(file.SafeFileHandle, final, (uint)final.Capacity, 0);
                if (length == 0 || length >= final.Capacity) throw new Win32Exception(Marshal.GetLastWin32Error());
                string value = final.ToString();
                if (value.StartsWith(@"\\?\UNC\", StringComparison.Ordinal)) return @"\\" + value.Substring(8);
                return value.StartsWith(@"\\?\", StringComparison.Ordinal) ? value.Substring(4) : value;
            }
        }
    }
}
'@
}
$source = Join-Path $PSScriptRoot 'artifacts'
if (-not (Test-Path -LiteralPath (Join-Path $source 'QuotaOrb.exe')) -or -not (Test-Path -LiteralPath (Join-Path $source 'QuotaOrb.Launcher.exe'))) { & (Join-Path $PSScriptRoot 'build.ps1') }
$destinationFull = [IO.Path]::GetFullPath($Destination)
$target = Join-Path $destinationFull 'QuotaOrb.exe'
$linkedLauncher = Join-Path $destinationFull 'QuotaOrb.Launcher.exe'
if (Test-Path -LiteralPath $target) { $target = [QuotaOrb.InstallationPaths]::ExistingFile($target) }
if (Test-Path -LiteralPath $linkedLauncher) { $linkedLauncher = [QuotaOrb.InstallationPaths]::ExistingFile($linkedLauncher) }
$running = @(Get-Process QuotaOrb -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $target })
if ($running.Count) {
    if ([version](Get-Item -LiteralPath $target).VersionInfo.FileVersion -lt [version]'0.1.4.0') { throw 'Please exit the older Quota Orb once before updating.' }
    Start-Process -FilePath $target -ArgumentList '--exit' -WindowStyle Hidden -Wait
    foreach ($ownedProcess in $running) { Wait-Process -Id $ownedProcess.Id -Timeout 8 -ErrorAction SilentlyContinue }
    if (Get-Process QuotaOrb -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $target }) { throw 'Quota Orb is still running.' }
}
$shell = New-Object -ComObject WScript.Shell
$settingsDirectory = Join-Path $env:LOCALAPPDATA 'QuotaOrb'
$backup = Join-Path $settingsDirectory ('shortcut-backups\' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
$launchersDirectory = Join-Path $settingsDirectory 'launchers'
$validatedLinks = @()
foreach ($path in $CodexShortcutPath) {
    $resolvedLink = (Resolve-Path -LiteralPath $path).Path
    if ([IO.Path]::GetExtension($resolvedLink) -ne '.lnk') { throw 'Only existing shortcuts may be bound.' }
    $link = $shell.CreateShortcut($resolvedLink)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $id = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($resolvedLink.ToLowerInvariant())))).Replace('-','').Substring(0,16).ToLowerInvariant() } finally { $sha.Dispose() }
    $originalShortcut = Join-Path $destinationFull ('linked-shortcuts\' + $id + '-original.lnk')
    $snapshotSource = $resolvedLink
    $entryTarget = $link.TargetPath
    if (Test-Path -LiteralPath $entryTarget -PathType Leaf) { $entryTarget = [QuotaOrb.InstallationPaths]::ExistingFile($entryTarget) }
    if ($entryTarget -eq $linkedLauncher) {
        if ($link.Arguments -match '^--shortcut "([^"]+)"$') {
            $snapshotSource = (Resolve-Path -LiteralPath $Matches[1]).Path
        } elseif ($link.Arguments -match '^--link ([a-f0-9]{16})$') {
            $previousConfigPath = Join-Path $launchersDirectory ($Matches[1] + '.json')
            $previousConfig = Get-Content -LiteralPath $previousConfigPath -Raw | ConvertFrom-Json
            $snapshotSource = (Resolve-Path -LiteralPath $previousConfig.OriginalShortcut).Path
        } else { throw 'Cannot locate the original shortcut for this existing binding.' }
    }
    $validatedLinks += [pscustomobject]@{ Path=$resolvedLink; Link=$link; Id=$id; OriginalShortcut=$originalShortcut; SnapshotSource=$snapshotSource; SourceHash=(Get-FileHash -LiteralPath $resolvedLink).Hash; Icon=$link.IconLocation }
}
New-Item -ItemType Directory -Path $destinationFull -Force | Out-Null
foreach ($name in @('QuotaOrb.exe','QuotaOrb.exe.config','QuotaOrb.Launcher.exe')) { Copy-Item -LiteralPath (Join-Path $source $name) -Destination $destinationFull -Force }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination $destinationFull -Force
$target = [QuotaOrb.InstallationPaths]::ExistingFile((Join-Path $destinationFull 'QuotaOrb.exe'))
$linkedLauncher = [QuotaOrb.InstallationPaths]::ExistingFile((Join-Path $destinationFull 'QuotaOrb.Launcher.exe'))
$installedDirectory = Split-Path -Parent $target
$folders = @([Environment]::GetFolderPath('Desktop'), (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'))
foreach ($folder in $folders) {
    $link = $shell.CreateShortcut((Join-Path $folder 'Quota Orb.lnk'))
    $link.TargetPath = $target
    $link.Arguments = '--toggle'
    $link.WorkingDirectory = $installedDirectory
    $link.Description = '点击开启或关闭 Codex 额度悬浮球'
    $link.Save()
}
# Linked mode has no logon watcher. Preserve the old shortcut outside Startup.
$oldStartup = Join-Path ([Environment]::GetFolderPath('Startup')) 'Quota Orb.lnk'
if (Test-Path -LiteralPath $oldStartup) {
    $oldLink = $shell.CreateShortcut($oldStartup)
    if ($oldLink.TargetPath -eq $target) {
        New-Item -ItemType Directory -Path $backup -Force | Out-Null
        Move-Item -LiteralPath $oldStartup -Destination (Join-Path $backup 'previous-logon-start.lnk')
    }
}
if ($validatedLinks.Count) {
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    New-Item -ItemType Directory -Path $launchersDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $destinationFull 'linked-shortcuts') -Force | Out-Null
    $index = 0
    foreach ($entry in $validatedLinks) {
        if ((Get-FileHash -LiteralPath $entry.Path).Hash -ne $entry.SourceHash) { throw 'The shortcut changed during installation; re-read it before retrying.' }
        Copy-Item -LiteralPath $entry.Path -Destination (Join-Path $backup (([string]$index) + '-' + [IO.Path]::GetFileName($entry.Path)))
        $sourcePhysical = [QuotaOrb.InstallationPaths]::ExistingFile($entry.SnapshotSource)
        $sameSnapshot = (Test-Path -LiteralPath $entry.OriginalShortcut) -and $sourcePhysical -eq [QuotaOrb.InstallationPaths]::ExistingFile($entry.OriginalShortcut)
        if (-not $sameSnapshot) { Copy-Item -LiteralPath $entry.SnapshotSource -Destination $entry.OriginalShortcut -Force }
        $entry.OriginalShortcut = [QuotaOrb.InstallationPaths]::ExistingFile($entry.OriginalShortcut)
        $configPath = Join-Path $launchersDirectory ($entry.Id + '.json')
        if (Test-Path -LiteralPath $configPath) { Copy-Item -LiteralPath $configPath -Destination (Join-Path $backup ($entry.Id + '-config.before.json')) }
        [IO.File]::WriteAllText($configPath, ([pscustomobject]@{OriginalShortcut=$entry.OriginalShortcut}|ConvertTo-Json), [Text.UTF8Encoding]::new($false))
        $entry.Link.TargetPath = $linkedLauncher
        $entry.Link.Arguments = '--shortcut "' + $entry.OriginalShortcut + '"'
        $entry.Link.WorkingDirectory = $installedDirectory
        if ($entry.Icon) { $entry.Link.IconLocation = $entry.Icon }
        $entry.Link.Save()
        $index++
    }
}
Write-Output "Installed to $destinationFull"
Write-Output 'Quota Orb shortcut toggles the ball. The ball exits after the last Codex window closes.'
