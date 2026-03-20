<#
.SYNOPSIS
    Builds separate diagram and theme gallery SVGs from the E2E snapshot fixtures.

.DESCRIPTION
    Reads the .expected.svg files in the E2E Fixtures directory, scales each to
    fit a uniform cell in a responsive grid, and writes two self-contained SVG
    galleries by default:

    - doc/diagram-gallery.svg for representative diagram examples
    - doc/theme-gallery.svg for the dedicated built-in theme showcase fixtures

    Because the output is pure SVG composition (nested <svg> elements with viewBox),
    no image manipulation libraries or rasterisation tools are needed.

.PARAMETER DiagramOutputPath
    Path for the generated diagram gallery SVG. Defaults to
    doc/diagram-gallery.svg relative to the repo root.

.PARAMETER ThemeOutputPath
    Path for the generated theme gallery SVG. Defaults to
    doc/theme-gallery.svg relative to the repo root.

.PARAMETER DiagramColumns
    Number of columns in the diagram gallery grid. Defaults to 4.

.PARAMETER ThemeColumns
    Number of columns in the theme gallery grid. Defaults to 5.

.PARAMETER CellWidth
    Width of each grid cell in SVG user units. Defaults to 320.

.PARAMETER Padding
    Space between cells. Defaults to 24.

.EXAMPLE
    pwsh scripts/Build-Gallery.ps1
