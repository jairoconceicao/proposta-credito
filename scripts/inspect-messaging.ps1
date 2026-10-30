#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/common.ps1"
Push-Location $script:RepositoryRoot
try {
    $definitions = Get-Content infra/rabbitmq/definitions.json -Raw | ConvertFrom-Json
    $queueJson = & docker compose exec -T rabbitmq rabbitmqctl -q list_queues -p proposta-credito name type policy --formatter json
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao verificar filas RabbitMQ.' }
    $queues = @($queueJson | ConvertFrom-Json)
    foreach ($expected in $definitions.queues) {
        $actual = @($queues | Where-Object name -EQ $expected.name)
        if ($actual.Count -ne 1 -or $actual[0].type -ne 'quorum') { throw "Fila quorum ausente/incompativel: $($expected.name)" }
        $policyName = if ($expected.name.EndsWith('.dlq')) { 'dlq-capacity' } else {
            switch ($expected.name) { 'fila-solicitacao-analise' { 'analise' }; 'fila-solicitacao-bureau' { 'bureau' }; 'fila-resultado-bureau' { 'resultado' }; 'fila-solicitacao-contrato' { 'contrato' } }
        }
        if ($actual[0].policy -ne $policyName) { throw "Policy nao efetiva: $($expected.name)" }
    }
    $policyJson = & docker compose exec -T rabbitmq rabbitmqctl -q list_policies -p proposta-credito --formatter json
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao verificar policies RabbitMQ.' }
    $policies = @($policyJson | ConvertFrom-Json)
    foreach ($expected in $definitions.policies) {
        $actual = @($policies | Where-Object name -EQ $expected.name)
        if ($actual.Count -ne 1) { throw "Policy ausente: $($expected.name)" }
        $actualDefinition = if ($actual[0].definition -is [string]) { $actual[0].definition | ConvertFrom-Json } else { $actual[0].definition }
        foreach ($property in $expected.definition.PSObject.Properties) {
            if ($actualDefinition.($property.Name) -ne $property.Value) { throw "Policy divergente: $($expected.name) / $($property.Name)" }
        }
    }
    $bindingJson = & docker compose exec -T rabbitmq rabbitmqctl -q list_bindings -p proposta-credito source_name destination_name destination_kind routing_key --formatter json
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao verificar bindings RabbitMQ.' }
    $bindings = @($bindingJson | ConvertFrom-Json)
    foreach ($expected in $definitions.bindings) {
        if (-not ($bindings | Where-Object { $_.source_name -eq $expected.source -and $_.destination_name -eq $expected.destination -and $_.routing_key -eq $expected.routing_key })) {
            throw "Binding ausente: $($expected.routing_key)"
        }
    }
    Write-Host 'Topologia verificada: 8 filas quorum, policies efetivas e 8 bindings de negocio/DLQ.'
}
finally { Pop-Location }
