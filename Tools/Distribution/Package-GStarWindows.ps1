param(
    [Parameter(Mandatory = $true)][string]$Compiler,
    [string]$Payload,
    [Parameter(Mandatory = $true)][string]$Output,
    [string]$Version = '2026.11.16.1',
    [Parameter(Mandatory = $true)][string]$Commit,
    [switch]$FastPackage
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$Payload) { $Payload = Join-Path $repoRoot 'Builds/GStar2026-Windows' }
$Payload = (Resolve-Path -LiteralPath $Payload).Path.TrimEnd('\')
$Compiler = (Resolve-Path -LiteralPath $Compiler).Path
$Output = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $Output) { throw "Output already exists: $Output" }
foreach ($required in @('24tu-GStar2026.exe', 'UnityPlayer.dll', 'UnityCrashHandler64.exe', '24tu-GStar2026_Data/globalgamemanagers', 'MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $Payload $required) -PathType Leaf)) { throw "Missing runtime payload: $required" }
}
if ($Commit -notmatch '^[0-9a-f]{7,40}$') { throw 'Commit must be a hexadecimal Git revision.' }
$head = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or !$head.StartsWith($Commit)) { throw 'Build commit does not match checkout HEAD.' }
$outDirectory = Split-Path -Parent $Output
$support = Join-Path $outDirectory ('package-support-' + [IO.Path]::GetFileNameWithoutExtension($Output))
New-Item -ItemType Directory -Force -Path $support | Out-Null
$runtimeFiles = @(Get-ChildItem -LiteralPath $Payload -Recurse -File | Where-Object {
    $relative = $_.FullName.Substring($Payload.Length + 1)
    $runtime = $relative -match '^(24tu-GStar2026_Data|MonoBleedingEdge)[\\/]' -or $relative -in @('24tu-GStar2026.exe', 'UnityPlayer.dll', 'UnityCrashHandler64.exe')
    $runtime -and $relative -notmatch '(^|[\\/])[^\\/]*_DoNotShip([\\/]|$)' -and $_.Extension -notin @('.pdb', '.mdb', '.log', '.bak')
} | Sort-Object FullName)
if ($runtimeFiles.Count -lt 20) { throw 'Unexpectedly small runtime payload.' }

$readme = @'
24시간의 사투 · 지스타 2026 체험판

설치가 끝나면 바탕화면의 「24시간의 사투 지스타2026」을 실행하세요.
Unity나 Steam을 별도로 설치할 필요가 없습니다.
게임에 필요한 파일은 설치 프로그램 하나에 모두 들어 있습니다.

첫 도전은 출전준비에서 캐릭터와 장비를 고른 뒤 조작과 아이템 사용을 연습하고 구강에서 시작합니다.
다음 도전에서는 스테이지를 선택합니다. 한 참가자당 도전 기회는 2회입니다. 같은 스테이지도 다시 도전할 수 있습니다.

운영자 단축키
  F9 : 체험 종료 화면에서 다음 참가자 준비
  F8 : QA 자료 내보내기

종료 화면의 큰 QR은 설문, 작은 QR은 게임 홈페이지입니다.
QR을 휴대폰으로 스캔하면 됩니다. 설문은 로그인 없이 참여할 수 있습니다.
설문: https://forms.gle/9aijHzJz66P8tr4Y9
홈페이지: https://24hours-gstar.pages.dev/

랭킹과 플레이 자료는 설치 폴더가 아닌 해당 Windows 계정의 아래 위치에 보관됩니다.
%USERPROFILE%\AppData\LocalLow\DefaultCompany\24시간의 사투 지스타2026\GStar2026
게임을 제거하거나 다시 설치해도 이 기록은 자동으로 삭제되지 않습니다.
행사 종료 후에는 이 폴더 전체를 운영용 저장 장치에 백업해 주세요.

