# Recover App.vue after a PowerShell ANSI/UTF-8 round-trip corrupted it.
#
# Recovery source: the last successful Vite build (dist/assets/*.js), which ran
# before the corruption and still contains every original string literal.
#
# Matching: find the CJK run immediately before each U+FFFD in the bundle, then
# read forward while the bundle stays CJK (or space). What follows the run is
# what the corruption ate. Right-hand anchoring is deliberately NOT used -- the
# text after the damage is usually ASCII punctuation and the minifier rewrites
# quotes, so it almost never lines up.
#
# Comments are absent from the bundle, so sites inside comments stay damaged.
# Harmless: comments never break compilation.
#
# ASCII-only on purpose: non-ASCII in a .ps1 that PowerShell 5.1 reads as ANSI
# is the very class of bug being fixed here.

param(
    [string]$Source = 'src\App.vue',
    [string]$Bundle,
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'

$src = Get-Content -Raw -Encoding UTF8 $Source
if (-not $Bundle) {
    $Bundle = (Get-ChildItem dist\assets\*.js | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
}
$bundle = Get-Content -Raw -Encoding UTF8 $Bundle

$FFFD = [char]0xFFFD
$MAXRUN = 14

function Get-LeftRun([string]$text, [int]$pos) {
    $start = $pos
    while ($start -gt 0 -and ($pos - $start) -lt $MAXRUN -and [int]$text[$start - 1] -gt 127) { $start-- }
    return $text.Substring($start, $pos - $start)
}

$sites = [regex]::Matches($src, [regex]::Escape($FFFD))
$resolved = New-Object System.Collections.ArrayList
$unresolved = New-Object System.Collections.ArrayList

foreach ($m in $sites) {
    $i = $m.Index

    $left = Get-LeftRun $src $i
    if ($left.Length -lt 2) { [void]$unresolved.Add($i); continue }

    $found = $null
    $ambiguous = $false
    $from = 0
    while ($true) {
        $p = $bundle.IndexOf($left, $from, [StringComparison]::Ordinal)
        if ($p -lt 0) { break }
        $from = $p + 1

        $end = $p + $left.Length
        $limit = $end + 6
        while ($end -lt $bundle.Length -and $end -lt $limit) {
            $ch = [int]$bundle[$end]
            if ($ch -gt 127 -or $ch -eq 32) { $end++ } else { break }
        }

        if ($end -ge $bundle.Length) { continue }

        $cand = $bundle.Substring($p + $left.Length, $end - ($p + $left.Length)).TrimEnd()
        if ($cand.Length -eq 0) { continue }

        if ($null -eq $found) { $found = $cand }
        elseif ($found -ne $cand) { $ambiguous = $true }
    }

    if ($null -ne $found -and -not $ambiguous) {
        [void]$resolved.Add([pscustomobject]@{ Index = $i; Gap = $found; Left = $left })
    }
    else {
        [void]$unresolved.Add($i)
    }
}

Write-Host ("sites={0}  resolved={1}  unresolved={2}" -f $sites.Count, $resolved.Count, $unresolved.Count)

if (-not $Apply) {
    Write-Host ''
    Write-Host '--- resolved (first 25) ---'
    $resolved | Select-Object -First 25 | ForEach-Object { "  {0}<<{1}>>" -f $_.Left, $_.Gap }
    return
}

$out = $src
$fixed = 0
for ($k = $resolved.Count - 1; $k -ge 0; $k--) {
    $r = $resolved[$k]
    $end = $r.Index + 1
    if ($end -lt $out.Length -and $out[$end] -eq '?') { $end++ }
    $out = $out.Substring(0, $r.Index) + $r.Gap + $out.Substring($end)
    $fixed++
}

[System.IO.File]::WriteAllText((Resolve-Path $Source), $out, (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("applied {0} replacements" -f $fixed)
Write-Host ("remaining U+FFFD: {0}" -f ([regex]::Matches($out, [regex]::Escape($FFFD))).Count)
