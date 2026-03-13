<#
.SYNOPSIS
    Watches a diagram source file and rerenders it to SVG whenever it changes.

.DESCRIPTION
    Provides a tight edit-save-preview loop for Mermaid and Conceptual DSL files.
    By default it uses `dnx --yes DiagramForge.Tool` so preview runs exercise the latest
    packaged CLI behavior, writes to a stable latest.svg output, and can optionally
    archive timestamped copies after each successful render.

.PARAMETER InputPath
    Path to the diagram source file to watch. Defaults to
    artifacts/scratch/test.mmd relative to the repo root.

.PARAMETER OutputPath
    Path to the SVG output file. Defaults to
    artifacts/scratch/latest.svg relative to the repo root.

.PARAMETER HistoryDirectory
    Directory for archived SVG outputs when -ArchiveOnSuccess is used. Defaults to
    artifacts/scratch/history relative to the repo root.

.PARAMETER Mode
    Render mode:
    - dnx: uses `dnx --yes DiagramForge.Tool`
    - project: uses the local CLI project via `dotnet run`

.PARAMETER Theme
    Optional built-in theme name passed through to the CLI.

.PARAMETER PaletteJson
    Optional JSON palette override passed through to the CLI.

.PARAMETER ThemeFile
    Optional theme JSON file path passed through to the CLI.

.PARAMETER Transparent
    Optional switch that omits the SVG background rect.

.PARAMETER ArchiveOnSuccess
    When set, writes a timestamped SVG copy into HistoryDirectory after each
    successful render.

.PARAMETER PollMilliseconds
    File polling interval in milliseconds. Defaults to 400.

.PARAMETER RenderOnce
    Render once and exit without entering watch mode.

.EXAMPLE
    pwsh scripts/Watch-Preview.ps1

.EXAMPLE
    pwsh scripts/Watch-Preview.ps1 -Theme presentation -ArchiveOnSuccess

.EXAMPLE
    pwsh scripts/Watch-Preview.ps1 -InputPath samples/test.mmd -OutputPath samples/latest.svg -Mode project
#>
[CmdletBinding()]
param(
    [string]$InputPath,
    [string]$OutputPath,
    [string]$HistoryDirectory,
    [ValidateSet('dnx', 'project')]
    [string]$Mode = 'dnx',
    [string]$Theme,
    [string]$PaletteJson,
    [string]$ThemeFile,
    [switch]$Transparent,
    [switch]$ArchiveOnSuccess,
    [int]$PollMilliseconds = 400,
    [switch]$RenderOnce
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
$scratchRoot = Join-Path $repoRoot 'artifacts\scratch'

if (-not $InputPath) {
    $InputPath = Join-Path $scratchRoot 'test.mmd'
}

if (-not $OutputPath) {
    $OutputPath = Join-Path $scratchRoot 'latest.svg'
}

if (-not $HistoryDirectory) {
    $HistoryDirectory = Join-Path $scratchRoot 'history'
}

$InputPath = [System.IO.Path]::GetFullPath($InputPath)
$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$HistoryDirectory = [System.IO.Path]::GetFullPath($HistoryDirectory)

function Ensure-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Initialize-InputFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (Test-Path $Path) {
        return
    }

    Ensure-Directory -Path (Split-Path $Path -Parent)

    $template = @'
flowchart LR
  A[Explore] --> B[Refine]
  B --> C[Ship]
'@

    Set-Content -LiteralPath $Path -Value $template -Encoding UTF8 -NoNewline
    Write-Host "Created starter diagram file: $Path"
}

function Get-RenderCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RenderInputPath
    )

    $arguments = [System.Collections.Generic.List[string]]::new()

    if ($Mode -eq 'dnx') {
        [string[]]$dnxArguments = @('--yes', 'DiagramForge.Tool')
        $arguments.AddRange($dnxArguments)
    }
    elseif ($Mode -eq 'project') {
        [string[]]$dotnetArguments = @('run', '--project', (Join-Path $repoRoot 'src\DiagramForge.Cli'), '--')
        $arguments.AddRange($dotnetArguments)
    }

    $arguments.Add($RenderInputPath)
    $arguments.Add('--output')
    $arguments.Add($OutputPath)

    if ($Theme) {
        $arguments.Add('--theme')
        $arguments.Add($Theme)
    }

    if ($PaletteJson) {
        $arguments.Add('--palette')
        $arguments.Add($PaletteJson)
    }

    if ($ThemeFile) {
        $arguments.Add('--theme-file')
        $arguments.Add([System.IO.Path]::GetFullPath($ThemeFile))
    }

    if ($Transparent) {
        $arguments.Add('--transparent')
    }

    return $arguments
}

function Get-PreparedInput {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath
    )

    $raw = Get-Content -LiteralPath $SourcePath -Raw
    $result = [PSCustomObject]@{
        RenderPath = $SourcePath
        RemoveAfterRender = $false
        StrippedMermaidFence = $false
    }

    if ($raw -match '(?s)^\s*```(?:mermaid)?\s*\r?\n(?<body>.*)\r?\n```\s*$') {
        $tempFile = [System.IO.Path]::GetTempFileName()
        Set-Content -LiteralPath $tempFile -Value $Matches['body'] -Encoding UTF8 -NoNewline

        $result.RenderPath = $tempFile
        $result.RemoveAfterRender = $true
        $result.StrippedMermaidFence = $true
    }

    return $result
}

