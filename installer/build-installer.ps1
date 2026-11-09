<#
.SYNOPSIS
    Publica o Gearbox (Release, self-contained) e gera o instalador com o Inno Setup.

.EXAMPLE
    .\installer\build-installer.ps1
    .\installer\build-installer.ps1 -Arch arm64

    O instalador fica em artifacts\installer\Gearbox-<versão>-<arch>-setup.exe.
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'arm64')]
    [string] $Arch = 'x64'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
$rid = "win-$Arch"
$publishDir = Join-Path $root "artifacts\publish\$rid"
$outputDir = Join-Path $root 'artifacts\installer'

# Inno Setup: no PATH ou nos locais padrão (instalação por usuário ou por máquina).
$iscc = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $iscc) {
    throw 'Inno Setup 6 não encontrado. Instale com: winget install JRSoftware.InnoSetup'
}

$project = Join-Path $root 'src\Gearbox\Gearbox.csproj'
$version = (dotnet msbuild $project -getProperty:Version -nologo | Select-Object -Last 1).Trim()
$semVer = '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-(0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*)(\.(0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*))*)?(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$'
if ($LASTEXITCODE -ne 0 -or $version -notmatch $semVer) {
    throw "Versão '$version' do Directory.Build.props não segue o SemVer 2.0 (ex.: 0.5.0, 0.5.0-alpha.1)"
}
$version = $version.Split('+')[0]

Write-Host "==> Publicando Gearbox $version ($rid)" -ForegroundColor Cyan
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

dotnet publish $project -c Release -r $rid --self-contained -o $publishDir -nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish falhou ($LASTEXITCODE)"
}

Write-Host "==> Gerando o instalador" -ForegroundColor Cyan
& $iscc /Q "/DSourceDir=$publishDir" "/DArch=$Arch" "/DAppVersion=$version" "/DOutputDir=$outputDir" (Join-Path $PSScriptRoot 'Gearbox.iss')
if ($LASTEXITCODE -ne 0) {
    throw "ISCC falhou ($LASTEXITCODE)"
}

$installer = Get-Item (Join-Path $outputDir "Gearbox-$version-$Arch-setup.exe")
Write-Host "==> $($installer.FullName) ($([math]::Round($installer.Length / 1MB, 1)) MB)" -ForegroundColor Green
