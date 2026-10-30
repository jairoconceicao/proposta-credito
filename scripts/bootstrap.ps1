#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateRange(1024, 65535)]
    [int] $OraclePort = 15210
)

. "$PSScriptRoot/common.ps1"
Push-Location $script:RepositoryRoot
try {
    Assert-Tool dotnet
    Assert-Tool docker
    Initialize-DotnetEnvironment
    Invoke-Checked dotnet @('--version')
    Invoke-Checked docker @('info', '--format', '{{.OSType}}')

    if (-not (Test-Path -LiteralPath '.env')) {
        $content = foreach ($name in @('ORACLE_PASSWORD', 'APP_USER_PASSWORD', 'PC_RUNTIME_PASSWORD')) {
            $password = 'Pc' + [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
            "$name=$password"
        }
        @($content; "ORACLE_PORT=$OraclePort") | Set-Content -LiteralPath '.env' -Encoding utf8NoBOM
        Write-Host '.env local criado com senhas aleatorias; arquivo ignorado pelo Git.'
    }
    Import-LocalEnvironment
    Invoke-Checked docker @('compose', 'up', '-d', '--wait', '--wait-timeout', '600', 'oracle')
    Invoke-Checked dotnet @('restore', 'PropostaCredito.sln', '--locked-mode')
    Invoke-Checked dotnet @('run', '--project', 'tools/PropostaCredito.DatabaseMigrator', '--configuration', 'Release', '--no-restore')
    Write-Host 'Ambiente preparado. Execute ./scripts/verify.ps1.'
}
finally {
    Pop-Location
}
