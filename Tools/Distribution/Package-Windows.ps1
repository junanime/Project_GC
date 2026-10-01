param(
    [Parameter(Mandatory = $true)][string]$Compiler,
    [string]$Payload,
    [string]$Output,
    [string]$Version = '0.1.20260929.3',
    [switch]$FastCompression
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$Payload) { $Payload = Join-Path $repoRoot 'Builds/WindowsDesktop' }
if (!$Output) { $Output = Join-Path $repoRoot "Builds/Installers/24tu-Setup-$Version.exe" }
$Payload = (Resolve-Path -LiteralPath $Payload).Path
$Compiler = (Resolve-Path -LiteralPath $Compiler).Path
$Output = [IO.Path]::GetFullPath($Output)
foreach ($required in @('24tu.exe', 'UnityPlayer.dll', '24tu_Data', 'desktop-build.json')) {
    if (!(Test-Path -LiteralPath (Join-Path $Payload $required))) { throw "Missing player payload: $required" }
}
if (Test-Path -LiteralPath $Output) { throw "Output already exists; choose a new version or output path: $Output" }
$outDirectory = Split-Path -Parent $Output
New-Item -ItemType Directory -Force -Path $outDirectory | Out-Null
$uninstallList = Join-Path $outDirectory 'uninstall-files.nsh'
$payloadFiles = @(Get-ChildItem -LiteralPath $Payload -Recurse -File | Where-Object {
    $_.FullName -notmatch '[\\/][^\\/]*_DoNotShip([\\/]|$)' -and $_.Extension -notin @('.pdb', '.mdb')
})
$lines = [Collections.Generic.List[string]]::new()
foreach ($file in $payloadFiles) {
    $relative = $file.FullName.Substring($Payload.Length + 1).Replace('$', '$$')
    $lines.Add('Delete "$INSTDIR\' + $relative + '"')
}
foreach ($dir in (Get-ChildItem -LiteralPath $Payload -Recurse -Directory | Where-Object {
    $_.FullName -notmatch '[\\/][^\\/]*_DoNotShip([\\/]|$)'
} | Sort-Object { $_.FullName.Length } -Descending)) {
    $relative = $dir.FullName.Substring($Payload.Length + 1).Replace('$', '$$')
    $lines.Add('RMDir "$INSTDIR\' + $relative + '"')
}
[IO.File]::WriteAllLines($uninstallList, $lines, [Text.UTF8Encoding]::new($true))
$compilerOptions = @('/V3', '/INPUTCHARSET', 'UTF8', "/DPAYLOAD=$Payload", "/DOUTPUT=$Output", "/DUNINSTALL_LIST=$uninstallList", "/DBUILD_VERSION=$Version")
if ($FastCompression) { $compilerOptions += '/DFAST_PACKAGE' }
& $Compiler @compilerOptions (Join-Path $PSScriptRoot '24tu.nsi')
if ($LASTEXITCODE -ne 0) { throw "Installer compiler failed with exit code $LASTEXITCODE" }
$hash = Get-FileHash -LiteralPath $Output -Algorithm SHA256
[IO.File]::WriteAllText("$Output.sha256", "$($hash.Hash)  $([IO.Path]::GetFileName($Output))`r`n")
Write-Output "Installer: $Output"
Write-Output "SHA256: $($hash.Hash)"
