<#
.SYNOPSIS
    Rewrites the Changelog section of docs\nexus\description.bbcode from README.md's "## Changelog", so the one
    NexusMods description (pasted on both pages) always carries every release's notes (M5SE-T132).

.DESCRIPTION
    The section runs from the "[size=5][b]Changelog[/b][/size]" heading to the next [size=5] heading; it is
    created before "Shout outs" if missing. The Markdown subset the changelog uses is converted: ### headings,
    **bold**, `code`, [links](url), "- " list items with indented continuation lines, and plain paragraphs.
    tools\package.ps1 runs this on every build; it can also be run on its own.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$utf8 = New-Object System.Text.UTF8Encoding($false)
$readme = [IO.File]::ReadAllText((Join-Path $root 'README.md'), $utf8) -replace "`r`n", "`n"
$descPath = Join-Path $root 'docs\nexus\description.bbcode'
$desc = [IO.File]::ReadAllText($descPath, $utf8)
$nl = if ($desc.Contains("`r`n")) { "`r`n" } else { "`n" }

$m = [regex]::Match($readme, '(?ms)^## Changelog\n(.*?)(?=^## |\z)')
if (-not $m.Success) { throw 'README.md has no "## Changelog" section.' }

function ConvertTo-Inline([string]$text) {
    $text = [regex]::Replace($text, '\[([^\]]+)\]\(([^)]+)\)', '[url=$2]$1[/url]')
    $text = [regex]::Replace($text, '\*\*(.+?)\*\*', '[b]$1[/b]')
    [regex]::Replace($text, '`([^`]+)`', '$1')
}

$out = New-Object System.Collections.Generic.List[string]
$out.Add('[size=5][b]Changelog[/b][/size]')
$block = New-Object System.Collections.Generic.List[string]   # lines of the paragraph or list item being built
$inList = $false
function Close-Block {
    if ($block.Count -gt 0) {
        $line = ConvertTo-Inline (($block | ForEach-Object { $_.Trim() }) -join ' ')
        $out.Add($(if ($inList) { "[*]$line" } else { $line }))
        $block.Clear()
    }
}
function Close-List {
    Close-Block
    if ($script:inList) { $out.Add('[/list]'); $script:inList = $false }
}

foreach ($raw in $m.Groups[1].Value.Split("`n")) {
    if ($raw -match '^### (.+)$') { Close-List; if ($out.Count -gt 1) { $out.Add('') }; $out.Add("[size=4][b]$($Matches[1].Trim())[/b][/size]"); continue }
    if ($raw.Trim() -eq '') { Close-List; continue }
    if ($raw -match '^- (.*)$') {
        Close-Block
        if (-not $inList) { $out.Add('[list]'); $inList = $true }
        $block.Add($Matches[1]); continue
    }
    if ($inList -and $raw -match '^\s+\S') { $block.Add($raw); continue }   # continuation of a list item
    if ($inList) { Close-List }
    $block.Add($raw)
}
Close-List
$section = ($out -join $nl) + $nl + $nl

$heading = '[size=5][b]Changelog[/b][/size]'
$start = $desc.IndexOf($heading)
if ($start -ge 0) {
    $next = $desc.IndexOf('[size=5]', $start + $heading.Length)
    if ($next -lt 0) { throw 'The Changelog section must be followed by another [size=5] heading.' }
    $desc = $desc.Substring(0, $start) + $section + $desc.Substring($next)
} else {
    $anchor = $desc.IndexOf('[size=5][b]Shout outs[/b][/size]')
    if ($anchor -lt 0) { throw 'description.bbcode has no Shout outs heading to put the Changelog before.' }
    $desc = $desc.Substring(0, $anchor) + $section + $desc.Substring($anchor)
}
[IO.File]::WriteAllText($descPath, $desc, $utf8)
$descPath
