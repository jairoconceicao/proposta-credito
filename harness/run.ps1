#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $TaskPath,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]{1,64}$')][string] $RunId,
    [switch] $Resume,
    [ValidateRange(0, 100)][int] $StopAfterStep = 0,
    [string] $AgentConfiguration = 'Codex desktop; model/configuration not exposed by session'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
$activeProcess = $null
$runLock = $null
$state = $null
$stopwatch = [Diagnostics.Stopwatch]::StartNew()
$previousSeconds = 0.0

function Source-Fingerprint {
    $files = @(git ls-files --cached --others --exclude-standard | Sort-Object -Unique)
    if ($LASTEXITCODE -ne 0) { throw 'Nao foi possivel identificar o snapshot Git.' }
    $parts = foreach ($file in $files) {
        $hash = if (Test-Path -LiteralPath $file -PathType Leaf) { (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash } else { 'deleted' }
        "$($file.Length):${file}:$hash"
    }
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($parts -join "`n"))))
}

function Save-State {
    $state.elapsedSeconds = [Math]::Round($previousSeconds + $stopwatch.Elapsed.TotalSeconds, 3)
    $state.updatedAt = [DateTime]::UtcNow.ToString('o')
    $temporary = Join-Path $runPath 'state.tmp'
    $state | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $temporary -Encoding utf8NoBOM
    Move-Item -LiteralPath $temporary -Destination $statePath -Force
}

function Sanitize([string] $Value) {
    foreach ($secret in $secrets) {
        if ($secret.Length -ge 8) { $Value = $Value.Replace($secret, '[REDACTED]') }
    }
    return $Value
}