function Get-FirstContentLine {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text
    )

    foreach ($line in ($Text -split "`r?`n")) {
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('%%', [System.StringComparison]::Ordinal)) {
            continue
        }

        return $trimmed
    }

    return $null
}

function Write-ParseHints {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ExitCode,
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,
        [Parameter(Mandatory = $true)]
        [bool]$StrippedMermaidFence
    )

    if ($ExitCode -ne 2 -or -not (Test-Path $SourcePath)) {
        return
    }

    $raw = Get-Content -LiteralPath $SourcePath -Raw
    $firstContentLine = Get-FirstContentLine -Text $raw
    $hints = [System.Collections.Generic.List[string]]::new()

    if ($StrippedMermaidFence) {
        $hints.Add('Markdown code fences were stripped automatically before rendering.')
    }
    elseif ($raw -match '(?m)^\s*```(?:mermaid)?\s*$') {
        $hints.Add('If you pasted a Markdown example, include both the opening and closing code fences so the watch script can strip them automatically.')
    }

    if ($firstContentLine -and $firstContentLine.Equals('xychart', [System.StringComparison]::OrdinalIgnoreCase)) {
        $hints.Add('Use `xychart-beta` as the header. `xychart` is not recognized by the current parser.')
    }

    if (($raw.TrimStart().StartsWith('---', [System.StringComparison]::Ordinal)) -and ($raw -match '(?m)^\s*config\s*:')) {
        $hints.Add('Leading `---` blocks are treated as DiagramForge frontmatter, not Mermaid config. Remove the Mermaid config block or translate supported settings into DiagramForge frontmatter.')
    }

    if ($hints.Count -eq 0) {
        return
    }

    Write-Host ''
    Write-Host 'Common fixes:' -ForegroundColor Yellow
    foreach ($hint in $hints) {
        Write-Host "  - $hint" -ForegroundColor Yellow
    }
}

function Invoke-Render {
    $preparedInput = Get-PreparedInput -SourcePath $InputPath

    try {
        $arguments = Get-RenderCommand -RenderInputPath $preparedInput.RenderPath
        $commandName = if ($Mode -eq 'project') { 'dotnet' } else { 'dnx' }

        Write-Host "Rendering $(Split-Path $InputPath -Leaf) -> $(Split-Path $OutputPath -Leaf)"
        if ($preparedInput.StrippedMermaidFence) {
            Write-Host '  Detected outer Markdown code fence; rendering the inner diagram body.'
        }

        & $commandName @arguments 2>&1 | Tee-Object -Variable renderOutput | Out-Host
        if ($LASTEXITCODE -ne 0) {
            Write-ParseHints -ExitCode $LASTEXITCODE -SourcePath $InputPath -StrippedMermaidFence:$preparedInput.StrippedMermaidFence
            throw "Render command failed with exit code $LASTEXITCODE."
        }

        if ($ArchiveOnSuccess) {
            Ensure-Directory -Path $HistoryDirectory
            $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
            $historyFile = Join-Path $HistoryDirectory ($stamp + '.svg')
            Copy-Item -LiteralPath $OutputPath -Destination $historyFile -Force
            Write-Host "Archived copy: $historyFile"
        }

        Write-Host "Updated preview: $OutputPath"
    }
    finally {
        if ($preparedInput.RemoveAfterRender -and (Test-Path $preparedInput.RenderPath)) {
            Remove-Item -LiteralPath $preparedInput.RenderPath -Force -ErrorAction SilentlyContinue
        }
    }
}

Ensure-Directory -Path (Split-Path $OutputPath -Parent)
Initialize-InputFile -Path $InputPath

Write-Host "Watch preview starting"
Write-Host "  Input:  $InputPath"
Write-Host "  Output: $OutputPath"
Write-Host "  Mode:   $Mode"
if ($ArchiveOnSuccess) {
    Write-Host "  Archive: $HistoryDirectory"
}

$initialRenderSucceeded = $true
try {
    Invoke-Render
}
catch {
    $initialRenderSucceeded = $false
    Write-Warning $_.Exception.Message
    if ($RenderOnce) {
        exit 1
    }
}

if ($RenderOnce) {
    return
}

$lastWriteUtc = (Get-Item -LiteralPath $InputPath).LastWriteTimeUtc
if (-not $initialRenderSucceeded) {
    Write-Host 'Initial render failed. Fix the source file and save to retry.' -ForegroundColor Yellow
}
Write-Host 'Watching for changes. Press Ctrl+C to stop.'

while ($true) {
    Start-Sleep -Milliseconds $PollMilliseconds

    if (-not (Test-Path $InputPath)) {
        continue
    }

    $currentWriteUtc = (Get-Item -LiteralPath $InputPath).LastWriteTimeUtc
    if ($currentWriteUtc -le $lastWriteUtc) {
        continue
    }

    $lastWriteUtc = $currentWriteUtc

    try {
        Invoke-Render
    }
    catch {
        Write-Warning $_.Exception.Message
    }
}