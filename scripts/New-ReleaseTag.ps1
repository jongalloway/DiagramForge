<#
.SYNOPSIS
    Prompts for a release version, normalizes it to a v-prefixed git tag, and pushes it.

.DESCRIPTION
    Accepts an optional version string, or prompts interactively when omitted.
    If the supplied value does not begin with 'v', the script prepends it.
    The script then creates an annotated git tag and pushes it to origin.

.PARAMETER Version
    Optional version or tag name. Examples: 1.0.0, v1.0.0, 1.0.0-rc.1.

.EXAMPLE
    pwsh scripts/New-ReleaseTag.ps1

.EXAMPLE
    pwsh scripts/New-ReleaseTag.ps1 -Version 1.0.0-rc.1
#>
[CmdletBinding()]
param(
    [string]$Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [switch]$AllowFailure
    )

    $output = & git @Arguments 2>&1
    if (-not $AllowFailure -and $LASTEXITCODE -ne 0) {
        $message = ($output | Out-String).Trim()
        if ([string]::IsNullOrWhiteSpace($message)) {
            $message = "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
        }
        throw $message
    }

    return $output
}

function Normalize-TagName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $trimmed = $Value.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed)) {
        throw 'A version number is required.'
    }

    if ($trimmed.StartsWith('v', [System.StringComparison]::OrdinalIgnoreCase)) {
        return 'v' + $trimmed.Substring(1)
    }

    return 'v' + $trimmed
}

$repoRoot = (Get-Item $PSScriptRoot).Parent.FullName
Push-Location $repoRoot
try {
    $null = Invoke-Git -Arguments @('rev-parse', '--is-inside-work-tree')

    if (-not $Version) {
        $Version = Read-Host 'Enter release version (example: 1.0.0 or v1.0.0-rc.1)'
    }

    $tagName = Normalize-TagName -Value $Version

    $localTag = Invoke-Git -Arguments @('tag', '--list', $tagName)
    if (($localTag | Out-String).Trim()) {
        throw "Tag already exists locally: $tagName"
    }

    $remoteTag = Invoke-Git -Arguments @('ls-remote', '--tags', 'origin', $tagName) -AllowFailure
    if (($remoteTag | Out-String).Trim()) {
        throw "Tag already exists on origin: $tagName"
    }

    Write-Host "Repository: $repoRoot"
    Write-Host "Tag:        $tagName"

    $confirmation = Read-Host 'Create and push this tag? [y/N]'
    if ($confirmation -notmatch '^(y|yes)$') {
        Write-Host 'Aborted. No tag was created.'
        return
    }

    $null = Invoke-Git -Arguments @('tag', '-a', $tagName, '-m', "Release $tagName")
    $null = Invoke-Git -Arguments @('push', 'origin', $tagName)

    Write-Host "Created and pushed tag $tagName"
}
finally {
    Pop-Location
}