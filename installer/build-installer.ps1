<#
.SYNOPSIS
    Publica o Darwin (Release, self-contained) e gera o instalador com o Inno Setup.

.EXAMPLE
    .\installer\build-installer.ps1
    .\installer\build-installer.ps1 -Arch arm64

    O instalador fica em artifacts\installer\Darwin-<versão>-<arch>-setup.exe.
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

Write-Host "==> Publicando Darwin ($rid)" -ForegroundColor Cyan
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

dotnet publish (Join-Path $root 'src\Darwin\Darwin.csproj') -c Release -r $rid --self-contained -o $publishDir -nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish falhou ($LASTEXITCODE)"
}

Write-Host "==> Gerando o instalador" -ForegroundColor Cyan
& $iscc /Q "/DSourceDir=$publishDir" "/DArch=$Arch" "/DOutputDir=$outputDir" (Join-Path $PSScriptRoot 'Darwin.iss')
if ($LASTEXITCODE -ne 0) {
    throw "ISCC falhou ($LASTEXITCODE)"
}

Get-ChildItem $outputDir -Filter "Darwin-*-$Arch-setup.exe" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 |
    ForEach-Object { Write-Host "==> $($_.FullName) ($([math]::Round($_.Length / 1MB, 1)) MB)" -ForegroundColor Green }
