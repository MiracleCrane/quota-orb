param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$version = ([version](Get-Item -LiteralPath (Join-Path $PSScriptRoot 'artifacts\QuotaOrb.exe')).VersionInfo.FileVersion).ToString(3)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$staging = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ('staging-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $staging 'artifacts'),(Join-Path $staging 'docs') -Force | Out-Null
foreach ($name in @('QuotaOrb.exe','QuotaOrb.exe.config','QuotaOrb.Launcher.exe')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('artifacts\'+$name)) -Destination (Join-Path $staging 'artifacts') }
foreach ($name in @('install.ps1','uninstall.ps1','Get-QuotaOrb.ps1','README.md','LICENSE','THIRD_PARTY.md')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $staging }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\component-contract.md') -Destination (Join-Path $staging 'docs')
$archive = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ('quota-orb-'+$version+'-windows.zip')
if (Test-Path -LiteralPath $archive) { throw 'Release archive already exists; use a new output directory.' }
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $archive
[IO.File]::WriteAllText((Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) 'SHA256SUMS'),((Get-FileHash -LiteralPath $archive).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($archive)+"`n"),[Text.UTF8Encoding]::new($false))
Write-Output $archive
