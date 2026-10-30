#Requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateRange(1024, 65535)]
    [int] $OraclePort = 15210,
    [ValidateRange(1024, 65535)]
    [int] $RabbitPort = 56720
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
    if (-not (Select-String -LiteralPath '.env' -Pattern '^RABBIT_PASSWORD=' -Quiet)) {
        "RABBIT_PASSWORD=Pc$([Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(24)))" | Add-Content -LiteralPath '.env' -Encoding utf8NoBOM
    }
    if (-not (Select-String -LiteralPath '.env' -Pattern '^RABBIT_PORT=' -Quiet)) {
        "RABBIT_PORT=$RabbitPort" | Add-Content -LiteralPath '.env' -Encoding utf8NoBOM
    }
    foreach ($name in @('DEV_ADMIN_TOKEN', 'DEV_EMPRESA_A_TOKEN', 'DEV_EMPRESA_B_TOKEN', 'DEV_ANALISTA_TOKEN', 'DEV_OPERADOR_TOKEN')) {
        if (-not (Select-String -LiteralPath '.env' -Pattern "^$name=" -Quiet)) {
            "$name=$([Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)))" |
                Add-Content -LiteralPath '.env' -Encoding utf8NoBOM
        }
    }
    Import-LocalEnvironment
    Invoke-Checked docker @('compose', 'up', '-d', '--wait', '--wait-timeout', '600', 'oracle', 'rabbitmq')
    $brokerUsers = & docker compose exec -T rabbitmq rabbitmqctl -q list_users --formatter json
    if ($LASTEXITCODE -ne 0) { throw 'Nao foi possivel verificar o usuario RabbitMQ.' }
    if (-not ($brokerUsers | ConvertFrom-Json | Where-Object { $_.name -eq 'pc_worker' })) {
        Invoke-Checked docker @('compose', 'exec', '-T', 'rabbitmq', 'sh', '-c', 'rabbitmqctl add_user pc_worker "$RABBITMQ_DEFAULT_PASS" >/dev/null')
    }
    $permissions = '^(proposta-credito\.credito(\.dlx)?\.v1|fila-.*)$'
    Invoke-Checked docker @('compose', 'exec', '-T', 'rabbitmq', 'rabbitmqctl', 'set_permissions', '-p', 'proposta-credito', 'pc_worker', $permissions, $permissions, $permissions)
    Invoke-Checked docker @('compose', 'exec', '-T', 'rabbitmq', 'rabbitmqctl', 'set_user_tags', 'pc_worker')
    Invoke-Checked pwsh @('-NoProfile', '-File', './scripts/inspect-messaging.ps1')
    Invoke-Checked dotnet @('restore', 'PropostaCredito.sln', '--locked-mode')
    Invoke-Checked dotnet @('run', '--project', 'tools/PropostaCredito.DatabaseMigrator', '--configuration', 'Release', '--no-restore')
    Write-Host 'Ambiente preparado. Execute ./scripts/verify.ps1.'
}
finally {
    Pop-Location
}
