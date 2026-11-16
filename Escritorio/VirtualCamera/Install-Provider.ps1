param(
    [Parameter(Mandatory=$true)][Guid]$CameraId,
    [Parameter(Mandatory=$true)][ValidatePattern('^S-1-5-21-\d+-\d+-\d+-\d+$|^S-1-12-1-\d+-\d+-\d+-\d+$')][string]$UserSid
)
$ErrorActionPreference = 'Stop'
try {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Ejecuta este instalador como administrador.' }
    if (![Environment]::Is64BitProcess) { throw 'El instalador requiere PowerShell de 64 bits.' }
    $source = Join-Path $PSScriptRoot 'WTMultiCam.VirtualCamera.dll'
    if (!(Test-Path -LiteralPath $source -PathType Leaf)) { throw 'No se encuentra el proveedor nativo junto al instalador.' }
    $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    # Versioned, admin-owned installation: never load a service DLL from a user-writable build directory.
    $installRoot = Join-Path $env:ProgramFiles 'WilkensTech\WTMultiCam\VirtualCamera'
    $versionRoot = Join-Path $installRoot $hash
    New-Item -ItemType Directory -Path $versionRoot -Force | Out-Null
    $dll = Join-Path $versionRoot 'WTMultiCam.VirtualCamera.dll'
    if (!(Test-Path -LiteralPath $dll)) { Copy-Item -LiteralPath $source -Destination $dll }
    if ((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) { throw 'La copia instalada no coincide con el proveedor compilado.' }
    $framesRoot = Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'WilkensTech\WTMultiCam\VirtualCamera\Frames'
    $userRoot = Join-Path $framesRoot $UserSid
    New-Item -ItemType Directory -Path $userRoot -Force | Out-Null
    $acl = New-Object Security.AccessControl.DirectorySecurity
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($sid in @('S-1-5-18', 'S-1-5-32-544', $UserSid)) {
        $identity = New-Object Security.Principal.SecurityIdentifier($sid)
        $rule = New-Object Security.AccessControl.FileSystemAccessRule($identity, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
        $acl.AddAccessRule($rule)
    }
    # The Windows Camera Frame Server runs as Local Service. Other users are not granted frame access.
    $service = New-Object Security.Principal.SecurityIdentifier('S-1-5-19')
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($service, 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
    Set-Acl -LiteralPath $userRoot -AclObject $acl
    $framePath = Join-Path $userRoot ($CameraId.ToString('D') + '.bin')
    $machine = [Microsoft.Win32.RegistryKey]::OpenBaseKey('LocalMachine', 'Registry64')
    try {
        $keyName = 'SOFTWARE\Classes\CLSID\' + $CameraId.ToString('B')
        $existing = $machine.OpenSubKey($keyName)
        if ($existing) {
            try {
                if ($existing.GetValue('WTMultiCamSource', 0) -ne 1 -or $existing.GetValue('OwnerSid', '') -ne $UserSid) {
                    throw 'El identificador COM ya está registrado para otra fuente o usuario.'
                }
            } finally { $existing.Dispose() }
        }
        $key = $machine.CreateSubKey($keyName)
        try {
            $key.SetValue('', 'WT MultiCam Camera Source')
            $key.SetValue('WTMultiCamSource', 1, [Microsoft.Win32.RegistryValueKind]::DWord)
            $key.SetValue('OwnerSid', $UserSid)
            $key.SetValue('FramePath', $framePath)
            $key.SetValue('ProviderHash', $hash)
            $server = $key.CreateSubKey('InprocServer32')
            try { $server.SetValue('', $dll); $server.SetValue('ThreadingModel', 'Both') } finally { $server.Dispose() }
        } finally { $key.Dispose() }
    } finally { $machine.Dispose() }
    Write-Output 'Proveedor WT MultiCam registrado. La aplicación activará la cámara virtual.'
    exit 0
} catch {
    Write-Error $_ -ErrorAction Continue
    exit 1
}
