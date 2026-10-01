param(
    [string]$Payload,
    [string]$Installed
)
$ErrorActionPreference = 'Stop'
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) '24시간의사투 (테스트).lnk'
if (!(Test-Path -LiteralPath $shortcutPath)) { throw 'Missing desktop shortcut' }
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
if (!$Installed) { $Installed = Split-Path -Parent $shortcut.TargetPath }
if (!$Payload) { $Payload = Join-Path $PSScriptRoot '../../Builds/WindowsDesktop' }
$Payload = (Resolve-Path -LiteralPath $Payload).Path
$Installed = (Resolve-Path -LiteralPath $Installed).Path
$files = @(Get-ChildItem -LiteralPath $Payload -File -Recurse | Where-Object {
    $_.FullName -notmatch '[\\/][^\\/]*_DoNotShip([\\/]|$)' -and $_.Extension -notin @('.pdb', '.mdb')
})
foreach ($file in $files) {
    $relative = $file.FullName.Substring($Payload.Length + 1)
    $target = Join-Path $Installed $relative
    if (!(Test-Path -LiteralPath $target -PathType Leaf)) { throw "Missing installed file: $relative" }
    if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
        throw "Installed file differs: $relative"
    }
}
if ($shortcut.TargetPath -ne (Join-Path $Installed '24tu.exe')) { throw 'Wrong desktop shortcut target' }
if ($shortcut.WorkingDirectory -ne $Installed) { throw 'Wrong shortcut working directory' }
$registration = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\24tuDesktopTest'
$registeredExe = Join-Path $registration.InstallLocation '24tu.exe'
if (!(Test-Path -LiteralPath $registeredExe) -or (Get-FileHash -LiteralPath $registeredExe).Hash -ne (Get-FileHash -LiteralPath $shortcut.TargetPath).Hash) { throw 'Wrong uninstall registration' }
$iconPath = Join-Path $Installed 'HyukiActive.ico'
if ($shortcut.IconLocation -ne "$iconPath,0") { throw 'Desktop shortcut must explicitly reference the installed icon' }
if (!(Test-Path -LiteralPath $iconPath)) { throw 'Shortcut icon path is missing' }
if ((Get-FileHash -LiteralPath $iconPath).Hash -ne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'HyukiActive.ico')).Hash) { throw 'Installed icon differs from the source icon' }
$menuLink = $shell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('Programs')) '24시간의사투 (테스트)/24시간의사투.lnk'))
if ($menuLink.TargetPath -ne $shortcut.TargetPath -or $menuLink.IconLocation -ne $shortcut.IconLocation) { throw 'Start Menu shortcut differs from desktop shortcut' }
Write-Output "PASS: $($files.Count) installed files match SHA-256; desktop shortcut and uninstall registration are correct."
