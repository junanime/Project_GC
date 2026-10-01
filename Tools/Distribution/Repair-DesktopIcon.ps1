$ErrorActionPreference = 'Stop'
$shell = New-Object -ComObject WScript.Shell
$desktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) '24시간의사투 (테스트).lnk'
if (!(Test-Path -LiteralPath $desktopLink)) { throw 'Game shortcut not found' }
$shortcut = $shell.CreateShortcut($desktopLink)
$target = $shortcut.TargetPath
if ([IO.Path]::GetFileName($target) -ne '24tu.exe' -or !(Test-Path -LiteralPath $target)) {
    throw 'Unexpected or missing game target; no shortcut was changed'
}
# Use the resolved shortcut target, not a reconstructed LOCALAPPDATA path.
# Packaged desktop hosts can redirect installation paths independently of IconLocation.
$installDirectory = Split-Path -Parent $target
$iconPath = Join-Path $installDirectory 'HyukiActive.ico'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'HyukiActive.ico') -Destination $iconPath
$links = @($desktopLink, (Join-Path ([Environment]::GetFolderPath('Programs')) '24시간의사투 (테스트)/24시간의사투.lnk'))
foreach ($link in $links) {
    if (!(Test-Path -LiteralPath $link)) { continue }
    $entry = $shell.CreateShortcut($link)
    if ([IO.Path]::GetFileName($entry.TargetPath) -ne '24tu.exe' -or
        !(Test-Path -LiteralPath $entry.TargetPath) -or
        (Get-FileHash -LiteralPath $entry.TargetPath).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
        throw "Unexpected shortcut target: $link"
    }
    $entry.TargetPath = $target
    $entry.WorkingDirectory = $installDirectory
    $entry.IconLocation = "$iconPath,0"
    $entry.Save()
}
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class DesktopIconNotification {
    [DllImport("shell32.dll", CharSet=CharSet.Unicode)]
    public static extern void SHChangeNotify(uint change, uint flags, string path, IntPtr other);
}
'@
foreach ($link in $links) {
    if (Test-Path -LiteralPath $link) { [DesktopIconNotification]::SHChangeNotify(0x2000, 0x1005, $link, [IntPtr]::Zero) }
}
$verified = $shell.CreateShortcut($desktopLink)
if ($verified.IconLocation -ne "$iconPath,0") { throw 'Icon reference did not persist' }
Write-Output "PASS: desktop and Start Menu icon now reference $iconPath"
