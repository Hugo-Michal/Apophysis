param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [int]$TargetCount = 200
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$downloadDir = Join-Path $ProjectRoot 'research\web_fractals_2026-08-20'
$sourceListPath = Join-Path $downloadDir 'google_image_urls.json'
$userAgent = 'FractalFlameCurator/1.0 (local research corpus)'

if (-not (Test-Path -LiteralPath $sourceListPath)) {
    throw "Google source list not found: $sourceListPath"
}

Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Path $downloadDir -Force | Out-Null

$blockedHosts = @(
    'www.shutterstock.com',
    'thumbs.dreamstime.com',
    'c8.alamy.com',
    'media.sciencephoto.com',
    'i.etsystatic.com',
    'images.fineartamerica.com',
    'i.ytimg.com'
)
$entries = @(Get-Content -LiteralPath $sourceListPath -Raw | ConvertFrom-Json | Where-Object {
    try {
        $u = [uri]$_.url
        $u.Scheme -in @('http', 'https') -and
            $u.Host -notin $blockedHosts -and
            $u.AbsolutePath -match '\.(jpg|jpeg|png)$'
    }
    catch { $false }
})

function Get-SafeStem {
    param([string]$Url)
    $uri = [uri]$Url
    $stem = [System.IO.Path]::GetFileNameWithoutExtension([System.Uri]::UnescapeDataString($uri.AbsolutePath))
    $stem = $stem -replace '[<>:"/\\|?*]', '_'
    $stem = $stem -replace '\s+', '_'
    $stem = $stem -replace '[^\p{L}\p{N}_-]', '_'
    $stem = $stem.Trim('_')
    if ([string]::IsNullOrWhiteSpace($stem)) { $stem = 'fractal' }
    return $stem.Substring(0, [Math]::Min($stem.Length, 90))
}

function Get-ImageDimensions {
    param([string]$Path)
    $image = $null
    try {
        $image = [System.Drawing.Image]::FromFile($Path)
        return [pscustomobject]@{ Width = $image.Width; Height = $image.Height }
    }
    finally {
        if ($null -ne $image) { $image.Dispose() }
    }
}

$downloaded = 0
$failed = 0
$attempted = 0
$entryIndex = 0
foreach ($entry in $entries) {
    if ($downloaded -ge $TargetCount) { break }
    $entryIndex++
    $uri = [uri]$entry.url
    $extension = [System.IO.Path]::GetExtension($uri.AbsolutePath).ToLowerInvariant()
    $name = 'google_{0:D3}_{1}{2}' -f ($downloaded + 1), (Get-SafeStem -Url $entry.url), $extension
    $path = Join-Path $downloadDir $name
    $partPath = "$path.part"
    $attempted++
    $downloadedThis = $false

    for ($attempt = 1; $attempt -le 2; $attempt++) {
        try {
            if (Test-Path -LiteralPath $partPath) { Remove-Item -LiteralPath $partPath -Force }
            Invoke-WebRequest -Uri $entry.url -Headers @{ 'User-Agent' = $userAgent; 'Referer' = 'https://www.google.com/' } -OutFile $partPath -TimeoutSec 90 -MaximumRedirection 5
            $dimensions = Get-ImageDimensions -Path $partPath
            if ($dimensions.Width -lt 500 -or $dimensions.Height -lt 500) {
                throw "Downloaded image is only $($dimensions.Width)x$($dimensions.Height)."
            }
            Move-Item -LiteralPath $partPath -Destination $path -Force
            $downloadedThis = $true
            Start-Sleep -Milliseconds 800
            break
        }
        catch {
            if ($attempt -eq 2) {
                Write-Warning "Skipping $($entry.url): $($_.Exception.Message)"
            }
            else {
                Start-Sleep -Seconds 2
            }
        }
    }

    if ($downloadedThis) { $downloaded++ } else { $failed++ }
    Write-Progress -Activity 'Downloading Google-discovered fractals' -Status "$downloaded valid; $entryIndex / $($entries.Count) checked" -PercentComplete ([Math]::Min(100, ($downloaded / [Math]::Max(1, $TargetCount)) * 100))
}
Write-Progress -Activity 'Downloading Google-discovered fractals' -Completed

$summary = @(
    'Discovery: Google Image Search queries for Apophysis, Mandelbulber, Mandelbulb3D, JWildfire, Ultra Fractal, Chaotica, Mandelbrot/Julia, and high-resolution square fractal art.'
    'Source image URLs were extracted from rendered Google result pages; Google preview thumbnails were not used.'
    'Minimum validated dimensions: 500 x 500 pixels.'
    "Eligible source URLs: $($entries.Count)"
    "Checked: $attempted"
    "Downloaded and validated: $downloaded"
    "Skipped or failed: $failed"
    "Folder: $downloadDir"
)
[System.IO.File]::WriteAllLines((Join-Path $downloadDir 'README_google.txt'), $summary, [System.Text.UTF8Encoding]::new($false))
Write-Host ($summary -join [Environment]::NewLine)
