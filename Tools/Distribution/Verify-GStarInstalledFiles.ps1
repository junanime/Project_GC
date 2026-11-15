param(
    [Parameter(Mandatory = $true)][string]$Installed,
    [Parameter(Mandatory = $true)][string]$ExpectedCommit
)
$ErrorActionPreference = 'Stop'
$Installed = (Resolve-Path -LiteralPath $Installed).Path.TrimEnd('\')
$manifest = @(Get-Content -LiteralPath (Join-Path $Installed 'installed-files.sha256.json') -Raw | ConvertFrom-Json)
foreach ($file in $manifest) {
    $target = [IO.Path]::GetFullPath((Join-Path $Installed $file.path))
    if (!$target.StartsWith($Installed + '\', [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe manifest path: $($file.path)" }
    if (!(Test-Path -LiteralPath $target -PathType Leaf)) { throw "Missing installed file: $($file.path)" }
    if ((Get-Item -LiteralPath $target).Length -ne $file.bytes -or (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $file.sha256) { throw "Installed file differs: $($file.path)" }
}
$release = Get-Content -LiteralPath (Join-Path $Installed 'gstar-release.json') -Raw | ConvertFrom-Json
if (!$release.commit.StartsWith($ExpectedCommit) -or $release.branch -ne 'gstar/2026-demo') { throw 'Wrong source revision.' }
if ($release.surveyUrl -ne 'https://forms.gle/9aijHzJz66P8tr4Y9' -or $release.homepageUrl -ne 'https://24hours-gstar.pages.dev/') { throw 'Wrong QR release URLs.' }
$shell = New-Object -ComObject WScript.Shell
$appName = '24시간의 사투 지스타2026'
foreach ($path in @((Join-Path ([Environment]::GetFolderPath('Desktop')) ($appName + '.lnk')), (Join-Path ([Environment]::GetFolderPath('Programs')) ($appName + '/' + $appName + '.lnk')))) {
    if (!(Test-Path -LiteralPath $path)) { throw "Missing shortcut: $path" }
    $shortcut = $shell.CreateShortcut($path)
    if ($shortcut.TargetPath -ne (Join-Path $Installed '24tu-GStar2026.exe')) { throw "Wrong shortcut target: $path" }
    if ($shortcut.WorkingDirectory -ne $Installed) { throw "Wrong shortcut working directory: $path" }
    if ($shortcut.IconLocation -ne "$(Join-Path $Installed 'GStar2026.ico'),0") { throw "Wrong shortcut icon: $path" }
}
$registration = Get-ItemProperty -LiteralPath 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall/24tuGStar2026'
if ($registration.InstallLocation -ne $Installed -or $registration.DisplayVersion -ne $release.version) { throw 'Wrong uninstall registration.' }
if (!(Test-Path -LiteralPath (Join-Path $Installed 'Uninstall.exe') -PathType Leaf)) { throw 'Uninstaller missing.' }
Write-Output "PASS: $($manifest.Count) installed files match SHA-256; source commit $($release.commit); desktop/start shortcuts and uninstall registration match."
