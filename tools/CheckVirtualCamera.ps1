# Read-only prerequisites check. Does not register COM classes, cameras, drivers or devices.
$buildNumber = [Environment]::OSVersion.Version.Build
$moduleHandle = [IntPtr]::Zero
$apiAddress = [IntPtr]::Zero
$hasApi = $false
try {
    if ([Runtime.InteropServices.NativeLibrary]::TryLoad('mfsensorgroup.dll', [ref]$moduleHandle)) {
        $hasApi = [Runtime.InteropServices.NativeLibrary]::TryGetExport($moduleHandle, 'MFCreateVirtualCamera', [ref]$apiAddress)
    }
} finally {
    if ($moduleHandle -ne [IntPtr]::Zero) { [Runtime.InteropServices.NativeLibrary]::Free($moduleHandle) }
}
$sdkIncludeRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\Include'
$sdkHeaders = @()
if (Test-Path -LiteralPath $sdkIncludeRoot) {
    $sdkHeaders = @(Get-ChildItem -LiteralPath $sdkIncludeRoot -Directory | Where-Object {
        Test-Path -LiteralPath (Join-Path $_.FullName 'um\mfvirtualcamera.h')
    })
}
$vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$compilerPaths = @()
if (Test-Path -LiteralPath $vswherePath) {
    $compilerPaths = @(& $vswherePath -all -prerelease -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'VC\Tools\MSVC\**\bin\Hostx64\x64\cl.exe')
}
if ($compilerPaths.Count -eq 0) {
    $vsRoot = Join-Path $env:ProgramFiles 'Microsoft Visual Studio'
    if (Test-Path -LiteralPath $vsRoot) {
        $compilerPaths = @(Get-ChildItem -Path "$vsRoot\*\*\VC\Tools\MSVC\*\bin\Hostx64\x64\cl.exe" -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
    }
}
[pscustomobject]@{
    WindowsBuild = $buildNumber
    Process64Bit = [Environment]::Is64BitProcess
    VirtualCameraApiSupported = ($buildNumber -ge 22000 -and $hasApi)
    WindowsSdkHeaderFound = ($sdkHeaders.Count -gt 0)
    MsvcCompilerFound = ($compilerPaths.Count -gt 0)
    NativeBuildPrerequisitesReady = ($sdkHeaders.Count -gt 0 -and $compilerPaths.Count -gt 0)
    NativeCameraProviderImplemented = (Test-Path -LiteralPath (Join-Path $PSScriptRoot '..\Escritorio\VirtualCamera\Provider.cpp'))
    NativeCameraProviderBuilt = (Test-Path -LiteralPath (Join-Path $PSScriptRoot '..\Escritorio\VirtualCamera\bin\x64\WTMultiCam.VirtualCamera.dll'))
    RegistersDevices = $false
} | ConvertTo-Json
