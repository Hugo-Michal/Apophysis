param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [int]$TargetCount = 200
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$downloadDir = Join-Path $ProjectRoot 'research\web_fractals_2026-08-20'
$userAgent = 'FractalFlameCurator/1.0 (local research corpus)'
$apiBase = 'https://commons.wikimedia.org/w/api.php'

New-Item -ItemType Directory -Path $downloadDir -Force | Out-Null
Add-Type -AssemblyName System.Drawing

$categorySpecs = @(
    [pscustomobject]@{ ApiName = 'Fractal_flames'; Label = 'fractal-flame'; Quota = 40 }
    [pscustomobject]@{ ApiName = 'Created_with_Apophysis'; Label = 'apophysis'; Quota = 30 }
    [pscustomobject]@{ ApiName = 'Mandelbulbs'; Label = 'mandelbulb'; Quota = 35 }
    [pscustomobject]@{ ApiName = 'Mandelbrot_sets'; Label = 'mandelbrot'; Quota = 35 }
    [pscustomobject]@{ ApiName = 'Julia_sets'; Label = 'julia'; Quota = 35 }
    [pscustomobject]@{ ApiName = 'Fractals_created_with_Fractal_Explorer'; Label = 'fractal-explorer'; Quota = 25 }
    [pscustomobject]@{ ApiName = 'Fractals'; Label = 'fractal-art'; Quota = 0 }
)

function Get-CategoryCandidates {
    param(
        [Parameter(Mandatory)] [string]$ApiName,
        [Parameter(Mandatory)] [string]$Label
    )

    $categoryTitle = [uri]::EscapeDataString("Category:$ApiName")
    $uri = "${apiBase}?action=query&generator=categorymembers&gcmtitle=$categoryTitle&gcmtype=file&gcmlimit=max&prop=imageinfo&iiprop=url%7Csize%7Cmime&iiurlwidth=1000&format=json"

    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            $response = Invoke-RestMethod -Uri $uri -Headers @{ 'User-Agent' = $userAgent } -TimeoutSec 90
            if ($null -ne $response.error) {
                throw $response.error.info
            }

            $pages = @($response.query.pages.PSObject.Properties.Value)
            foreach ($page in $pages) {
                if ($null -eq $page.imageinfo) { continue }
                $info = @($page.imageinfo)[0]
                if ($null -eq $info) { continue }
                if ($info.mime -notmatch '^image/(jpeg|png|gif)$') { continue }
                if ($info.size -gt 100000000) { continue }
                if ($info.thumbwidth -lt 500 -or $info.thumbheight -lt 500) { continue }

                [pscustomobject]@{
                    Title = $page.title
                    Label = $Label
                    SourcePage = $info.descriptionurl
                    DownloadUrl = $info.thumburl
                    ExpectedWidth = [int]$info.thumbwidth
                    ExpectedHeight = [int]$info.thumbheight
                    Mime = $info.mime
                }
            }
            return
        }
        catch {
            if ($attempt -eq 3) { throw }
            Start-Sleep -Seconds (5 * $attempt)
        }
    }
}

$allCandidates = New-Object System.Collections.Generic.List[object]
$seenTitles = @{}

foreach ($spec in $categorySpecs) {
    Write-Host "Searching Commons category $($spec.ApiName)..."
    $candidates = @(Get-CategoryCandidates -ApiName $spec.ApiName -Label $spec.Label)
    Write-Host "  Found $($candidates.Count) usable candidates."
    foreach ($candidate in $candidates) {
        $key = $candidate.Title.ToLowerInvariant()
        if (-not $seenTitles.ContainsKey($key)) {
            $seenTitles[$key] = $true
            $allCandidates.Add($candidate)
        }
    }
    Start-Sleep -Seconds 3
}

$selected = New-Object System.Collections.Generic.List[object]
$selectedTitles = @{}
foreach ($spec in $categorySpecs) {
    if ($spec.Quota -eq 0) { continue }
    $group = @($allCandidates | Where-Object { $_.Label -eq $spec.Label })
    foreach ($candidate in ($group | Select-Object -First $spec.Quota)) {
        $key = $candidate.Title.ToLowerInvariant()
        if (-not $selectedTitles.ContainsKey($key)) {
            $selectedTitles[$key] = $true
            $selected.Add($candidate)
        }
    }
}

if ($selected.Count -lt $TargetCount) {
    foreach ($candidate in $allCandidates) {
        if ($selected.Count -ge $TargetCount) { break }
        $key = $candidate.Title.ToLowerInvariant()
        if (-not $selectedTitles.ContainsKey($key)) {
            $selectedTitles[$key] = $true
            $selected.Add($candidate)
        }
    }
}

$selected = @($selected | Select-Object -First $TargetCount)
Write-Host "Selected $($selected.Count) unique images for download."

function Get-SafeStem {
    param([string]$Title)
    $stem = [System.IO.Path]::GetFileNameWithoutExtension(([string]$Title -replace '^File:', ''))
    $stem = $stem -replace '[<>:"/\\|?*]', '_'
    $stem = $stem -replace '\s+', '_'
    $stem = $stem -replace '[^\p{L}\p{N}_-]', '_'
    $stem = $stem.Trim('_')
    if ([string]::IsNullOrWhiteSpace($stem)) { $stem = 'fractal' }
    return $stem.Substring(0, [Math]::Min($stem.Length, 100))
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
for ($index = 0; $index -lt $selected.Count; $index++) {
    $candidate = $selected[$index]
    $extension = switch ($candidate.Mime) {
        'image/png' { '.png' }
        'image/gif' { '.gif' }
        default { '.jpg' }
    }
    $name = '{0:D3}_{1}_{2}{3}' -f ($index + 1), $candidate.Label, (Get-SafeStem -Title $candidate.Title), $extension
    $path = Join-Path $downloadDir $name
    $partPath = "$path.part"

    $downloadedThis = $false
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        try {
            if (Test-Path $partPath) { Remove-Item -LiteralPath $partPath -Force }
            Invoke-WebRequest -Uri $candidate.DownloadUrl -Headers @{ 'User-Agent' = $userAgent } -OutFile $partPath -TimeoutSec 180
            $dimensions = Get-ImageDimensions -Path $partPath
            if ($dimensions.Width -lt 500 -or $dimensions.Height -lt 500) {
                throw "Downloaded image is only $($dimensions.Width)x$($dimensions.Height)."
            }
            Move-Item -LiteralPath $partPath -Destination $path -Force
            $downloadedThis = $true
            Start-Sleep -Seconds 3
            break
        }
        catch {
            if ($attempt -eq 3) {
                Write-Warning "Skipping $($candidate.Title): $($_.Exception.Message)"
            }
            else {
                Start-Sleep -Seconds (3 * $attempt)
            }
        }
    }

    if ($downloadedThis) { $downloaded++ } else { $failed++ }
    Write-Progress -Activity 'Downloading fractal corpus' -Status "$($index + 1) / $($selected.Count)" -PercentComplete ((($index + 1) / $selected.Count) * 100)
}
Write-Progress -Activity 'Downloading fractal corpus' -Completed

$summary = @(
    "Source: Wikimedia Commons categories searched by fractal type/program"
    "Minimum validated dimensions: 500 x 500 pixels"
    "Requested: $($selected.Count)"
    "Downloaded: $downloaded"
    "Failed: $failed"
    "Folder: $downloadDir"
)
[System.IO.File]::WriteAllLines((Join-Path $downloadDir 'README.txt'), $summary, [System.Text.UTF8Encoding]::new($false))
Write-Host ($summary -join [Environment]::NewLine)
