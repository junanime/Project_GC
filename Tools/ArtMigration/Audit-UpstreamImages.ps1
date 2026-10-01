param([string]$Base = '01f8c76')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $root
try {
    $imagePattern = '\.(png|jpe?g|tga|psd|gif|bmp|svg|tiff?|exr|hdr|dds|ico)$'
    $baseRows = @(git -c core.quotepath=false ls-tree -r $Base | ForEach-Object {
        if ($_ -match '^\d+ blob ([0-9a-f]+)\t(.+)$' -and $Matches[2] -match $imagePattern) {
            $parts = $_ -split "`t", 2
            $hash = ($parts[0] -split ' ')[2]
            $path = $parts[1]
            $meta = (git show "${Base}:$path.meta" 2>$null) -join "`n"
            $guid = if ($meta -match '(?m)^guid: ([0-9a-f]{32})') { $Matches[1] } else { '' }
            [pscustomobject]@{ path=$path; blob=$hash; guid=$guid }
        }
    })
    $files = @(Get-ChildItem Assets -Recurse -File | Where-Object { $_.Name -match $imagePattern } | Sort-Object FullName)
    $paths = @($files | ForEach-Object { $_.FullName.Substring($root.Length + 1).Replace('\','/') })
    $hashes = @($paths | git hash-object --stdin-paths)
    if ($hashes.Count -ne $paths.Count) { throw 'Image hash count mismatch' }
    $results = @(for ($i=0; $i -lt $paths.Count; $i++) {
        $path = $paths[$i]
        $metaPath = "$path.meta"
        $meta = if (Test-Path -LiteralPath $metaPath) { Get-Content -LiteralPath $metaPath -Raw } else { '' }
        $guid = if ($meta -match '(?m)^guid: ([0-9a-f]{32})') { $Matches[1] } else { '' }
        $original = @($baseRows | Where-Object { $_.path -eq $path -or $_.blob -eq $hashes[$i] -or ($guid -and $_.guid -eq $guid) })
        if ($original.Count) {
            $refs = @(if ($guid) { rg -l --fixed-strings $guid Assets ProjectSettings -g '!*.meta' -g '!*.png' -g '!*.jpg' -g '!*.jpeg' })
            [pscustomobject]@{
                path=$path; guid=$guid; blob=$hashes[$i]; originalPaths=@($original.path)
                unchanged=($hashes[$i] -in $original.blob); references=$refs
                spriteMode=if ($meta -match '(?m)^  spriteMode: (\d+)') { [int]$Matches[1] } else { 0 }
            }
        }
    })
    $outputDirectory = Join-Path $root 'Logs/ArtMigration'
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
    $report = [pscustomobject]@{base=$Base; upstreamImages=$baseRows; candidates=$results; currentImageCount=$files.Count}
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outputDirectory 'upstream-image-audit.json') -Encoding utf8
    $results | Select-Object path,unchanged,spriteMode,@{n='refs';e={$_.references.Count}} | Format-Table -AutoSize
    Write-Output "Upstream images: $($baseRows.Count); candidates: $($results.Count); unchanged: $(@($results | Where-Object unchanged).Count); modified: $(@($results | Where-Object { !$_.unchanged }).Count)"
} finally { Pop-Location }
