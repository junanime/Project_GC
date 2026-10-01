param(
    [ValidateSet('UploadDraft','Publish','Inspect')][string]$Mode = 'Inspect',
    [string]$Commit,
    [string]$Version = '0.1.20260929.3',
    [string]$Installer,
    [string]$Notes
)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$remote = git -C $repoRoot remote get-url origin
if ($remote -ne 'https://github.com/junanime/Project_GC.git') { throw 'Unexpected repository; refusing release operation' }
if (!$Installer) { $Installer = Join-Path $repoRoot "Builds/Installers/24tu-Setup-$Version.exe" }
if (!$Notes) { $Notes = Join-Path $PSScriptRoot "ReleaseNotes-$Version.md" }
$env:GIT_TERMINAL_PROMPT = '0'
$env:GCM_INTERACTIVE = 'never'
$credentialLines = "protocol=https`nhost=github.com`n`n" | git credential fill
if ($LASTEXITCODE -ne 0) { throw 'Existing GitHub credential unavailable' }
$credential = @{}
foreach ($line in $credentialLines) {
    $pair = $line.Split('=', 2)
    if ($pair.Length -eq 2) { $credential[$pair[0]] = $pair[1] }
}
if (!$credential.password) { throw 'No existing GitHub credential available' }
$headers = @{ Authorization = 'Bearer ' + $credential.password; Accept = 'application/vnd.github+json'; 'User-Agent' = '24tu-release-tools'; 'X-GitHub-Api-Version' = '2022-11-28' }
$baseUri = 'https://api.github.com/repos/junanime/Project_GC'
try {
    $releases = @(Invoke-RestMethod -Uri "$baseUri/releases?per_page=100" -Headers $headers)
    $release = $releases | Where-Object tag_name -eq "v$Version" | Select-Object -First 1
    if ($Mode -eq 'Inspect') {
        $release | Select-Object id,tag_name,draft,prerelease,html_url,assets | ConvertTo-Json -Depth 6
        return
    }
    if ($Mode -eq 'UploadDraft') {
        if ($Commit -notmatch '^[0-9a-f]{40}$') { throw 'Provide an exact pushed source commit SHA' }
        foreach ($path in @($Installer, "$Installer.sha256", $Notes)) {
            if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing artifact: $path" }
        }
        $expectedHash = (Get-FileHash -LiteralPath $Installer -Algorithm SHA256).Hash.ToLowerInvariant()
        if ((Get-Content -LiteralPath "$Installer.sha256" -Raw).Split(' ')[0].ToLowerInvariant() -ne $expectedHash) { throw 'Installer checksum file mismatch' }
        if (!$release) {
            $body = @{tag_name="v$Version"; target_commitish=$Commit; name="24시간의사투 · Windows 테스트 v$Version"; body=(Get-Content -LiteralPath $Notes -Raw); draft=$true; prerelease=$true} | ConvertTo-Json
            $release = Invoke-RestMethod -Method Post -Uri "$baseUri/releases" -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
        }
        if (!$release.draft -or $release.target_commitish -ne $Commit) { throw 'Existing release is not the expected draft; no overwrite performed' }
        foreach ($path in @($Installer, "$Installer.sha256")) {
            $file = Get-Item -LiteralPath $path
            $localDigest = 'sha256:' + (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant()
            $asset = @($release.assets) | Where-Object name -eq $file.Name | Select-Object -First 1
            if (!$asset) {
                $uploadBase = $release.upload_url -replace '\{.*$', ''
                if (([Uri]$uploadBase).Host -ne 'uploads.github.com') { throw 'Unexpected asset upload host' }
                Write-Output "Uploading $($file.Name) ($($file.Length) bytes) to draft release..."
                $asset = Invoke-RestMethod -Method Post -Uri ($uploadBase + '?name=' + [Uri]::EscapeDataString($file.Name)) -Headers $headers -ContentType 'application/octet-stream' -InFile $file.FullName -TimeoutSec 1800
            }
            if ($asset.state -ne 'uploaded' -or $asset.size -ne $file.Length -or $asset.digest -ne $localDigest) { throw "Remote asset verification failed: $($file.Name)" }
            Write-Output "Verified upload: $($asset.name) / $($asset.digest)"
        }
        Write-Output "Draft ready: $($release.html_url)"
        return
    }
    if (!$release -or !$release.draft) { throw 'Expected an existing verified draft release' }
    foreach ($path in @($Installer, "$Installer.sha256")) {
        $file = Get-Item -LiteralPath $path
        $asset = @($release.assets) | Where-Object name -eq $file.Name | Select-Object -First 1
        if (!$asset -or $asset.state -ne 'uploaded' -or $asset.size -ne $file.Length -or $asset.digest -ne ('sha256:' + (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant())) { throw 'Release assets do not match local validated files' }
    }
    $published = Invoke-RestMethod -Method Patch -Uri "$baseUri/releases/$($release.id)" -Headers $headers -ContentType 'application/json' -Body '{"draft":false,"prerelease":true}'
    Write-Output "Published: $($published.html_url)"
}
finally {
    $credential.Clear(); $headers.Clear(); $credentialLines = $null
}
