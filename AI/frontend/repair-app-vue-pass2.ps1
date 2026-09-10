# Second pass: fix the remaining compile blockers in App.vue.
#
# The first pass recovered 40 sites from the build bundle. The rest are either
# comments (absent from the bundle, harmless) or string literals whose closing
# quote was ALSO eaten -- those break the build.
#
# Every replacement is built from Unicode code points so this file stays pure
# ASCII. Non-ASCII source in a .ps1 that Windows PowerShell 5.1 reads as ANSI
# is precisely the bug that caused this mess.
#
# Two PowerShell traps this file works around, both hit on the first attempt:
#   1. "$FFFD?" parses the variable name as "FFFD?", swallowing the '?'.
#      Always write ${FFFD} to delimit it.
#   2. In @('a' + $x, 'b') the comma binds tighter than '+', so the pair
#      collapses into a 3-element array. Every element is parenthesised.
#
# Read AND write go through [System.IO.File] with an explicit UTF8 encoding,
# never Get-Content/Set-Content: `Get-Content` without -Encoding decodes as
# ANSI in PowerShell 5.1 and destroys Chinese text.

$ErrorActionPreference = 'Stop'

$path = (Resolve-Path 'src\App.vue').Path
$utf8 = New-Object System.Text.UTF8Encoding($false)
$text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)

$FFFD = [char]0xFFFD
function C([int[]]$codes) { -join ($codes | ForEach-Object { [char]$_ }) }

$fixes = @(
    @( ("name: '未分${FFFD}?,"), ("name: '" + (C 0x672A, 0x5206, 0x7EC4) + "',") ),
    @( ("= '登录失败${FFFD}? + e.message"), ("= '" + (C 0x767B, 0x5F55, 0x5931, 0x8D25, 0xFF1A) + "' + e.message") ),
    @( ("= '移除失败${FFFD}? + e.message"), ("= '" + (C 0x79FB, 0x9664, 0x5931, 0x8D25, 0xFF1A) + "' + e.message") ),
    @( ("'重命名失${FFFD}"), ("'" + (C 0x91CD, 0x547D, 0x540D, 0x5931, 0x8D25) + "'") ),
    @( ("+ 's' : '${FFFD}?,"), ("+ 's' : '" + (C 0x2014) + "',") ),
    @( ("'zh-CN') : '${FFFD}"), ("'zh-CN') : '" + (C 0x2014) + "'") ),
    @( ("+ '${FFFD} : text"), ("+ '" + (C 0x2026) + "' : text") ),
    @( ('placeholder="用户名' + $FFFD), ('placeholder="' + (C 0x7528, 0x6237, 0x540D) + '"') )
)

$applied = 0
foreach ($pair in $fixes) {
    $old = [string]$pair[0]
    $new = [string]$pair[1]
    if ($text.Contains($old)) {
        $text = $text.Replace($old, $new)
        $applied++
        Write-Host ("ok  : {0}" -f ($old -replace [regex]::Escape($FFFD), '<FFFD>'))
    }
    else {
        Write-Host ("MISS: {0}" -f ($old -replace [regex]::Escape($FFFD), '<FFFD>'))
    }
}

[System.IO.File]::WriteAllText($path, $text, $utf8)
Write-Host ("applied {0}/{1}" -f $applied, $fixes.Count)
Write-Host ("remaining U+FFFD: {0}" -f ([regex]::Matches($text, [regex]::Escape($FFFD))).Count)
