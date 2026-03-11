<#
.SYNOPSIS
    Assembles the E2E snapshot SVGs into a single gallery/collage SVG for the README.

.DESCRIPTION
    Reads every .expected.svg file in the E2E Fixtures directory, scales each to
    fit a uniform cell in a responsive grid, and writes a single self-contained
    SVG to docs/gallery.svg under the repository root by default.

    Because the output is pure SVG composition (nested <svg> elements with viewBox),
    no image manipulation libraries or rasterisation tools are needed.

.PARAMETER OutputPath
    Path for the generated gallery SVG. Defaults to docs/gallery.svg relative to
    the repo root.

.PARAMETER Columns
    Number of columns in the grid. Defaults to 4.

.PARAMETER CellWidth
    Width of each grid cell in SVG user units. Defaults to 320.

.PARAMETER Padding
    Space between cells. Defaults to 24.

.EXAMPLE
    pwsh scripts/Build-Gallery.ps1
    pwsh scripts/Build-Gallery.ps1 -Columns 3 -CellWidth 400
#>
[CmdletBinding()]
param(
    [string]$OutputPath,
    [int]$Columns = 4,
    [double]$CellWidth = 320,
    [double]$Padding = 24
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Resolve paths ──────────────────────────────────────────────────────────────

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$fixturesDir = Join-Path $repoRoot 'tests' 'DiagramForge.E2ETests' 'Fixtures'

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot 'docs' 'gallery.svg'
}

if (-not (Test-Path $fixturesDir)) {
    Write-Error "Fixtures directory not found: $fixturesDir"
    return
}

# ── Collect SVG files ──────────────────────────────────────────────────────────

$svgFiles = Get-ChildItem -Path $fixturesDir -Filter '*.expected.svg' | Sort-Object Name

if ($svgFiles.Count -eq 0) {
    Write-Error "No .expected.svg files found in $fixturesDir"
    return
}

Write-Host "Found $($svgFiles.Count) fixture SVGs"

# ── Parse each SVG's dimensions ────────────────────────────────────────────────

$entries = @()
foreach ($file in $svgFiles) {
    [xml]$doc = Get-Content -LiteralPath $file.FullName -Raw
    $root = $doc.DocumentElement

    $w = [double]$root.GetAttribute('width')
    $h = [double]$root.GetAttribute('height')

    # Friendly label from filename: "mermaid-flowchart-lr.expected.svg" → "Flowchart LR"
    $label = $file.BaseName -replace '\.expected$', ''
    $label = $label -replace '^mermaid-', '' -replace '^conceptual-', 'Conceptual '
    $label = ($label -split '-' | ForEach-Object {
        if ($_.Length -le 2) { $_.ToUpperInvariant() } else { [System.Globalization.CultureInfo]::InvariantCulture.TextInfo.ToTitleCase($_) }
    }) -join ' '

    $entries += [PSCustomObject]@{
        File    = $file
        Width   = $w
        Height  = $h
        Label   = $label
        Content = (Get-Content -LiteralPath $file.FullName -Raw)
    }
}

# ── Layout grid ────────────────────────────────────────────────────────────────

$labelHeight = 28          # Space reserved below each cell for the label
$labelFontSize = 12
$headerHeight = 56         # Space at top for the gallery title
$footerHeight = 16         # Bottom padding
$maxCellHeight = 260       # Cap cell height so tall diagrams don't dominate

$rows = [Math]::Ceiling($entries.Count / $Columns)

# Compute uniform cell height based on the tallest scaled diagram, capped
$maxAspect = ($entries | ForEach-Object { $_.Height / $_.Width } | Measure-Object -Maximum).Maximum
$cellHeight = [Math]::Min([Math]::Ceiling($CellWidth * $maxAspect), $maxCellHeight)

$totalWidth  = $Columns * $CellWidth + ($Columns - 1) * $Padding + $Padding * 2
$totalHeight = $headerHeight + $rows * ($cellHeight + $labelHeight + $Padding) + $footerHeight

# ── Build composite SVG ───────────────────────────────────────────────────────

$sb = [System.Text.StringBuilder]::new()

