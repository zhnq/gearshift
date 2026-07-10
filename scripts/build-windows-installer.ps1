param(
  [Parameter(Mandatory = $true)][string]$Version,
  [Parameter(Mandatory = $true)][string]$PublishDir,
  [Parameter(Mandatory = $true)][string]$OutputDir
)

$ErrorActionPreference = 'Stop'
$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source
if (-not $iscc) {
  $candidates = @(
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
  )
  foreach ($candidate in $candidates) {
    if (Test-Path $candidate) { $iscc = $candidate; break }
  }
}
if (-not $iscc) { throw '未找到 Inno Setup 6（ISCC.exe）' }

$env:GEARSHIFT_VERSION = $Version
$env:GEARSHIFT_PUBLISH_DIR = (Resolve-Path $PublishDir).Path
New-Item -ItemType Directory -Force $OutputDir | Out-Null
$env:GEARSHIFT_OUTPUT_DIR = (Resolve-Path $OutputDir).Path
& $iscc (Join-Path $PSScriptRoot '..\packaging\windows\GearShift.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup 失败 ($LASTEXITCODE)" }
