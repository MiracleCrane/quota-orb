param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'artifacts'), [switch]$Test)
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.8 compiler is required.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$references = @('System.dll','System.Core.dll','System.Web.Extensions.dll','System.Drawing.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
$references += Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Xaml\v4.0_4.0.0.0__b77a5c561934e089\System.Xaml.dll'
$arguments = @('/nologo','/optimize+','/target:winexe','/platform:anycpu',('/out:' + (Join-Path $OutputDirectory 'QuotaOrb.exe')),('/win32manifest:' + (Join-Path $PSScriptRoot 'src\app.manifest')))
foreach ($reference in $references) { $arguments += '/reference:' + $(if ([IO.Path]::IsPathRooted($reference)) {$reference} else {Join-Path $framework $reference}) }
$arguments += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\QuotaOrb.exe.config') -Destination $OutputDirectory -Force
if ($Test) {
    $testArguments = @('/nologo','/target:exe',('/out:' + (Join-Path $OutputDirectory 'QuotaOrb.Tests.exe')),('/reference:' + (Join-Path $OutputDirectory 'QuotaOrb.exe')))
    foreach ($reference in $references) { $testArguments += '/reference:' + $(if ([IO.Path]::IsPathRooted($reference)) {$reference} else {Join-Path $framework $reference}) }
    $testArguments += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' | ForEach-Object FullName)
    & $compiler @testArguments
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & (Join-Path $OutputDirectory 'QuotaOrb.Tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
Get-FileHash -LiteralPath (Join-Path $OutputDirectory 'QuotaOrb.exe') -Algorithm SHA256
