[CmdletBinding()]
param([string]$ArtifactsPath = $env:TEMP)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Join-Path ([IO.Path]::GetFullPath($ArtifactsPath)) ('steam-depot-tests-' + [guid]::NewGuid().ToString('N'))
$source = Join-Path $root 'input'
$destination = Join-Path $root 'prepared'
New-Item -ItemType Directory -Path (Join-Path $source '24tu_Data/Plugins/x86_64'),(Join-Path $source '24tu_BurstDebugInformation_DoNotShip') -Force | Out-Null
# Text fixtures only: never create or upload a player with these test IDs.
foreach ($file in @('24tu.exe','UnityPlayer.dll','Steamworks.NET-LICENSE.txt','24tu_Data/Plugins/x86_64/steam_api64.dll','24tu_Data/scene.asset','steam_appid.txt','debug.pdb','unity-build.log','24tu_BurstDebugInformation_DoNotShip/symbols.txt')) {
    [IO.File]::WriteAllText((Join-Path $source $file),'isolated packaging fixture')
}
$manifest = @{steamEnabled=$true;steamAppId=12345;sourceCommit=('a'*40);company='DefaultCompany';product='TestGame'}
function Save-Manifest { [IO.File]::WriteAllText((Join-Path $source 'desktop-build.json'),($manifest | ConvertTo-Json)) }
Save-Manifest
$script:checks = 0
function Check([bool]$ok,[string]$message) { if (!$ok) { throw $message }; $script:checks++; Write-Host "PASS $message" }
function Reject([scriptblock]$action,[string]$message) {
    $failed = $false
    try { & $action | Out-Null } catch { $failed = $true }
    Check $failed $message
}
$prepare = Join-Path $PSScriptRoot 'Prepare-SteamDepot.ps1'
& $prepare -BuildPath $source -OutputPath $destination -AppId 12345 -DepotId 12346
$preview = Get-Content -LiteralPath (Join-Path $destination 'scripts/app_build_preview.vdf') -Raw
$upload = Get-Content -LiteralPath (Join-Path $destination 'scripts/app_build_upload.vdf') -Raw
Check ($preview -match '"Preview" "1"' -and $upload -match '"Preview" "0"') 'preview and upload are separate'
Check ($upload -notmatch 'SetLive' -and $preview -notmatch 'SetLive') 'neither command publishes a branch'
Check ($upload -match '"AppID" "12345"' -and $upload -match '"12346" "depot_12346.vdf"') 'depot and application IDs match'
$record = Get-Content -LiteralPath (Join-Path $destination 'depot-manifest.json') -Raw | ConvertFrom-Json
Check ($record.files.Count -eq 6 -and $record.omitted.Count -eq 4) 'only intended player files included'
Check (!(Test-Path -LiteralPath (Join-Path $destination 'content/steam_appid.txt'))) 'development app ID excluded'
foreach ($file in $record.files) {
    Check ((Get-FileHash -LiteralPath (Join-Path (Join-Path $destination 'content') $file.path) -Algorithm SHA256).Hash.ToLowerInvariant() -eq $file.sha256) ('payload hash ' + $file.path)
}
$cloud = Get-Content -LiteralPath (Join-Path $destination 'steam-cloud-settings.json') -Raw | ConvertFrom-Json
Check ($cloud.root -eq 'WinAppDataLocalLow' -and $cloud.subdirectory -eq 'DefaultCompany/TestGame/SteamProfiles/{64BitSteamID}') 'cloud directory is account-specific'
Check ($cloud.pattern -eq 'progress*.json' -and !$cloud.recursive -and !$cloud.dynamicCloudSync) 'only completed progress and backup files synchronize at launch and exit'
Reject { & $prepare -BuildPath $source -OutputPath $destination -AppId 12345 -DepotId 12346 } 'existing output never overwritten'
Reject { & $prepare -BuildPath $source -OutputPath (Join-Path $source 'nested') -AppId 12345 -DepotId 12346 } 'nested output rejected'
Reject { & $prepare -BuildPath $source -OutputPath (Join-Path $root 'invalid-id') -AppId 480 -DepotId 12346 } 'Spacewar ID rejected'
Reject { & $prepare -BuildPath $source -OutputPath (Join-Path $root 'mismatch') -AppId 54321 -DepotId 12346 } 'wrong application rejected'
$manifest.steamEnabled=$false; Save-Manifest
Reject { & $prepare -BuildPath $source -OutputPath (Join-Path $root 'non-steam') -AppId 12345 -DepotId 12346 } 'non-Steam build rejected'
$manifest.steamEnabled=$true; $manifest.sourceCommit='unknown'; Save-Manifest
Reject { & $prepare -BuildPath $source -OutputPath (Join-Path $root 'unknown-source') -AppId 12345 -DepotId 12346 } 'unidentified build rejected'
$manifest.sourceCommit='a'*40; Save-Manifest
# Rename within the verified fixture directory; originals stay available for inspection.
Rename-Item -LiteralPath (Join-Path $source '24tu_Data/Plugins/x86_64/steam_api64.dll') -NewName 'steam_api64.missing'
Reject { & $prepare -BuildPath $source -OutputPath (Join-Path $root 'missing-native') -AppId 12345 -DepotId 12346 } 'missing native Steam library rejected'
Write-Host "COMPLETE $checks checks; isolated artifacts: $root"
