param(
    [Parameter(Mandatory=$true)][string]$BuildFolder,
    [Parameter(Mandatory=$true)][string]$InstallFolder
)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $BuildFolder).Path.TrimEnd('\')
$target = [IO.Path]::GetFullPath($InstallFolder).TrimEnd('\')
$parent = [IO.Path]::GetDirectoryName($target)
if (-not $parent -or $target -eq [IO.Path]::GetPathRoot($target)) { throw 'Specify a game installation folder, not a drive root.' }
if ($source -eq $target -or $source.StartsWith($target+'\',[StringComparison]::OrdinalIgnoreCase) -or $target.StartsWith($source+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Build and installation folders must be separate.' }
$manifest = Get-Content -LiteralPath (Join-Path $source 'desktop-build.json') -Raw | ConvertFrom-Json
if ($manifest.steamEnabled -or -not $manifest.sourceCommit -or $manifest.sourceCommit -eq 'unknown') { throw 'Expected a verified standalone festival build with its source commit.' }
if (-not $manifest.festival -or $manifest.festivalDuration -ne 45 -or $manifest.festivalRules -ne 'festival_pc_v6_45') { throw 'Expected the 45-second festival ruleset.' }
$required = @('24tu.exe','UnityPlayer.dll','24tu_Data/Managed/Assembly-CSharp.dll','PLAY_GUIDE.md','실행안내.txt')
foreach ($name in $required) { if (-not (Test-Path -LiteralPath (Join-Path $source $name))) { throw "Incomplete build: $name" } }
if (Test-Path -LiteralPath $target) {
    if (-not (Test-Path -LiteralPath (Join-Path $target '24tu.exe'))) { throw 'Target is not an existing 24tu installation.' }
    $running = Get-Process -Name '24tu' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $target '24tu.exe') }
    if ($running) { throw 'Close the festival game before updating its installation.' }
}
New-Item -ItemType Directory -Path $parent -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$staged = $target+'.new-'+$stamp
$backup = $target+'.backup-'+$stamp
# Validate every computed rename destination stays in the explicitly named installation parent.
foreach ($path in @($staged,$backup,$target)) {
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($path)) -ne $parent) { throw 'Installation path escaped its intended parent.' }
}
Copy-Item -LiteralPath $source -Destination $staged -Recurse
foreach ($name in $required) {
    if ((Get-FileHash -LiteralPath (Join-Path $source $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $staged $name)).Hash) { throw "Copy verification failed: $name" }
}
$hadInstall = Test-Path -LiteralPath $target
if ($hadInstall) { Move-Item -LiteralPath $target -Destination $backup }
try { Move-Item -LiteralPath $staged -Destination $target }
catch {
    if ($hadInstall -and -not (Test-Path -LiteralPath $target)) { Move-Item -LiteralPath $backup -Destination $target }
    throw
}
# Saves live in LocalLow, outside both directories. Existing desktop shortcuts retain their target.
[pscustomobject]@{ Installation=$target; PreviousBuild=$(if($hadInstall){$backup}else{$null}); SourceCommit=$manifest.sourceCommit } | ConvertTo-Json