#>
[CmdletBinding()]
param(
    [string]$DiagramOutputPath,
    [string]$ThemeOutputPath,
    [int]$DiagramColumns = 4,
    [int]$ThemeColumns = 5,
    [double]$CellWidth = 320,
    [double]$Padding = 24
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$fixturesDir = Join-Path $repoRoot 'tests' 'DiagramForge.E2ETests' 'Fixtures'

if (-not $DiagramOutputPath) {
    $DiagramOutputPath = Join-Path $repoRoot 'doc' 'diagram-gallery.svg'
}

if (-not $ThemeOutputPath) {
    $ThemeOutputPath = Join-Path $repoRoot 'doc' 'theme-gallery.svg'
}

if (-not (Test-Path $fixturesDir)) {
    Write-Error "Fixtures directory not found: $fixturesDir"
    return
}

$svgFiles = Get-ChildItem -Path $fixturesDir -Filter '*.expected.svg' | Sort-Object Name
if ($svgFiles.Count -eq 0) {
    Write-Error "No .expected.svg files found in $fixturesDir"
    return
}

Write-Host "Found $($svgFiles.Count) fixture SVGs"

function Get-FriendlyLabel {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    return (($Value -split '-') | ForEach-Object {
            if ($_.Length -le 2) {
                $_.ToUpperInvariant()
            }
            else {
                [System.Globalization.CultureInfo]::InvariantCulture.TextInfo.ToTitleCase($_)
            }
        }) -join ' '
}

$entries = @()
foreach ($file in $svgFiles) {
    [xml]$doc = Get-Content -LiteralPath $file.FullName -Raw
    $root = $doc.DocumentElement

    $width = [double]$root.GetAttribute('width')
    $height = [double]$root.GetAttribute('height')
    $fixtureName = $file.BaseName -replace '\.expected$', ''
    $inputPath = Join-Path $fixturesDir ($fixtureName + '.input')
    $themeName = $null

    if (Test-Path $inputPath) {
        $rawInput = Get-Content -LiteralPath $inputPath -Raw
        if ($rawInput -match '(?im)^theme:\s*(.+)$') {
            $themeName = $Matches[1].Trim()
        }
    }

    $isThemeShowcase = $fixtureName -like 'mermaid-theme-*'
    if ($isThemeShowcase -and $themeName) {
        $label = Get-FriendlyLabel $themeName
    }
    else {
        $labelSeed = $fixtureName -replace '^mermaid-', '' -replace '^conceptual-', 'Conceptual '
        $label = (($labelSeed -split '-') | ForEach-Object {
                if ($_.Length -le 2) {
                    $_.ToUpperInvariant()
                }
                else {
                    [System.Globalization.CultureInfo]::InvariantCulture.TextInfo.ToTitleCase($_)
                }
            }) -join ' '
    }

    $entries += [PSCustomObject]@{
        File = $file
        Width = $width
        Height = $height
        Label = $label
        Theme = $themeName
        IsThemeShowcase = $isThemeShowcase
        Content = (Get-Content -LiteralPath $file.FullName -Raw)
    }
}

function Write-GallerySvg {
    param(
        [Parameter(Mandatory = $true)]
        [array]$GalleryEntries,
        [Parameter(Mandatory = $true)]
        [string]$OutputPath,
        [Parameter(Mandatory = $true)]
        [string]$Title,
        [Parameter(Mandatory = $true)]
        [int]$Columns
    )

    $labelHeight = 34
    $labelFontSize = 14
    $headerHeight = 56
    $footerHeight = 16
    $maxCellHeight = 260

    $rows = [Math]::Ceiling($GalleryEntries.Count / $Columns)
    $maxAspect = ($GalleryEntries | ForEach-Object { $_.Height / $_.Width } | Measure-Object -Maximum).Maximum
    $cellHeight = [Math]::Min([Math]::Ceiling($CellWidth * $maxAspect), $maxCellHeight)

    $totalWidth = $Columns * $CellWidth + ($Columns - 1) * $Padding + $Padding * 2
    $totalHeight = $headerHeight + $rows * ($cellHeight + $labelHeight + $Padding) + $footerHeight

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("<?xml version=`"1.0`" encoding=`"UTF-8`"?>")
    [void]$sb.AppendLine("<svg xmlns=`"http://www.w3.org/2000/svg`" width=`"$($totalWidth.ToString('F0'))`" height=`"$($totalHeight.ToString('F0'))`" viewBox=`"0 0 $($totalWidth.ToString('F0')) $($totalHeight.ToString('F0'))`">")
    [void]$sb.AppendLine("  <rect width=`"100%`" height=`"100%`" fill=`"#F8FAFC`" rx=`"12`"/>")

    $titleX = $totalWidth / 2
    [void]$sb.AppendLine("  <text x=`"$($titleX.ToString('F0'))`" y=`"36`" text-anchor=`"middle`" font-family=`"&quot;Segoe UI&quot;, Inter, Arial, sans-serif`" font-size=`"20`" font-weight=`"bold`" fill=`"#1F2937`">$([System.Security.SecurityElement]::Escape($Title))</text>")

    for ($i = 0; $i -lt $GalleryEntries.Count; $i++) {
        $entry = $GalleryEntries[$i]
        $col = $i % $Columns
        $row = [Math]::Floor($i / $Columns)

        $cellX = $Padding + $col * ($CellWidth + $Padding)
        $cellY = $headerHeight + $row * ($cellHeight + $labelHeight + $Padding)

        [void]$sb.AppendLine("  <rect x=`"$($cellX.ToString('F0'))`" y=`"$($cellY.ToString('F0'))`" width=`"$($CellWidth.ToString('F0'))`" height=`"$($cellHeight.ToString('F0'))`" fill=`"#FFFFFF`" stroke=`"#E5E7EB`" stroke-width=`"1`" rx=`"8`"/>")

        $viewBox = "0 0 $($entry.Width.ToString('F2')) $($entry.Height.ToString('F2'))"
        [void]$sb.AppendLine("  <svg x=`"$($cellX.ToString('F0'))`" y=`"$($cellY.ToString('F0'))`" width=`"$($CellWidth.ToString('F0'))`" height=`"$($cellHeight.ToString('F0'))`" viewBox=`"$viewBox`" preserveAspectRatio=`"xMidYMid meet`">")

        $inner = $entry.Content
        $inner = $inner -replace '<\?xml[^?]*\?>\s*', ''
        $inner = $inner -replace '<svg[^>]*>', ''
        $inner = $inner -replace '</svg>\s*$', ''

        # Namespace all arrowhead marker IDs to avoid cross-fixture collisions in the combined SVG.
        $inner = $inner -replace 'id="arrowhead"', "id=`"arrowhead-$i`""
        $inner = $inner -replace 'url\(#arrowhead\)', "url(#arrowhead-$i)"
        $inner = $inner -replace '#arrowhead"', "#arrowhead-$i`""
        $inner = $inner -replace 'id="arrowhead-open"', "id=`"arrowhead-open-$i`""
        $inner = $inner -replace 'url\(#arrowhead-open\)', "url(#arrowhead-open-$i)"
        $inner = $inner -replace '#arrowhead-open"', "#arrowhead-open-$i`""
        $inner = $inner -replace 'id="arrowhead-filled-diamond"', "id=`"arrowhead-filled-diamond-$i`""
        $inner = $inner -replace 'url\(#arrowhead-filled-diamond\)', "url(#arrowhead-filled-diamond-$i)"
        $inner = $inner -replace '#arrowhead-filled-diamond"', "#arrowhead-filled-diamond-$i`""
        $inner = $inner -replace 'id="arrowhead-open-diamond"', "id=`"arrowhead-open-diamond-$i`""
        $inner = $inner -replace 'url\(#arrowhead-open-diamond\)', "url(#arrowhead-open-diamond-$i)"
        $inner = $inner -replace '#arrowhead-open-diamond"', "#arrowhead-open-diamond-$i`""
        # Node gradients/filters (node-0-fill-gradient, node-0-soft-shadow, etc.):
        $inner = $inner -replace 'id="(node-\d+)', "id=`"g${i}-`$1"
        $inner = $inner -replace 'url\(#(node-\d+)', "url(#g${i}-`$1"
        # Snake path gradient:
        $inner = $inner -replace 'id="snake-gradient"', "id=`"snake-gradient-$i`""
        $inner = $inner -replace 'url\(#snake-gradient\)', "url(#snake-gradient-$i)"

        [void]$sb.AppendLine($inner.Trim())
        [void]$sb.AppendLine("  </svg>")

        $labelX = $cellX + $CellWidth / 2
        $labelY = $cellY + $cellHeight + $labelHeight - 8
        [void]$sb.AppendLine("  <text x=`"$($labelX.ToString('F0'))`" y=`"$($labelY.ToString('F0'))`" text-anchor=`"middle`" font-family=`"&quot;Segoe UI&quot;, Inter, Arial, sans-serif`" font-size=`"$labelFontSize`" fill=`"#6B7280`">$([System.Security.SecurityElement]::Escape($entry.Label))</text>")
    }

    [void]$sb.AppendLine("</svg>")

    $outDir = Split-Path $OutputPath -Parent
    if (-not (Test-Path $outDir)) {
        New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    }

    Set-Content -LiteralPath $OutputPath -Value $sb.ToString() -Encoding UTF8 -NoNewline
    Write-Host "Gallery SVG written to: $OutputPath"
    Write-Host "  Diagrams: $($GalleryEntries.Count)  Grid: ${Columns}x${rows}  Size: $($totalWidth.ToString('F0'))x$($totalHeight.ToString('F0'))"
}

$diagramEntries = @($entries | Where-Object { -not $_.IsThemeShowcase })
$themeEntries = @($entries | Where-Object { $_.IsThemeShowcase })

if ($diagramEntries.Count -eq 0) {
    Write-Error 'No diagram gallery entries were found.'
    return
}

if ($themeEntries.Count -eq 0) {
    Write-Error 'No theme gallery entries were found. Add mermaid-theme-*.input fixtures first.'
    return
}

Write-GallerySvg -GalleryEntries $diagramEntries -OutputPath $DiagramOutputPath -Title 'DiagramForge - Diagram Gallery' -Columns $DiagramColumns
Write-GallerySvg -GalleryEntries $themeEntries -OutputPath $ThemeOutputPath -Title 'DiagramForge - Theme Gallery' -Columns $ThemeColumns
