<#
.SYNOPSIS
    Renders DiagramForge-compatible fenced diagrams in markdown files to SVG.

.DESCRIPTION
    Scans markdown files for fenced code blocks labeled `mermaid`, `diagram`,
    `diagramforge`, or `conceptual`, renders each block to SVG using DiagramForge,
    and writes the outputs to a mirror directory under OutputRoot.

    Optionally rewrites markdown to replace fenced diagrams with image references.
    By default, rewrite mode preserves the original diagram source inside a
    collapsible `<details>` block so the markdown file retains an editable
    source-of-truth without risking broken HTML comment syntax.

.PARAMETER RootPath
    Root directory to scan for markdown files. Defaults to the repository root.

.PARAMETER OutputRoot
    Root directory where rendered SVGs will be written. Defaults to
    artifacts/rendered-diagrams relative to the repository root.

.PARAMETER Mode
    Render mode:
    - project: uses the local CLI project via `dotnet run`
    - dnx: uses `dnx --yes DiagramForge.Tool`

.PARAMETER RewriteMarkdown
    Rewrites markdown files to replace matching fenced code blocks with rendered
    image references.

.PARAMETER SourceHandling
    Controls rewrite behavior when -RewriteMarkdown is used:
    - details: preserve the original fence in a collapsible <details> block and add an image reference
    - remove: replace the original fence with an image reference only

.EXAMPLE
    pwsh scripts/Render-MarkdownDiagrams.ps1 -RootPath docs -OutputRoot docs/generated/diagrams

.EXAMPLE
    pwsh scripts/Render-MarkdownDiagrams.ps1 -RootPath docs -OutputRoot docs/generated/diagrams -RewriteMarkdown -SourceHandling details
#>
[CmdletBinding()]
param(
    [string]$RootPath,
    [string]$OutputRoot,
    [ValidateSet('project', 'dnx')]
    [string]$Mode = 'project',
    [switch]$RewriteMarkdown,
    [ValidateSet('details', 'remove')]
    [string]$SourceHandling = 'details'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName

if (-not $RootPath) {
    $RootPath = $repoRoot
}

if (-not $OutputRoot) {
    $OutputRoot = Join-Path $repoRoot 'artifacts\rendered-diagrams'
}

$RootPath = [System.IO.Path]::GetFullPath($RootPath)
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)

$supportedFenceLanguages = @('mermaid', 'diagram', 'diagramforge', 'conceptual')
$script:renderedFenceCount = 0
$script:rewrittenFileCount = 0

function Ensure-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Get-RelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,
        [Parameter(Mandatory = $true)]
        [string]$TargetPath
    )

    $baseUri = [System.Uri]((Resolve-Path -LiteralPath $BasePath).Path.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar)
    $targetUri = [System.Uri](Resolve-Path -LiteralPath $TargetPath).Path
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
}

function Get-MarkdownFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return Get-ChildItem -Path $Path -Recurse -File -Filter '*.md' |
        Where-Object {
            $_.FullName -notlike "*$([System.IO.Path]::DirectorySeparatorChar).git$([System.IO.Path]::DirectorySeparatorChar)*" -and
            $_.FullName -notlike "*$([System.IO.Path]::DirectorySeparatorChar)bin$([System.IO.Path]::DirectorySeparatorChar)*" -and
            $_.FullName -notlike "*$([System.IO.Path]::DirectorySeparatorChar)obj$([System.IO.Path]::DirectorySeparatorChar)*" -and
            $_.FullName -notlike "$OutputRoot*"
        }
}

function Get-DiagramFenceMatches {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text
    )

    $pattern = '(?ms)^```(?<lang>[A-Za-z0-9_-]+)\s*\r?\n(?<body>.*?)^```[ \t]*(?:\r?\n|$)'
    $matches = [System.Text.RegularExpressions.Regex]::Matches($Text, $pattern)

    $result = @()
    for ($i = 0; $i -lt $matches.Count; $i++) {
        $match = $matches[$i]
        $lang = $match.Groups['lang'].Value.ToLowerInvariant()
        if ($supportedFenceLanguages -notcontains $lang) {
            continue
        }

        $result += [PSCustomObject]@{
            Match = $match
            Index = $i + 1
            Language = $lang
            Body = $match.Groups['body'].Value.TrimEnd("`r", "`n")
        }
    }

    return $result
}

