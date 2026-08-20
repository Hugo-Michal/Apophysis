param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [int]$StartIndex = 300,
    [int]$TargetCount = 50
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$downloadDir = Join-Path $ProjectRoot 'research\web_fractals_2026-08-20'
$sourceListPath = Join-Path $downloadDir 'google_image_urls.json'
$userAgent = 'FractalFlameCurator/1.0 (local research corpus)'
$excludedHosts = @('images-wixmp-ed30a86b8c4ca887773594c2.wixmp.com', 'preview.redd.it', 'en.wikipedia.org', 'www.wikipedia.org', 'i.ytimg.com')

Add-Type -AssemblyName System.Drawing
Get-ChildItem -LiteralPath $downloadDir -File -Filter '*.part' -ErrorAction SilentlyContinue | ForEach-Object {
    Remove-Item -LiteralPath $_.FullName -Force
}
$entries = @(Get-Content -LiteralPath $sourceListPath -Raw | ConvertFrom-Json | Select-Object -Skip $StartIndex | Where-Object {
    try {
        $u = [uri]$_.url
        $u.Scheme -in @('http', 'https') -and
            $u.Host -notin $excludedHosts -and
            $u.AbsolutePath -notmatch '/236x/' -and
            $u.AbsolutePath -notmatch 'webp' -and
            $u.AbsolutePath -match '\.(jpg|jpeg|png)$'
    }
    catch { $false }
})

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
$checked = 0
foreach ($entry in $entries) {
    if ($downloaded -ge $TargetCount) { break }
    $checked++
    $uri = [uri]$entry.url
    $extension = [System.IO.Path]::GetExtension($uri.AbsolutePath).ToLowerInvariant()
    $path = Join-Path $downloadDir ('google_extra_{0:D3}{1}' -f ($downloaded + 1), $extension)
    $partPath = "$path.part"
    try {
        Invoke-WebRequest -Uri $entry.url -Headers @{ 'User-Agent' = $userAgent; 'Referer' = 'https://www.google.com/' } -OutFile $partPath -TimeoutSec 20 -MaximumRedirection 4
        $dimensions = Get-ImageDimensions -Path $partPath
        if ($dimensions.Width -ge 500 -and $dimensions.Height -ge 500) {
            Move-Item -LiteralPath $partPath -Destination $path -Force
            $downloaded++
            Start-Sleep -Milliseconds 500
        }
        else {
            Remove-Item -LiteralPath $partPath -Force
        }
    }
    catch {
        if (Test-Path -LiteralPath $partPath) { Remove-Item -LiteralPath $partPath -Force }
    }
    Write-Progress -Activity 'Topping up Google-discovered fractals' -Status "$downloaded valid; $checked checked" -PercentComplete ([Math]::Min(100, ($downloaded / [Math]::Max(1, $TargetCount)) * 100))
}
Write-Progress -Activity 'Topping up Google-discovered fractals' -Completed
Write-Host "Top-up downloaded $downloaded validated images after checking $checked later Google sources."
