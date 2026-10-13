param([int]$Port = 18724, [switch]$NoBrowser)
$ErrorActionPreference = 'Stop'
$webRoot = [IO.Path]::GetFullPath($PSScriptRoot)
if (-not (Test-Path -LiteralPath (Join-Path $webRoot 'index.html'))) { throw 'Extract the full web demo ZIP before running this file.' }
$webRootPrefix = $webRoot.TrimEnd('\') + '\'
$listener = New-Object Net.HttpListener
$url = "http://localhost:$Port/"
$listener.Prefixes.Add($url)
$types = @{'.html'='text/html; charset=utf-8';'.js'='application/javascript';'.wasm'='application/wasm';'.json'='application/json';'.png'='image/png';'.jpg'='image/jpeg';'.css'='text/css';'.ico'='image/x-icon';'.txt'='text/plain; charset=utf-8'}
try {
    $listener.Start()
    Write-Host "24tu browser demo: $url"
    Write-Host 'Keep this window open while playing. Close it to stop the local server.'
    if (-not $NoBrowser) { Start-Process $url }
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        $response = $context.Response
        try {
            if ($context.Request.HttpMethod -notin @('GET','HEAD')) { $response.StatusCode=405; continue }
            $relative = [Uri]::UnescapeDataString($context.Request.Url.AbsolutePath).TrimStart('/')
            if (-not $relative) { $relative = 'index.html' }
            $file = [IO.Path]::GetFullPath((Join-Path $webRoot $relative))
            if (-not $file.StartsWith($webRootPrefix,[StringComparison]::OrdinalIgnoreCase) -or $relative.Contains(':')) { $response.StatusCode=403; continue }
            if (-not [IO.File]::Exists($file)) { $response.StatusCode=404; continue }
            $extension = [IO.Path]::GetExtension($file).ToLowerInvariant()
            $response.ContentType = if ($types.ContainsKey($extension)) { $types[$extension] } else { 'application/octet-stream' }
            $response.Headers['Cache-Control'] = 'no-cache'
            $stream = [IO.File]::OpenRead($file)
            try {
                $response.ContentLength64 = $stream.Length
                if ($context.Request.HttpMethod -eq 'GET') { $stream.CopyTo($response.OutputStream) }
            } finally { $stream.Dispose() }
        } catch {
            Write-Warning $_.Exception.Message
        } finally { $response.Close() }
    }
} finally { $listener.Close() }