function Invoke-DiagramRender {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InputPath,
        [Parameter(Mandatory = $true)]
        [string]$OutputPath,
        [Parameter(Mandatory = $true)]
        [string]$SourceMarkdownPath,
        [Parameter(Mandatory = $true)]
        [int]$SourceFenceIndex
    )

    Ensure-Directory -Path (Split-Path -Path $OutputPath -Parent)

    if ($Mode -eq 'dnx') {
        & dnx --yes DiagramForge.Tool $InputPath --output $OutputPath
    }
    else {
        & dotnet run --project (Join-Path $repoRoot 'src\DiagramForge.Cli') -c Release -- $InputPath --output $OutputPath
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Diagram render failed for fence $SourceFenceIndex in '$SourceMarkdownPath' with exit code $LASTEXITCODE."
    }
}

function Get-OutputPathForFence {
    param(
        [Parameter(Mandatory = $true)]
        [string]$MarkdownPath,
        [Parameter(Mandatory = $true)]
        [int]$FenceIndex
    )

    $relativeMarkdown = Get-RelativePath -BasePath $RootPath -TargetPath $MarkdownPath
    $relativeDirectory = Split-Path -Path $relativeMarkdown -Parent
    $fileNameStem = [System.IO.Path]::GetFileNameWithoutExtension($MarkdownPath)
    $svgFileName = "$fileNameStem.diagram-$FenceIndex.svg"

    if ([string]::IsNullOrWhiteSpace($relativeDirectory)) {
        return Join-Path $OutputRoot $svgFileName
    }

    return Join-Path (Join-Path $OutputRoot $relativeDirectory) $svgFileName
}

function Get-RewriteContent {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Fence,
        [Parameter(Mandatory = $true)]
        [string]$RelativeSvgPath
    )

    $normalizedPath = $RelativeSvgPath.Replace('\', '/')
    $imageLine = "![Diagram $($Fence.Index)]($normalizedPath)"

    if ($SourceHandling -eq 'remove') {
        return $imageLine
    }

    $fenceStart = '```' + $Fence.Language
    $fenceEnd = '```'
    $originalFence = @(
        '<details>',
        '<summary>diagram source</summary>',
        '',
        $fenceStart,
        $Fence.Body,
        $fenceEnd,
        '',
        '</details>',
        '',
        $imageLine
    ) -join [Environment]::NewLine

    return $originalFence
}

function Process-MarkdownFile {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$File
    )

    $raw = Get-Content -LiteralPath $File.FullName -Raw
    $fences = Get-DiagramFenceMatches -Text $raw
    if ($fences.Count -eq 0) {
        return
    }

    Write-Host "Rendering $($fences.Count) diagram(s) from $($File.FullName)"

    $replacements = @()
    foreach ($fence in $fences) {
        $tempExtension = if ($fence.Language -eq 'mermaid') { '.mmd' } else { '.txt' }
        $tempBaseName = [System.IO.Path]::GetFileNameWithoutExtension([IO.Path]::GetRandomFileName())
        $tempPath = Join-Path ([IO.Path]::GetTempPath()) ($tempBaseName + $tempExtension)
        Set-Content -LiteralPath $tempPath -Value $fence.Body -Encoding UTF8 -NoNewline

        try {
            $svgPath = Get-OutputPathForFence -MarkdownPath $File.FullName -FenceIndex $fence.Index
            Invoke-DiagramRender -InputPath $tempPath -OutputPath $svgPath -SourceMarkdownPath $File.FullName -SourceFenceIndex $fence.Index
            $script:renderedFenceCount++

            if ($RewriteMarkdown) {
                $relativeSvgPath = Get-RelativePath -BasePath $File.Directory.FullName -TargetPath $svgPath
                $replacements += [PSCustomObject]@{
                    Start = $fence.Match.Index
                    Length = $fence.Match.Length
                    Replacement = Get-RewriteContent -Fence $fence -RelativeSvgPath $relativeSvgPath
                }
            }
        }
        finally {
            Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue
        }
    }

    if (-not $RewriteMarkdown -or $replacements.Count -eq 0) {
        return
    }

    $updated = $raw
    foreach ($replacement in ($replacements | Sort-Object Start -Descending)) {
        $updated = $updated.Remove($replacement.Start, $replacement.Length).Insert($replacement.Start, $replacement.Replacement)
    }

    if ($updated -ne $raw) {
        Set-Content -LiteralPath $File.FullName -Value $updated -Encoding UTF8 -NoNewline
        $script:rewrittenFileCount++
    }
}

Ensure-Directory -Path $OutputRoot

$markdownFiles = @(Get-MarkdownFiles -Path $RootPath)
Write-Host "Scanning $($markdownFiles.Count) markdown file(s) under $RootPath"

foreach ($file in $markdownFiles) {
    Process-MarkdownFile -File $file
}

Write-Host "Rendered $script:renderedFenceCount diagram fence(s)."
if ($RewriteMarkdown) {
    Write-Host "Rewrote $script:rewrittenFileCount markdown file(s)."
}