자세한 운영 방법은 같은 폴더의 「지스타-운영안내.md」를 참고하세요.
'@
[IO.File]::WriteAllText((Join-Path $support '먼저 읽어주세요.txt'), $readme, [Text.UTF8Encoding]::new($true))
Copy-Item -LiteralPath (Join-Path $repoRoot 'Docs/GStar2026-Guide.md') -Destination (Join-Path $support '지스타-운영안내.md')
$notices = [ordered]@{
    'LICENSE.txt' = 'LICENSE'
    'ThirdPartyNotices.md' = 'Tools/Distribution/ThirdPartyNotices.md'
    'Steamworks.NET-LICENSE.txt' = 'Library/PackageCache/com.rlabrecque.steamworks.net@c21a8f0e31/LICENSE.md'
    'LiberationSans-OFL.txt' = 'Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt'
}
foreach ($name in $notices.Keys) { Copy-Item -LiteralPath (Join-Path $repoRoot $notices[$name]) -Destination (Join-Path $support $name) }
$release = [ordered]@{
    product = '24시간의 사투 지스타2026'; version = $Version; branch = 'gstar/2026-demo'; commit = $head
    packagedUtc = [DateTime]::UtcNow.ToString('o'); surveyUrl = 'https://forms.gle/9aijHzJz66P8tr4Y9'; homepageUrl = 'https://24hours-gstar.pages.dev/'
}
[IO.File]::WriteAllText((Join-Path $support 'gstar-release.json'), ($release | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
$packFiles = [Collections.Generic.List[object]]::new()
foreach ($file in $runtimeFiles) { $packFiles.Add([pscustomobject]@{ Relative = $file.FullName.Substring($Payload.Length + 1); Source = $file.FullName }) }
foreach ($name in (@('먼저 읽어주세요.txt', '지스타-운영안내.md', 'gstar-release.json') + @($notices.Keys))) { $packFiles.Add([pscustomobject]@{ Relative = $name; Source = (Join-Path $support $name) }) }
$packFiles.Add([pscustomobject]@{ Relative = 'GStar2026.ico'; Source = (Join-Path $PSScriptRoot 'HyukiActive.ico') })
$manifest = @($packFiles | ForEach-Object { [ordered]@{ path = $_.Relative.Replace('\','/'); bytes = (Get-Item -LiteralPath $_.Source).Length; sha256 = (Get-FileHash -LiteralPath $_.Source -Algorithm SHA256).Hash.ToLowerInvariant() } })
[IO.File]::WriteAllText((Join-Path $support 'installed-files.sha256.json'), (ConvertTo-Json -InputObject $manifest -Depth 5), [Text.UTF8Encoding]::new($false))
$packFiles.Add([pscustomobject]@{ Relative = 'installed-files.sha256.json'; Source = (Join-Path $support 'installed-files.sha256.json') })
function Quote-Nsis([string]$value) { return $value.Replace('$', '$$') }
$install = [Collections.Generic.List[string]]::new()
$uninstall = [Collections.Generic.List[string]]::new()
$dirs = [Collections.Generic.HashSet[string]]::new()
$lastDirectory = $null
foreach ($file in $packFiles) {
    $relative = $file.Relative.Replace('/', '\')
    $directory = [IO.Path]::GetDirectoryName($relative)
    if ($directory -ne $lastDirectory) {
        $suffix = if ($directory) { '\' + (Quote-Nsis $directory) } else { '' }
        $install.Add('SetOutPath "$INSTDIR' + $suffix + '"')
        $lastDirectory = $directory
    }
    $install.Add('File "/oname=' + (Quote-Nsis ([IO.Path]::GetFileName($relative))) + '" "' + (Quote-Nsis $file.Source) + '"')
    $uninstall.Add('Delete "$INSTDIR\' + (Quote-Nsis $relative) + '"')
    while ($directory) { [void]$dirs.Add($directory); $directory = [IO.Path]::GetDirectoryName($directory) }
}
foreach ($directory in ($dirs | Sort-Object Length -Descending)) { $uninstall.Add('RMDir "$INSTDIR\' + (Quote-Nsis $directory) + '"') }
$installPath = Join-Path $support 'install-files.nsh'
$uninstallPath = Join-Path $support 'uninstall-files.nsh'
[IO.File]::WriteAllLines($installPath, $install, [Text.UTF8Encoding]::new($true))
[IO.File]::WriteAllLines($uninstallPath, $uninstall, [Text.UTF8Encoding]::new($true))
$runtimeBytes = [long](($manifest | ForEach-Object { [long]$_['bytes'] } | Measure-Object -Sum).Sum)
if ($runtimeBytes -le 0) { throw 'Packaged runtime byte count must be positive.' }
$sizeKb = [long][Math]::Ceiling($runtimeBytes / 1024)
$compilerOptions = @('/V3', '/INPUTCHARSET', 'UTF8', "/DOUTPUT=$Output", "/DINSTALL_LIST=$installPath", "/DUNINSTALL_LIST=$uninstallPath", "/DBUILD_VERSION=$Version", "/DESTIMATED_SIZE=$sizeKb")
if ($FastPackage) { $compilerOptions += '/DFAST_PACKAGE' }
# NSIS uses this process's temporary path, never the system-wide user setting.
$oldTemp = $env:TEMP; $oldTmp = $env:TMP
try {
    $env:TEMP = $support; $env:TMP = $support
    & $Compiler @compilerOptions (Join-Path $PSScriptRoot 'GStar2026.nsi')
    if ($LASTEXITCODE -ne 0) { throw "Installer compiler failed with exit code $LASTEXITCODE" }
} finally { $env:TEMP = $oldTemp; $env:TMP = $oldTmp }
$hash = (Get-FileHash -LiteralPath $Output -Algorithm SHA256).Hash
[IO.File]::WriteAllText("$Output.sha256", "$hash  $([IO.Path]::GetFileName($Output))`r`n")
Write-Output "Installer: $Output"
Write-Output "Packaged files: $($packFiles.Count); runtime bytes: $runtimeBytes"
Write-Output "SHA256: $hash"
Write-Output "Manifest: $(Join-Path $support 'installed-files.sha256.json')"
