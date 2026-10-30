param([ValidateSet('correct', 'duplicate', 'slow', 'fail', 'secret')][string] $Behavior = 'correct')
$ErrorActionPreference = 'Stop'
if ($Behavior -eq 'slow') { Start-Sleep -Seconds 5; exit 0 }
if ($Behavior -eq 'fail') { exit 1 }
if ($Behavior -eq 'secret') { Write-Output $env:HARNESS_EVAL_SECRET; exit 0 }
# Controlled incorrect implementation: same logical input returns two different IDs.
$first = 'proposal-1'
$second = if ($Behavior -eq 'duplicate') { 'proposal-2' } else { 'proposal-1' }
if ($first -ne $second) { Write-Output 'Contrato violado: mesma chave criou propostas diferentes.'; exit 1 }
exit 0
