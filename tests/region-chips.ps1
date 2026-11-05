# In-game check of the browse tab: placard facts on the tiles and the region chip row.
# Needs a running slot with NPC Cards loaded and the phone app mounted, and NPC Vault reachable.
#   pwsh -File tests/region-chips.ps1 -Slot mptest3
param([string]$Slot = 'mptest3')
$ErrorActionPreference = 'Stop'
$s1 = Join-Path $PSScriptRoot '..\..\Workspace\tools\bridge\s1.py'
$fail = 0
function Check($name, $cond, $detail) {
    if ($cond) { "PASS $name" } else { "FAIL $name $detail"; $script:fail++ }
}
function Js($code) { (python $s1 --slot $Slot eval npccards $code 2>&1 | Out-String).Trim() }
function WaitFor($code, $want, $seconds = 20) {
    $end = (Get-Date).AddSeconds($seconds)
    do { $v = Js $code; if ($v -match $want) { return $v }; Start-Sleep -Milliseconds 500 } while ((Get-Date) -lt $end)
    return $v
}

Js "show('browse'); 'ok'" | Out-Null
$n = WaitFor "document.querySelectorAll('#browse-list .tile').length" '[1-9]'
Check 'browse shows tiles' ($n -match '[1-9]') "got: $n"

$chips = Js "Array.from(document.querySelectorAll('#regions .chip')).map(c => c.textContent).join('|')"
Check 'region chip row has 7 chips' (($chips -split '\|').Count -eq 7) "got: $chips"

$facts = Js "Array.from(document.querySelectorAll('#browse-list .tile-facts')).map(f => f.textContent).join('|')"
Check 'tiles show role and region' ($facts -match ' · ') "got: $facts"

Js "document.querySelectorAll('#regions .chip')[4].click(); 'ok'" | Out-Null
$docks = WaitFor "browse.loading ? 'loading' : Array.from(document.querySelectorAll('#browse-list .tile-facts')).map(f => f.textContent).join('|')" 'Docks'
$parts = @($docks -split '\|' | Where-Object { $_ })
Check 'docks chip filters to docks' (($parts.Count -ge 1) -and (@($parts | Where-Object { $_ -notmatch 'Docks' }).Count -eq 0)) "got: $docks"
$on = Js "document.querySelector('#regions .chip.on').textContent"
Check 'docks chip is marked on' ($on -match 'Docks') "got: $on"

$shot = (python $s1 --slot $Slot shot 2>&1 | Out-String).Trim()
"shot: $shot"

Js "document.querySelectorAll('#regions .chip')[0].click(); 'ok'" | Out-Null
if ($fail) { "FAILED: $fail"; exit 1 } else { 'ALL PASS' }
