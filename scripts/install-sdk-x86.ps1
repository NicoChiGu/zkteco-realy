$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$dllSource = Join-Path $projectRoot 'dll\x86'
$target = Join-Path $projectRoot 'sdk\x86'
$developerSource = Join-Path $projectRoot '..\docs\脱机通讯开发包-6.3.1.55\SDK\x86'

if (Test-Path (Join-Path $dllSource 'zkemkeeper.dll')) {
    $source = $dllSource
} elseif (Test-Path (Join-Path $target 'zkemkeeper.dll')) {
    $source = $target
} elseif (Test-Path (Join-Path $developerSource 'zkemkeeper.dll')) {
    $source = $developerSource
} else {
    throw "ZKTeco x86 SDK was not found. Put the authorized DLL files in '$dllSource' or '$target'."
}

$sysDir = if ([Environment]::Is64BitOperatingSystem) {
    Join-Path $env:WINDIR 'SysWOW64'
} else {
    Join-Path $env:WINDIR 'System32'
}

Copy-Item -Path (Join-Path $source '*.dll') -Destination $sysDir -Force

$zkemkeeper = Join-Path $sysDir 'zkemkeeper.dll'
$regsvr32 = Join-Path $sysDir 'regsvr32.exe'
Start-Process -FilePath $regsvr32 -ArgumentList @('/s', "`"$zkemkeeper`"") -Verb RunAs -Wait

Write-Host "ZKTeco x86 SDK copied to $sysDir and registered."
