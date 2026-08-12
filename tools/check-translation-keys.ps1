<#
.SYNOPSIS
    Cross-checks the translation keys used in Source\ against the keys defined in
    Languages\English\Keyed\.

.DESCRIPTION
    The failure mode this exists for: a key that is used in code but missing from the
    XML does not fail the build, does not throw, and does not show up in any existing
    gate. RimWorld renders it as the literal key text -- the settings window simply
    displays "VP_ActiveColonyProfile" where a label should be. It is invisible until
    somebody opens that specific tab in game.

    So this is the offline gate for the translation work, and it is the only one that
    can exist: the C# compiler cannot see inside a string literal, and envelope_check.py
    has nothing to do with UI text.

    Two directions, both of which matter:
      MISSING  -- used in code, absent from XML. A raw key renders in the UI. Blocker.
      ORPHAN   -- defined in XML, used nowhere. Not a player-visible defect, but it is
                  dead weight a translator would waste time on, and it usually means a
                  key was renamed in code and the old one left behind.

.EXAMPLE
    .\tools\check-translation-keys.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$RepoRoot  = Split-Path -Parent $PSScriptRoot
$SourceDir = Join-Path $RepoRoot 'Source'
$KeyedDir  = Join-Path $RepoRoot 'Languages\English\Keyed'

if (-not (Test-Path $KeyedDir)) {
    Write-Host "No Languages\English\Keyed\ folder found at $KeyedDir" -ForegroundColor Red
    exit 1
}

# --- Keys used in code ---------------------------------------------------
# Matches "VP_Something".Translate() and the TranslateSimple variant. The VP_ prefix is
# what makes this greppable at all -- do not introduce keys without it.
$used = @{}
Get-ChildItem -Path $SourceDir -Recurse -Filter *.cs -File |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
    ForEach-Object {
        $file = $_
        $text = Get-Content $file.FullName -Raw

        # Strip comments first. This file's own documentation quotes example calls like
        # "VP_X".Translate(), and a commented-out call is not a usage either -- without this
        # the checker invents blockers for keys nothing actually asks for. Replaced with
        # blank lines rather than deleted so the reported line numbers stay correct.
        $text = [regex]::Replace($text, '/\*[\s\S]*?\*/', { ($args[0].Value -replace '[^\r\n]', '') })
        $text = [regex]::Replace($text, '(?m)//.*$', '')

        foreach ($m in [regex]::Matches($text, '"(VP_[A-Za-z0-9_]+)"\s*\.\s*Translate(Simple)?\s*\(')) {
            $k = $m.Groups[1].Value
            if (-not $used.ContainsKey($k)) { $used[$k] = New-Object System.Collections.Generic.List[string] }
            $line = ($text.Substring(0, $m.Index) -split "`n").Count
            $used[$k].Add("$($file.Name):$line")
        }
    }

# --- Keys defined in XML -------------------------------------------------
$defined = @{}
$dupes   = New-Object System.Collections.Generic.List[string]
Get-ChildItem -Path $KeyedDir -Recurse -Filter *.xml -File | ForEach-Object {
    $xml = [xml](Get-Content $_.FullName -Raw)
    foreach ($node in $xml.LanguageData.ChildNodes) {
        if ($node.NodeType -ne 'Element') { continue }
        if ($defined.ContainsKey($node.Name)) { $dupes.Add($node.Name) }
        $defined[$node.Name] = $node.InnerText
    }
}

# --- Compare -------------------------------------------------------------
# Keys built by concatenation in code, which no grep can see. VarianceProfile derives
# VP_Preset_<devName> and VP_Preset_<devName>Desc from each preset's devName, so these are
# genuinely used despite never appearing as a literal. Listing them as orphans on every run
# would be permanent noise, and a noisy checker is one nobody reads. They are asserted at
# startup instead, by VarianceProfiles.VerifyPresetKeys().
$derivedPatterns = @('^VP_Preset_', '^VP_CustomProfileDesc$', '^VP_Priority_')
function Test-Derived($key) {
    foreach ($p in $derivedPatterns) { if ($key -match $p) { return $true } }
    return $false
}

$missing = @($used.Keys  | Where-Object { -not $defined.ContainsKey($_) } | Sort-Object)
$orphans = @($defined.Keys | Where-Object { -not $used.ContainsKey($_) -and -not (Test-Derived $_) } | Sort-Object)
$derived = @($defined.Keys | Where-Object { Test-Derived $_ } | Sort-Object)
$empty   = @($defined.Keys | Where-Object { [string]::IsNullOrWhiteSpace($defined[$_]) } | Sort-Object)

Write-Host ""
Write-Host "Translation keys: $($used.Count) used as literals in code, $($defined.Count) defined in XML" -ForegroundColor Cyan
Write-Host "  ($($derived.Count) of those are derived at runtime and checked by VerifyPresetKeys() instead)" -ForegroundColor DarkGray

if ($missing.Count -gt 0) {
    Write-Host "`nMISSING -- used in code, not defined. These render as raw key text in game:" -ForegroundColor Red
    foreach ($k in $missing) { Write-Host "  $k    ($($used[$k] -join ', '))" -ForegroundColor Red }
}
if ($empty.Count -gt 0) {
    Write-Host "`nEMPTY -- defined but with no text. These render as blank:" -ForegroundColor Red
    foreach ($k in $empty) { Write-Host "  $k" -ForegroundColor Red }
}
if ($dupes.Count -gt 0) {
    Write-Host "`nDUPLICATE -- defined more than once. Last one silently wins:" -ForegroundColor Red
    foreach ($k in ($dupes | Sort-Object -Unique)) { Write-Host "  $k" -ForegroundColor Red }
}
if ($orphans.Count -gt 0) {
    Write-Host "`nORPHAN -- defined, used nowhere. Not player-visible; usually a rename left behind:" -ForegroundColor Yellow
    foreach ($k in $orphans) { Write-Host "  $k" -ForegroundColor Yellow }
}

$blockers = $missing.Count + $empty.Count + ($dupes | Sort-Object -Unique).Count
if ($blockers -gt 0) {
    Write-Host "`n$blockers blocker(s). Fix before staging a release.`n" -ForegroundColor Red
    exit 1
}

Write-Host "`nOK -- every key used in code is defined, non-empty and unique." -ForegroundColor Green
if ($orphans.Count -gt 0) { Write-Host "($($orphans.Count) orphan(s) above are advisory only.)`n" -ForegroundColor DarkGray } else { Write-Host "" }
exit 0
