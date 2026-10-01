$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location $root
try {
    $manifest = Get-Content Documentation/UpstreamImageMigration.json -Raw | ConvertFrom-Json
    $imagePattern = '\.(png|jpe?g|tga|psd|gif|bmp|svg|tiff?|exr|hdr|dds|ico)$'
    $files = @(Get-ChildItem Assets -Recurse -File | Where-Object { $_.Name -match $imagePattern } | Sort-Object FullName)
    $paths = @($files | ForEach-Object { $_.FullName.Substring($root.Length + 1).Replace('\','/') })
    $hashes = @($paths | git hash-object --stdin-paths)
    if ($hashes.Count -ne $paths.Count) { throw 'Hash count mismatch' }
    $before = @{}
    git -c core.quotepath=false ls-tree -r HEAD | ForEach-Object {
        if ($_ -match '^\d+ blob ([0-9a-f]+)\t(.+)$') { $before[$Matches[2]]=$Matches[1] }
    }
    foreach ($e in $manifest.entries) {
        if (Test-Path -LiteralPath $e.originalPath) { throw "Original path remains: $($e.originalPath)" }
        if (Test-Path -LiteralPath ($e.originalPath + '.meta')) { throw "Original metadata path remains: $($e.originalPath)" }
        if ($e.originalBlob -in $hashes) { throw "Original binary remains under another name: $($e.originalPath)" }
        $meta = Get-Content -LiteralPath ($e.replacementPath + '.meta') -Raw
        if ($meta -notmatch "(?m)^guid: $($e.guid)\r?$") { throw "Replacement GUID mismatch: $($e.replacementPath)" }
    }
    $protected = 0
    for ($i=0; $i -lt $paths.Count; $i++) {
        if ($paths[$i] -in $manifest.entries.replacementPath) { continue }
        if ($before.ContainsKey($paths[$i])) {
            if ($hashes[$i] -ne $before[$paths[$i]]) { throw "Non-upstream art changed: $($paths[$i])" }
            $protected++
        }
    }
    foreach ($path in $before.Keys) {
        if ($path -match $imagePattern -and $path.StartsWith('Assets/') -and $path -notin $manifest.entries.originalPath -and !(Test-Path -LiteralPath $path)) {
            throw "Non-upstream image removed: $path"
        }
    }
    Write-Output "PASS: $($manifest.entries.Count) original image paths/binaries removed; replacement GUIDs preserved; $protected other images unchanged."

    Add-Type -AssemblyName System.Drawing
    $cols=8; $cellW=150; $cellH=136; $rows=[int][Math]::Ceiling($manifest.entries.Count/$cols)
    $sheet=[Drawing.Bitmap]::new($cols*$cellW,$rows*$cellH)
    $graphics=[Drawing.Graphics]::FromImage($sheet)
    $font=[Drawing.Font]::new('Arial',8)
    try {
        $graphics.Clear([Drawing.Color]::FromArgb(34,39,48))
        $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        for ($i=0;$i -lt $manifest.entries.Count;$i++) {
            $e=$manifest.entries[$i]; $left=($i%$cols)*$cellW; $top=[int][Math]::Floor($i/$cols)*$cellH
            $source=[Drawing.Image]::FromFile((Join-Path $root $e.replacementPath))
            try {
                $crop=[Drawing.RectangleF]::new(0,0,$source.Width,$source.Height)
                if ($e.sprites.Count -gt 1) {
                    $r=$e.sprites[0].rect
                    $crop=[Drawing.RectangleF]::new($r.x,$source.Height-$r.y-$r.height,$r.width,$r.height)
                }
                $scale=[Math]::Min(96/$crop.Width,96/$crop.Height)
                $dest=[Drawing.RectangleF]::new($left+($cellW-$crop.Width*$scale)/2,$top+6,$crop.Width*$scale,$crop.Height*$scale)
                $graphics.DrawImage($source,$dest,$crop,[Drawing.GraphicsUnit]::Pixel)
                $graphics.DrawString([IO.Path]::GetFileNameWithoutExtension($e.originalPath),$font,[Drawing.Brushes]::White,[single]($left+5),[single]($top+108))
            } finally { $source.Dispose() }
        }
        $preview=Join-Path $root 'Logs/ArtMigration/placeholder-contact-sheet.png'
        $sheet.Save($preview,[Drawing.Imaging.ImageFormat]::Png)
        Write-Output "Preview: $preview"
    } finally { $font.Dispose(); $graphics.Dispose(); $sheet.Dispose() }
} finally { Pop-Location }
