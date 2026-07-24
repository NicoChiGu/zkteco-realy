$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$target = Join-Path $projectRoot 'sdk\x86'
$dllSource = Join-Path $projectRoot 'dll\x86'
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

New-Item -ItemType Directory -Force -Path $target | Out-Null
if ((Resolve-Path $source).Path -ne (Resolve-Path $target).Path) {
    Copy-Item -Path (Join-Path $source '*.dll') -Destination $target -Force
}

$zkemkeeper = Join-Path $target 'zkemkeeper.dll'
$regsvr32 = if (Test-Path (Join-Path $env:WINDIR 'SysWOW64\regsvr32.exe')) {
    Join-Path $env:WINDIR 'SysWOW64\regsvr32.exe'
} else {
    Join-Path $env:WINDIR 'System32\regsvr32.exe'
}
Start-Process -FilePath $regsvr32 -ArgumentList @('/s', "`"$zkemkeeper`"") -Verb RunAs -Wait

Write-Host "ZKTeco x86 SDK registered from $target."
