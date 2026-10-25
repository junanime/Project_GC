[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][uint32]$AppId,
    [Parameter(Mandatory=$true)][string]$UnityPath,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [string]$ProjectPath = (Join-Path $PSScriptRoot '../..')
)
$ErrorActionPreference = 'Stop'
if ($AppId -eq 0 -or $AppId -eq 480) { throw 'Use the real Steamworks App ID after approval.' }
$projectRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
$outputRoot = [IO.Path]::GetFullPath($OutputPath)
if (Test-Path -LiteralPath $outputRoot) { throw 'Choose a new build output directory.' }
foreach ($protectedFolder in @('Assets','Packages','ProjectSettings','Library','.git')) {
    $protectedRoot = Join-Path $projectRoot $protectedFolder
    if ($outputRoot.StartsWith($protectedRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw "Build output must not be inside $protectedFolder." }
}
$version = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') -TotalCount 1
Write-Host "Required editor: $version"
$oldAppId=$env:PROJECT_GC_STEAM_APP_ID; $oldOutput=$env:PROJECT_GC_WINDOWS_OUTPUT; $oldCommit=$env:PROJECT_GC_SOURCE_COMMIT
try {
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    $env:PROJECT_GC_STEAM_APP_ID=[string]$AppId; $env:PROJECT_GC_WINDOWS_OUTPUT=$outputRoot
    $env:PROJECT_GC_SOURCE_COMMIT=(& git -C $projectRoot rev-parse HEAD)
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the source commit.' }
    $buildLog=Join-Path $outputRoot 'unity-build.log'
    $process=Start-Process -FilePath $UnityPath -ArgumentList @('-batchmode','-nographics','-job-worker-count','2','-buildTarget','Win64','-projectPath',('"'+$projectRoot+'"'),'-executeMethod','Vampire.Editor.WindowsDesktopBuild.RunSteam','-logFile',('"'+$buildLog+'"')) -WindowStyle Hidden -PassThru -Wait
    if ($process.ExitCode -ne 0) { throw "Unity failed. Inspect $buildLog" }
    Write-Host "Steam Windows build completed: $outputRoot"
} finally {
    $env:PROJECT_GC_STEAM_APP_ID=$oldAppId; $env:PROJECT_GC_WINDOWS_OUTPUT=$oldOutput; $env:PROJECT_GC_SOURCE_COMMIT=$oldCommit
}
