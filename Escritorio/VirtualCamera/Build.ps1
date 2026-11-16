param([switch]$RunChecks)
$ErrorActionPreference = 'Stop'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$compilers = @()
if (Test-Path -LiteralPath $vswhere) {
    $compilers = @(& $vswhere -all -prerelease -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'VC\Tools\MSVC\**\bin\Hostx64\x64\cl.exe')
}
if ($compilers.Count -eq 0) {
    $compilers = @(Get-ChildItem -Path "$env:ProgramFiles\Microsoft Visual Studio\*\*\VC\Tools\MSVC\*\bin\Hostx64\x64\cl.exe" -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
}
if ($compilers.Count -eq 0) { throw 'Instala Desarrollo para el escritorio con C++ en Visual Studio Installer.' }
$compiler = $compilers | Sort-Object -Descending | Select-Object -First 1
$msvc = [IO.Path]::GetFullPath((Join-Path (Split-Path $compiler) '..\..\..'))
$sdk = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10'
$version = Get-ChildItem -LiteralPath (Join-Path $sdk 'Include') -Directory | Where-Object {
    Test-Path -LiteralPath (Join-Path $_.FullName 'um\mfvirtualcamera.h')
} | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1 -ExpandProperty Name
if (!$version) { throw 'Instala el SDK de Windows 11.' }
$oldInclude = $env:INCLUDE; $oldLib = $env:LIB
$out = Join-Path $PSScriptRoot 'bin\x64'
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    $env:INCLUDE = "$msvc\include;$sdk\Include\$version\ucrt;$sdk\Include\$version\shared;$sdk\Include\$version\um;$sdk\Include\$version\winrt"
    $env:LIB = "$msvc\lib\x64;$sdk\Lib\$version\ucrt\x64;$sdk\Lib\$version\um\x64"
    $sources = @('Provider.cpp', 'FrameBridge.cpp') | ForEach-Object { Join-Path $PSScriptRoot $_ }
    $dependencies = @('Provider.cpp', 'FrameBridge.cpp', 'FrameBridge.h', 'Provider.def', 'Build.ps1') | ForEach-Object { Get-Item -LiteralPath (Join-Path $PSScriptRoot $_) }
    $binary = Join-Path $out 'WTMultiCam.VirtualCamera.dll'
    $rebuild = !(Test-Path -LiteralPath $binary)
    if (!$rebuild) { $rebuild = @($dependencies | Where-Object { $_.LastWriteTimeUtc -gt (Get-Item -LiteralPath $binary).LastWriteTimeUtc }).Count -gt 0 }
    if ($rebuild) {
    & $compiler /nologo /std:c++17 /EHsc /MT /O2 /W4 /wd4458 /DUNICODE /D_UNICODE /DNOMINMAX /LD @sources "/Fo$out\" "/Fe$out\WTMultiCam.VirtualCamera.dll" /link "/DEF:$PSScriptRoot\Provider.def" mfplat.lib mf.lib mfuuid.lib mfsensorgroup.lib ole32.lib oleaut32.lib advapi32.lib ksuser.lib /INCREMENTAL:NO
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación del proveedor de cámara virtual.' }
    } else { Write-Output 'Proveedor de cámara virtual actualizado; se reutiliza la DLL.' }
    if ($RunChecks) {
        & $compiler /nologo /std:c++17 /EHsc /MT /O2 /W4 /DUNICODE /D_UNICODE /DNOMINMAX (Join-Path $PSScriptRoot 'Checks.cpp') "/Fo$out\Checks.obj" "/Fe$out\VirtualCameraChecks.exe" /link mfplat.lib mf.lib mfuuid.lib ole32.lib oleaut32.lib /INCREMENTAL:NO
        if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de las pruebas nativas.' }
        & (Join-Path $out 'VirtualCameraChecks.exe')
        if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas nativas.' }
    }
} finally { $env:INCLUDE = $oldInclude; $env:LIB = $oldLib }