try {
    $gitRoot = git rev-parse --show-toplevel
    if ($LASTEXITCODE -ne 0 -or [IO.Path]::GetFullPath($gitRoot) -ne [IO.Path]::GetFullPath($root)) {
        throw 'O runner deve estar na raiz do checkout Git que esta sendo verificado.'
    }
    $resolvedTask = (Resolve-Path -LiteralPath $TaskPath).Path
    if (-not $resolvedTask.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'O contrato da tarefa deve estar dentro do repositorio.'
    }
    $task = Get-Content -LiteralPath $resolvedTask -Raw | ConvertFrom-Json -AsHashtable
    if ($task.schemaVersion -ne 1 -or $task.id -notmatch '^[A-Za-z0-9_-]+$' -or $task.steps.Count -eq 0 -or
        $task.maxAttempts -lt 1 -or $task.maxAttempts -gt 3 -or $task.maxSeconds -lt 1 -or $task.maxSeconds -gt 3600) {
        throw 'Contrato invalido: informe schema, ID, etapas e limites.'
    }
    foreach ($step in $task.steps) {
        if ($step.id -notmatch '^[A-Za-z0-9_-]+$' -or $step.executable -notin @('pwsh', 'dotnet', 'git') -or $step.timeoutSeconds -lt 1) {
            throw 'Etapa invalida: use IDs seguros, executavel permitido e timeout positivo.'
        }
    }
    if (@($task.steps.id | Sort-Object -Unique).Count -ne $task.steps.Count) { throw 'IDs de etapas duplicados.' }
    $taskHash = (Get-FileHash -LiteralPath $resolvedTask -Algorithm SHA256).Hash
    $fingerprint = Source-Fingerprint
    $gitHead = git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Commit base indisponivel.' }
    $runPath = Join-Path $root "artifacts/harness/runs/$RunId"
    $statePath = Join-Path $runPath 'state.json'
    [void](New-Item -ItemType Directory -Force -Path $runPath)
    $runLock = [IO.File]::Open((Join-Path $runPath 'run.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    $secrets = @([Environment]::GetEnvironmentVariables().GetEnumerator() |
        Where-Object { $_.Key -match '(?i)PASSWORD|TOKEN|SECRET|ConnectionStrings' } | ForEach-Object { [string]$_.Value })
    if (Test-Path -LiteralPath '.env') {
        $secrets += @(Get-Content -LiteralPath '.env' | Where-Object { $_ -match '(?i)(PASSWORD|TOKEN|SECRET)=' } | ForEach-Object { ($_ -split '=', 2)[1] })
    }
    if ($Resume) {
        if (-not (Test-Path -LiteralPath $statePath)) { throw 'Checkpoint inexistente.' }
        $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json -AsHashtable
        if ($state.taskHash -ne $taskHash -or $state.sourceFingerprint -ne $fingerprint -or $state.gitHead -ne $gitHead) {
            $state = $null
            throw 'Retomada recusada: tarefa ou codigo mudaram. Inicie outro RunId.'
        }
        $previousSeconds = [double]$state.elapsedSeconds
    }
    else {
        if (Test-Path -LiteralPath $statePath) { throw 'RunId ja utilizado; use -Resume ou um novo ID.' }
        $state = [ordered]@{
            schemaVersion = 1; taskId = $task.id; taskHash = $taskHash; gitHead = $gitHead
            sourceFingerprint = $fingerprint; agentConfiguration = (Sanitize $AgentConfiguration)
            cost = $null; costNote = 'API/model billing not exposed; verification only, no model invocation'
            startedAt = [DateTime]::UtcNow.ToString('o'); updatedAt = ''; elapsedSeconds = 0.0; status = 'pending'
            steps = @($task.steps | ForEach-Object { [ordered]@{ id = $_.id; status = 'pending'; attempts = 0; exitCode = $null; timedOut = $false; commands = @(); logs = @() } })
        }
    }
    Save-State
    for ($index = 0; $index -lt $task.steps.Count; $index++) {
        $definition = $task.steps[$index]
        $progress = $state.steps[$index]
        if ($progress.status -eq 'passed') { continue }
        while ($progress.attempts -lt $task.maxAttempts) {
            $remaining = [double]$task.maxSeconds - $previousSeconds - $stopwatch.Elapsed.TotalSeconds
            if ($remaining -le 0) { $state.status = 'budget-exhausted'; Save-State; exit 1 }
            $progress.attempts++
            $progress.status = 'running'; $state.status = 'running'
            $progress.commands += @{ executable = $definition.executable; arguments = @(Sanitize ($definition.arguments -join ' ')); attempt = $progress.attempts }
            Save-State
            $info = [Diagnostics.ProcessStartInfo]::new($definition.executable)
            foreach ($argument in $definition.arguments) { [void]$info.ArgumentList.Add([string]$argument) }
            $info.UseShellExecute = $false; $info.CreateNoWindow = $true
            $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
            $info.WorkingDirectory = $root
            $activeProcess = [Diagnostics.Process]::Start($info)
            $stdout = $activeProcess.StandardOutput.ReadToEndAsync(); $stderr = $activeProcess.StandardError.ReadToEndAsync()
            $deadline = [DateTime]::UtcNow.AddSeconds([Math]::Min([double]$definition.timeoutSeconds, $remaining))
            $progress.timedOut = $false
            while (-not $activeProcess.WaitForExit(250)) {
                if ([DateTime]::UtcNow -ge $deadline) { $progress.timedOut = $true; $activeProcess.Kill($true); break }
            }
            $activeProcess.WaitForExit()
            $progress.exitCode = $activeProcess.ExitCode
            $logBase = "$($definition.id)-$($progress.attempts)"
            Sanitize ($stdout.GetAwaiter().GetResult()) | Set-Content -LiteralPath (Join-Path $runPath "$logBase.stdout.log")
            Sanitize ($stderr.GetAwaiter().GetResult()) | Set-Content -LiteralPath (Join-Path $runPath "$logBase.stderr.log")
            $progress.logs += @("$logBase.stdout.log", "$logBase.stderr.log")
            $activeProcess.Dispose(); $activeProcess = $null
            $progress.status = if ($progress.exitCode -eq 0 -and -not $progress.timedOut) { 'passed' } else { 'failed' }
            Save-State
            Write-Host "$($definition.id): $($progress.status), tentativa $($progress.attempts)."
            if ($progress.status -eq 'passed') { break }
        }
        if ($progress.status -ne 'passed') { $state.status = 'failed'; Save-State; exit 1 }
        if ($StopAfterStep -eq ($index + 1) -and $index -lt ($task.steps.Count - 1)) {
            $state.status = 'interrupted'; Save-State; exit 2
        }
    }
    $state.status = 'passed'; Save-State
    Write-Host "Tarefa $($task.id) aprovada; checkpoint sanitizado em artifacts/harness/runs/$RunId."
}
finally {
    if ($null -ne $activeProcess) {
        if (-not $activeProcess.HasExited) { $activeProcess.Kill($true); $activeProcess.WaitForExit() }
        $activeProcess.Dispose()
    }
    if ($null -ne $state -and $state.status -eq 'running') { $state.status = 'interrupted'; Save-State }
    if ($null -ne $runLock) { $runLock.Dispose() }
    Pop-Location
}
