$ErrorActionPreference = 'Stop'

$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$source = Join-Path $projectDir 'Program.cs'
$manifest = Join-Path $projectDir 'app.manifest'
$icon = Join-Path $projectDir 'app-icon.ico'
$distDir = Join-Path $projectDir 'dist'
$output = Join-Path $distDir 'AltPrtScnCapture.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw '.NET Framework C# compiler was not found.'
}

New-Item -ItemType Directory -Force -Path $distDir | Out-Null

& $compiler `
    /nologo `
    /target:winexe `
    /platform:anycpu `
    /optimize+ `
    /win32manifest:$manifest `
    /win32icon:$icon `
    /resource:$icon,AltPrtScnCapture.app-icon.ico `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /out:$output `
    $source

if ($LASTEXITCODE -ne 0) {
    throw "Compilation failed with exit code $LASTEXITCODE."
}

Write-Host "Built: $output"
