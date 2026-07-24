$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$dllSource = Join-Path $projectRoot 'dll\x64'
$target = Join-Path $projectRoot 'sdk\x64'
$developerSource = Join-Path $projectRoot '..\docs\脱机通讯开发包-6.3.1.55\SDK\x64'

if (Test-Path (Join-Path $dllSource 'zkemkeeper.dll')) {
    $source = $dllSource
} elseif (Test-Path (Join-Path $target 'zkemkeeper.dll')) {
    $source = $target
} elseif (Test-Path (Join-Path $developerSource 'zkemkeeper.dll')) {
    $source = $developerSource
} else {
    throw "ZKTeco x64 SDK was not found. Put the authorized DLL files in '$dllSource' or '$target'."
}

$system32 = Join-Path $env:WINDIR 'System32'
Copy-Item -Path (Join-Path $source '*.dll') -Destination $system32 -Force

$zkemkeeper = Join-Path $system32 'zkemkeeper.dll'
$regsvr32 = Join-Path $system32 'regsvr32.exe'
Start-Process -FilePath $regsvr32 -ArgumentList @('/s', "`"$zkemkeeper`"") -Verb RunAs -Wait

Write-Host "ZKTeco x64 SDK copied to $system32 and registered."
