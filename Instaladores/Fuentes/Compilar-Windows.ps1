param(
    [switch]$OfflineLocal,
    [string]$RuntimeVersion = '8.0.31',
    [string]$NuGetCache = (Join-Path $env:USERPROFILE '.nuget\packages'),
    [string]$PlatformTools = (Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools')
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$work = Join-Path $projectRoot 'build\packaging'
$payload = Join-Path $work 'desktop'
$output = Join-Path $PSScriptRoot '..\Windows'
New-Item -ItemType Directory -Path $work, $output -Force | Out-Null
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_HOME = Join-Path $env:TEMP 'wt-dotnet-home'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$arguments = @('publish', (Join-Path $projectRoot 'Escritorio\MovilCamara\MovilCamara.csproj'), '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-p:Version=1.0.0-beta.1', '-p:FileVersion=1.0.0.0', '-p:AssemblyVersion=1.0.0.0', '-p:UseSharedCompilation=false', '-nodeReuse:false', '-o', $payload)
if ($OfflineLocal) {
    if (!(Test-Path -LiteralPath $NuGetCache)) { throw 'No existe la caché NuGet local.' }
    $env:NUGET_PACKAGES = $NuGetCache
    $config = Join-Path $work 'NuGet.Offline.Config'
    $escaped = [Security.SecurityElement]::Escape($NuGetCache)
    [IO.File]::WriteAllText($config, "<configuration><packageSources><clear/><add key=`"local`" value=`"$escaped`"/></packageSources><packageSourceMapping><clear/><packageSource key=`"local`"><package pattern=`"*`"/></packageSource></packageSourceMapping></configuration>")
    $dotnetRoot = Split-Path (Get-Command dotnet).Source
    $hostPack = Get-ChildItem (Join-Path $dotnetRoot 'packs\Microsoft.NETCore.App.Host.win-x64') -Directory |
        Where-Object { $_.Name -match '^8\.0\.' } | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
    if (!$hostPack) { throw 'Falta el apphost .NET 8 x64 local.' }
    $hostSource = Join-Path $hostPack.FullName 'runtimes\win-x64\native\apphost.exe'
    $arguments += @(('-p:RuntimeFrameworkVersion=' + $RuntimeVersion), '-p:EnableAppHostPackDownload=false', '-p:UseAppHostFromAssetsFile=false', ('-p:AppHostSourcePath=' + $hostSource), '-p:NuGetAudit=false', '--configfile', $config)
}
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación de Windows.' }
New-Item -ItemType Directory -Path (Join-Path $payload 'Tools') -Force | Out-Null
foreach ($name in @('adb.exe', 'AdbWinApi.dll', 'AdbWinUsbApi.dll', 'libwinpthread-1.dll', 'NOTICE.txt', 'source.properties')) {
    $source = Join-Path $PlatformTools $name
    if (!(Test-Path -LiteralPath $source)) { throw "Falta el componente oficial de Platform Tools: $name" }
    Copy-Item -LiteralPath $source -Destination (Join-Path $payload 'Tools')
}
$exe = Join-Path $payload 'MovilCamara.exe'
$report = Join-Path $work 'verification.txt'
$check = Start-Process -FilePath $exe -ArgumentList @('--verify-installation', ('"--report=' + $report + '"')) -WorkingDirectory $payload -WindowStyle Hidden -Wait -PassThru
if ($check.ExitCode -ne 0) { throw 'Falló la comprobación del paquete (runtime/proveedor/ADB).' }
$compiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'Falta Inno Setup 6 para generar el instalador.' }
& $compiler '/Q' ("/DPayloadDir=" + $payload) (Join-Path $PSScriptRoot 'WTMultiCam.iss')
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de Inno Setup.' }
$setup = Join-Path $output 'WTMultiCam-Setup-1.0.0-beta.1-win-x64.exe'
$hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $output 'SHA256.txt'), "$hash  $([IO.Path]::GetFileName($setup))`r`n")
Write-Output "Instalador generado: $setup"
