param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference = 'Stop'
$target = Join-Path ([IO.Path]::GetFullPath($Destination)) 'QuotaOrb.exe'
$installed = Test-Path -LiteralPath $target -PathType Leaf
$instances = @(Get-Process QuotaOrb -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $target })
$processIds = @($instances | ForEach-Object { $_.Id })
$status = $null
$fresh = $false
$statusPath = Join-Path $env:LOCALAPPDATA 'QuotaOrb\status.json'
if (Test-Path -LiteralPath $statusPath) {
    try {
        $candidate = Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json
        $age = ((Get-Date).ToUniversalTime() - (Get-Item -LiteralPath $statusPath).LastWriteTimeUtc).TotalSeconds
        $fresh = $candidate.running -and ($candidate.processId -in $processIds) -and $age -ge 0 -and $age -lt 10
        if ($fresh) { $status = $candidate }
    } catch { }
}
[pscustomobject]@{schemaVersion=1;component='quota-orb';installed=$installed;version=$(if($installed){(Get-Item -LiteralPath $target).VersionInfo.FileVersion}else{$null});running=($instances.Count -gt 0);processIds=$processIds;statusFresh=[bool]$fresh;status=$status} | ConvertTo-Json -Depth 8
