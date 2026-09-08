#Requires -Version 5.0
<#
.SYNOPSIS
    Сборка velum.csproj с поиском MSBuild через vswhere (без ручного PATH).
.NOTES
    Для Inno Setup: список файлов в bin\<Configuration>\icons\ должен совпадать с секцией [Files] в installer\VelumSetup.iss (см. Content icons\ в velum.csproj).
#>
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $repoRoot 'velum.csproj'

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) {
    Write-Error "vswhere не найден: $vswhere. Установите Visual Studio или Build Tools."
}

$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild `
    -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null | Select-Object -First 1
if (-not $msbuild -or -not (Test-Path -LiteralPath $msbuild)) {
    Write-Error "MSBuild.exe не найден через vswhere."
}

Write-Host "MSBuild: $msbuild"
Write-Host "Проект:  $csproj"

$cfg = if ($args.Count -ge 1) { $args[0] } else { 'Debug' }

$contractCsproj = Join-Path $repoRoot '..\..\ISIDA\Programms\app\SymbiontEnv.Contract\SymbiontEnv.Contract.csproj'
$contractCsproj = [System.IO.Path]::GetFullPath($contractCsproj)
if (Test-Path -LiteralPath $contractCsproj) {
    Write-Host "SymbiontEnv.Contract: $contractCsproj"
    & $msbuild $contractCsproj /restore /t:Build /p:Configuration=$cfg /v:m
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

& $msbuild $csproj /restore /t:Build /p:Configuration=$cfg /v:m
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Сборка успешна ($cfg)."
