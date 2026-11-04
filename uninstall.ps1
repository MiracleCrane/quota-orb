param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($Destination).TrimEnd('\')
$receiptPath = Join-Path $root 'quota-orb-install.json'
$receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
if ($receipt.schemaVersion -ne 1 -or $receipt.component -ne 'quota-orb' -or $receipt.mode -ne 'component' -or $receipt.directory.TrimEnd('\') -ne $root) {
    throw 'Only a matching NoShortcuts component installation can be uninstalled automatically.'
}
if (Test-Path -LiteralPath (Join-Path $root 'linked-shortcuts')) { throw 'Existing shortcut bindings require explicit detachment first.' }
$target = Join-Path $root 'QuotaOrb.exe'
$instances = @(Get-Process QuotaOrb -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $target })
if ($instances.Count) {
    Start-Process -FilePath $target -ArgumentList '--exit' -WindowStyle Hidden -Wait
    foreach ($instance in $instances) { Wait-Process -Id $instance.Id -Timeout 8 -ErrorAction SilentlyContinue }
    if (Get-Process QuotaOrb -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $target }) { throw 'The installed orb has not exited.' }
}
# Fixed filenames only: never recursively delete a caller-supplied directory.
foreach ($name in @('QuotaOrb.exe','QuotaOrb.exe.config','QuotaOrb.Launcher.exe','LICENSE','quota-orb-install.json')) {
    $path = Join-Path $root $name
    if (Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path }
}
Write-Output 'Component files removed; settings, shortcuts and unknown files preserved.'
