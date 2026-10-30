#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $root
try {
    $evalId = 'eval-' + [Guid]::NewGuid().ToString('N')
    $directory = Join-Path $root "artifacts/harness/evals/$evalId"
    [void](New-Item -ItemType Directory -Force -Path $directory)
    $results = @()
    $env:HARNESS_EVAL_SECRET = 'Eval' + [Guid]::NewGuid().ToString('N')
    foreach ($scenario in @('duplicate', 'fail', 'slow', 'budget', 'resume', 'secret', 'stale')) {
        $behavior = if ($scenario -in @('resume', 'stale')) { 'correct' } elseif ($scenario -eq 'budget') { 'slow' } else { $scenario }
        $task = @{
            schemaVersion = 1; id = "fixture-$scenario"; maxAttempts = 2; maxSeconds = 30
            steps = @(@{ id = 'contract'; executable = 'pwsh'; arguments = @('-NoProfile', '-File', 'harness/evals/fixture.ps1', '-Behavior', $behavior); timeoutSeconds = $(if ($scenario -eq 'slow') { 1 } else { 10 }) })
        }
        if ($scenario -eq 'budget') { $task.maxSeconds = 2 }
        if ($scenario -in @('resume', 'stale')) { $task.steps += @{ id = 'next'; executable = 'git'; arguments = @('diff', '--check'); timeoutSeconds = 10 } }
        $taskPath = Join-Path $directory "$scenario.json"
        $task | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $taskPath
        $runId = "$evalId-$scenario"
        $arguments = @('-NoProfile', '-File', 'harness/run.ps1', '-TaskPath', $taskPath, '-RunId', $runId)
        if ($scenario -in @('resume', 'stale')) { $arguments += @('-StopAfterStep', '1') }
        & pwsh @arguments
        $exit = $LASTEXITCODE
        $statePath = Join-Path $root "artifacts/harness/runs/$runId/state.json"
        $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        switch ($scenario) {
            { $_ -in @('duplicate', 'fail', 'slow') } {
                if ($exit -ne 1 -or $state.status -ne 'failed' -or $state.steps[0].attempts -ne 2) { throw "Limite/rejeicao falhou: $scenario" }
                if ($scenario -eq 'slow' -and -not $state.steps[0].timedOut) { throw 'Timeout nao interrompeu processo.' }
            }
            'resume' {
                if ($exit -ne 2 -or $state.status -ne 'interrupted') { throw 'Interrupcao nao persistida.' }
                & pwsh -NoProfile -File harness/run.ps1 -TaskPath $taskPath -RunId $runId -Resume
                if ($LASTEXITCODE -ne 0) { throw 'Retomada falhou.' }
                $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
                if ($state.steps[0].attempts -ne 1 -or $state.status -ne 'passed') { throw 'Retomada repetiu etapa aprovada.' }
            }
            'budget' {
                if ($exit -ne 1 -or $state.status -ne 'budget-exhausted') { throw 'Orcamento total nao foi aplicado.' }
            }
            'stale' {
                $task.maxSeconds = 31
                $task | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $taskPath
                & pwsh -NoProfile -File harness/run.ps1 -TaskPath $taskPath -RunId $runId -Resume 2> (Join-Path $directory 'stale.stderr.log')
                if ($LASTEXITCODE -eq 0) { throw 'Retomada aceitou contrato alterado.' }
            }
            'secret' {
                $log = Get-Content -LiteralPath (Join-Path $root "artifacts/harness/runs/$runId/contract-1.stdout.log") -Raw
                if ($exit -ne 0 -or $log.Contains($env:HARNESS_EVAL_SECRET) -or -not $log.Contains('[REDACTED]')) { throw 'Sanitizacao falhou.' }
            }
        }
        $results += @{ scenario = $scenario; passed = $true; runId = $runId }
    }
    $results | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'results.json')
    Write-Host "7 avaliacoes do harness aprovadas. Evidencias: artifacts/harness/evals/$evalId"
}
finally { Remove-Item Env:HARNESS_EVAL_SECRET -ErrorAction SilentlyContinue; Pop-Location }
