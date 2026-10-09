param(
    [ValidateSet('Uncompressed', 'Compressed', 'FrameworkDependent')]
    [string]$Mode = 'Compressed',
    [string]$OutputRoot = '',
    [string]$BuildRoot = '',
    [switch]$NoRestore
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$parentRoot = Split-Path -Parent $projectRoot
$project = Join-Path $projectRoot 'WindowSizerWinUI.csproj'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $parentRoot 'WindowSizerReleases'
}
if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
    $BuildRoot = Join-Path $parentRoot 'WindowSizerReleaseBuild'
}
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$BuildRoot = [IO.Path]::GetFullPath($BuildRoot)
foreach ($root in @($OutputRoot, $BuildRoot)) {
    if ($root.Equals($projectRoot, [StringComparison]::OrdinalIgnoreCase) -or
        $root.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Build and release roots must be outside the source directory: $root"
    }
}
$releaseDirectory = Join-Path $OutputRoot $Mode
$buildDirectory = Join-Path $BuildRoot $Mode
$publishDirectory = Join-Path $buildDirectory 'Payload'
New-Item -ItemType Directory -Path $publishDirectory, $releaseDirectory -Force | Out-Null

$singleFile = $Mode -ne 'FrameworkDependent'
$compressed = $Mode -eq 'Compressed'
$arguments = @(
    'publish', $project, '-c', 'Release', '-r', 'win-x64',
    '-o', $publishDirectory, '--nologo',
    "-p:WindowsAppSDKSelfContained=$($singleFile.ToString().ToLowerInvariant())",
    "-p:SelfContained=$($singleFile.ToString().ToLowerInvariant())",
    "-p:PublishSingleFile=$($singleFile.ToString().ToLowerInvariant())",
    "-p:IncludeAllContentForSelfExtract=$($singleFile.ToString().ToLowerInvariant())",
    "-p:EnableCompressionInSingleFile=$($compressed.ToString().ToLowerInvariant())",
    '-p:PublishTrimmed=false',
    '-p:PublishReadyToRun=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
)
if ($NoRestore) { $arguments += '--no-restore' }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }

$publishedExe = Join-Path $publishDirectory 'WindowSizerWinUI.exe'
if (-not (Test-Path -LiteralPath $publishedExe)) {
    throw "Published EXE was not found: $publishedExe"
}

if ($Mode -eq 'FrameworkDependent') {
    $zip = Join-Path $releaseDirectory 'WindowSizerWinUI_FrameworkDependent.zip'
    Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $zip -Force
    Write-Host "Framework-dependent ZIP: $zip"
    exit 0
}

$nativeDirectory = Join-Path $buildDirectory 'NativeLauncher'
New-Item -ItemType Directory -Path $nativeDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'packaging\rename_safe_launcher.cpp') -Destination $nativeDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot 'Assets\AppIcon.ico') -Destination $nativeDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot 'app.manifest') -Destination $nativeDirectory

$resourceScript = @'
1 ICON "AppIcon.ico"
1 RT_MANIFEST "app.manifest"
101 RCDATA "..\\Payload\\WindowSizerWinUI.exe"
'@
Set-Content -LiteralPath (Join-Path $nativeDirectory 'payload.rc') -Value $resourceScript -Encoding Ascii

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) { throw 'Visual Studio C++ Build Tools were not found.' }
$visualStudio = (& $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Select-Object -First 1)
if (-not $visualStudio) { throw 'Visual Studio C++ Build Tools were not found.' }
$devCommand = Join-Path $visualStudio 'Common7\Tools\VsDevCmd.bat'

$nativeBuild = @"
@echo off
call "$devCommand" -arch=x64 -host_arch=x64 >nul
if errorlevel 1 exit /b 1
rc.exe /nologo /fo launcher.res payload.rc
if errorlevel 1 exit /b 1
cl.exe /nologo /std:c++20 /O2 /MT /DUNICODE /D_UNICODE /EHsc /utf-8 rename_safe_launcher.cpp launcher.res /link /SUBSYSTEM:WINDOWS /MANIFEST:NO /OUT:launcher.exe
exit /b %ERRORLEVEL%
"@
Set-Content -LiteralPath (Join-Path $nativeDirectory 'build-native.cmd') -Value $nativeBuild -Encoding Ascii
Push-Location $nativeDirectory
try {
    & cmd.exe /d /c build-native.cmd
    if ($LASTEXITCODE -ne 0) { throw "Native launcher build failed: $LASTEXITCODE" }
}
finally { Pop-Location }

$launcher = Join-Path $nativeDirectory 'launcher.exe'
if (-not (Test-Path -LiteralPath $launcher)) { throw 'Native launcher output is missing.' }
$outputExe = Join-Path $releaseDirectory 'WindowSizerWinUI.exe'
Copy-Item -LiteralPath $launcher -Destination $outputExe -Force
$releaseFiles = @(Get-ChildItem -LiteralPath $releaseDirectory -File -Force)
if ($releaseFiles.Count -ne 1 -or $releaseFiles[0].Name -ne 'WindowSizerWinUI.exe') {
    throw "The $Mode release must contain only WindowSizerWinUI.exe."
}
Write-Host "$Mode rename-safe EXE: $outputExe"