[void]$sb.AppendLine("<?xml version=`"1.0`" encoding=`"UTF-8`"?>")
[void]$sb.AppendLine("<svg xmlns=`"http://www.w3.org/2000/svg`" width=`"$($totalWidth.ToString('F0'))`" height=`"$($totalHeight.ToString('F0'))`" viewBox=`"0 0 $($totalWidth.ToString('F0')) $($totalHeight.ToString('F0'))`">")

# Background
[void]$sb.AppendLine("  <rect width=`"100%`" height=`"100%`" fill=`"#F8FAFC`" rx=`"12`"/>")

# Title
$titleX = $totalWidth / 2
[void]$sb.AppendLine("  <text x=`"$($titleX.ToString('F0'))`" y=`"36`" text-anchor=`"middle`" font-family=`"&quot;Segoe UI&quot;, Inter, Arial, sans-serif`" font-size=`"20`" font-weight=`"bold`" fill=`"#1F2937`">DiagramForge — Sample Gallery</text>")

for ($i = 0; $i -lt $entries.Count; $i++) {
    $entry = $entries[$i]
    $col = $i % $Columns
    $row = [Math]::Floor($i / $Columns)

    $cellX = $Padding + $col * ($CellWidth + $Padding)
    $cellY = $headerHeight + $row * ($cellHeight + $labelHeight + $Padding)

    # Cell background (subtle card)
    [void]$sb.AppendLine("  <rect x=`"$($cellX.ToString('F0'))`" y=`"$($cellY.ToString('F0'))`" width=`"$($CellWidth.ToString('F0'))`" height=`"$($cellHeight.ToString('F0'))`" fill=`"#FFFFFF`" stroke=`"#E5E7EB`" stroke-width=`"1`" rx=`"8`"/>")

    # Nested SVG with viewBox — scales the diagram to fit the cell while
    # preserving aspect ratio (xMidYMid meet is the SVG default).
    $vb = "0 0 $($entry.Width.ToString('F2')) $($entry.Height.ToString('F2'))"
    [void]$sb.AppendLine("  <svg x=`"$($cellX.ToString('F0'))`" y=`"$($cellY.ToString('F0'))`" width=`"$($CellWidth.ToString('F0'))`" height=`"$($cellHeight.ToString('F0'))`" viewBox=`"$vb`" preserveAspectRatio=`"xMidYMid meet`">")

    # Strip the XML declaration and outer <svg> wrapper, keep inner content.
    $inner = $entry.Content
    # Remove XML declaration if present
    $inner = $inner -replace '<\?xml[^?]*\?>\s*', ''
    # Remove the opening <svg ...> tag
    $inner = $inner -replace '<svg[^>]*>', ''
    # Remove the closing </svg> tag
    $inner = $inner -replace '</svg>\s*$', ''

    # Namespace marker IDs to avoid collisions across nested SVGs
    $inner = $inner -replace 'id="arrowhead"', "id=`"arrowhead-$i`""
    $inner = $inner -replace 'url\(#arrowhead\)', "url(#arrowhead-$i)"
    $inner = $inner -replace '#arrowhead"', "#arrowhead-$i`""

    [void]$sb.AppendLine($inner.Trim())
    [void]$sb.AppendLine("  </svg>")

    # Label below the cell
    $labelX = $cellX + $CellWidth / 2
    $labelY = $cellY + $cellHeight + $labelHeight - 8
    [void]$sb.AppendLine("  <text x=`"$($labelX.ToString('F0'))`" y=`"$($labelY.ToString('F0'))`" text-anchor=`"middle`" font-family=`"&quot;Segoe UI&quot;, Inter, Arial, sans-serif`" font-size=`"$labelFontSize`" fill=`"#6B7280`">$([System.Security.SecurityElement]::Escape($entry.Label))</text>")
}

[void]$sb.AppendLine("</svg>")

# ── Write output ───────────────────────────────────────────────────────────────

$outDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null
}

Set-Content -LiteralPath $OutputPath -Value $sb.ToString() -Encoding UTF8 -NoNewline
Write-Host "Gallery SVG written to: $OutputPath"
Write-Host "  Diagrams: $($entries.Count)  Grid: ${Columns}x${rows}  Size: $($totalWidth.ToString('F0'))x$($totalHeight.ToString('F0'))"
