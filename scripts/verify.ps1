#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('All', 'Unit')]
    [string] $Scope = 'All'
)

. "$PSScriptRoot/common.ps1"
Push-Location $script:RepositoryRoot
try {
    Assert-Tool dotnet
    Initialize-DotnetEnvironment
    if ($Scope -eq 'All') {
        Import-LocalEnvironment
        $env:Authentication__Mode = 'Development'
    }
    Invoke-Checked dotnet @('restore', 'PropostaCredito.sln', '--locked-mode')
    Invoke-Checked dotnet @('build', 'PropostaCredito.sln', '--configuration', 'Release', '--no-restore')
    $results = Join-Path $script:RepositoryRoot 'artifacts/test-results'
    Invoke-Checked dotnet @('test', 'tests/PropostaCredito.UnitTests', '--configuration', 'Release', '--no-build', '--logger', 'trx;LogFileName=unit.trx', '--results-directory', $results)
    if ($Scope -eq 'All') {
        Invoke-Checked dotnet @('run', '--project', 'tools/PropostaCredito.DatabaseMigrator', '--configuration', 'Release', '--no-build')
        Invoke-Checked dotnet @('test', 'tests/PropostaCredito.IntegrationTests', '--configuration', 'Release', '--no-build', '--logger', 'trx;LogFileName=integration.trx', '--results-directory', $results)
        Invoke-Checked pwsh @('-NoProfile', '-File', './scripts/smoke.ps1')
        Invoke-Checked pwsh @('-NoProfile', '-File', './harness/evals/run.ps1')
    }
    Write-Host "Verificacao '$Scope' aprovada. Relatorios em artifacts/test-results."
    if ($Scope -eq 'Unit') { Write-Host 'Integracao e smoke nao executados; este escopo nao conclui M0.' }
}
finally {
    Pop-Location
}
