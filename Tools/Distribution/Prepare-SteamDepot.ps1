[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$BuildPath,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [Parameter(Mandatory=$true)][uint32]$AppId,
    [Parameter(Mandatory=$true)][uint32]$DepotId
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($AppId -eq 0 -or $AppId -eq 480 -or $DepotId -eq 0) { throw 'Real Steam application and Windows depot IDs are required.' }
$buildRoot = (Resolve-Path -LiteralPath $BuildPath).Path.TrimEnd('\','/')
$outputRoot = [IO.Path]::GetFullPath($OutputPath).TrimEnd('\','/')
if ($outputRoot.Equals($buildRoot,[StringComparison]::OrdinalIgnoreCase) -or $outputRoot.StartsWith($buildRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Output must be outside the build source.' }
if (Test-Path -LiteralPath $outputRoot) { throw 'Choose a new output directory; existing files will not be overwritten.' }
if ((Get-Item -LiteralPath $buildRoot).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Use the resolved build directory, not a junction.' }
$buildManifest = Join-Path $buildRoot 'desktop-build.json'
if (!(Test-Path -LiteralPath $buildManifest)) { throw 'Build the Steam Windows player first.' }
$build = Get-Content -LiteralPath $buildManifest -Raw | ConvertFrom-Json
if (!$build.PSObject.Properties['steamEnabled'] -or !$build.steamEnabled -or [uint32]$build.steamAppId -ne $AppId) { throw 'The build must be Steam-enabled and match this App ID.' }
if ($build.sourceCommit -notmatch '\A[0-9a-fA-F]{40}\z') { throw 'A full source commit is required in desktop-build.json.' }
foreach ($required in @('24tu.exe','24tu_Data','UnityPlayer.dll','Steamworks.NET-LICENSE.txt')) {
    if (!(Test-Path -LiteralPath (Join-Path $buildRoot $required))) { throw "Required build content is missing: $required" }
}
if ($build.product -match '[\\/]' -or $build.company -match '[\\/]') { throw 'Company/product names cannot contain path separators.' }
function Write-Utf8([string]$path,[string]$content) { [IO.File]::WriteAllText($path,$content,(New-Object Text.UTF8Encoding($false))) }
function VdfPath([string]$path) {
    if ($path -match '["\r\n]') { throw 'Invalid VDF path.' }
    return $path.Replace('\','/')
}
$contentRoot = Join-Path $outputRoot 'content'
$scriptRoot = Join-Path $outputRoot 'scripts'
$logRoot = Join-Path $outputRoot 'logs'
New-Item -ItemType Directory -Path $contentRoot,$scriptRoot,$logRoot -Force | Out-Null
$records = New-Object Collections.Generic.List[object]
$omitted = New-Object Collections.Generic.List[string]
foreach ($entry in Get-ChildItem -LiteralPath $buildRoot -Recurse -Force) {
    if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Unexpected link in build: $($entry.FullName)" }
    if ($entry.PSIsContainer) { continue }
    $relative = $entry.FullName.Substring($buildRoot.Length+1).Replace('\','/')
    if ($relative -match '(^|/)([^/]*_DoNotShip|Logs|Temp|\.git)(/|$)' -or $entry.Name -eq 'steam_appid.txt' -or $entry.Extension -in @('.pdb','.mdb','.log','.tmp','.bak')) { $omitted.Add($relative); continue }
    $target = [IO.Path]::GetFullPath((Join-Path $contentRoot $relative))
    if (!$target.StartsWith($contentRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Content path escaped its directory.' }
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
    Copy-Item -LiteralPath $entry.FullName -Destination $target
    $records.Add([pscustomobject]@{path=$relative;bytes=$entry.Length;sha256=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()})
}
if (!($records | Where-Object { $_.path.EndsWith('/steam_api64.dll') })) { throw 'Steam native library missing.' }
$depot = @"
"DepotBuildConfig"
{
    "DepotID" "$DepotId"
    "FileMapping"
    {
        "LocalPath" "*"
        "DepotPath" "."
        "recursive" "1"
    }
    "FileExclusion" "steam_appid.txt"
    "FileExclusion" "*.pdb"
    "FileExclusion" "*.mdb"
}
"@
Write-Utf8 (Join-Path $scriptRoot "depot_$DepotId.vdf") $depot
foreach ($mode in @('preview','upload')) {
    $preview = if ($mode -eq 'preview') { '1' } else { '0' }
    $app = @"
"AppBuild"
{
    "AppID" "$AppId"
    "Desc" "24tu Windows $($build.sourceCommit)"
    "ContentRoot" "$(VdfPath $contentRoot)"
    "BuildOutput" "$(VdfPath $logRoot)"
    "Preview" "$preview"
    "Depots"
    {
        "$DepotId" "depot_$DepotId.vdf"
    }
}
"@
    Write-Utf8 (Join-Path $scriptRoot "app_build_$mode.vdf") $app
}
$cloud = [ordered]@{
    appId=$AppId;byteQuotaPerUser=1048576;filesPerUser=10;dynamicCloudSync=$false;
    root='WinAppDataLocalLow';subdirectory="$($build.company)/$($build.product)/SteamProfiles/{64BitSteamID}";
    pattern='progress*.json';osRestriction='Cross-Platform';recursive=$false;
    note='Configure and publish these values in Steamworks. This JSON is a setup record, not an automatic upload.'
}
Write-Utf8 (Join-Path $outputRoot 'steam-cloud-settings.json') ($cloud | ConvertTo-Json -Depth 5)
Write-Utf8 (Join-Path $outputRoot 'depot-manifest.json') ([ordered]@{appId=$AppId;depotId=$DepotId;sourceCommit=$build.sourceCommit;files=$records.ToArray();omitted=$omitted.ToArray()} | ConvertTo-Json -Depth 5)
Write-Host "Prepared $($records.Count) files. Nothing has been uploaded or made live."
Write-Host "SteamPipe preview: $(Join-Path $scriptRoot 'app_build_preview.vdf')"
Write-Host "SteamPipe upload:  $(Join-Path $scriptRoot 'app_build_upload.vdf')"
