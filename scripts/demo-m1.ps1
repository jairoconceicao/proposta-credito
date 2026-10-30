#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/common.ps1"
Push-Location $script:RepositoryRoot
$process = $null
try {
    Initialize-DotnetEnvironment
    Import-LocalEnvironment
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
    $baseUri = "http://127.0.0.1:$port"
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    [void]$start.ArgumentList.Add((Join-Path $script:RepositoryRoot 'src/PropostaCredito.Api/bin/Release/net10.0/PropostaCredito.Api.dll'))
    [void]$start.ArgumentList.Add('--urls'); [void]$start.ArgumentList.Add($baseUri)
    $start.Environment['ASPNETCORE_ENVIRONMENT'] = 'Development'
    $start.Environment['Authentication__Mode'] = 'Development'
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($process.HasExited) { throw 'API encerrou antes da demonstracao; verifique configuracao.' }
        try {
            $health = Invoke-WebRequest "$baseUri/health/ready" -TimeoutSec 12 -SkipHttpErrorCheck
            if ($health.StatusCode -eq 200) { $ready = $true; break }
        }
        catch [System.Net.Http.HttpRequestException] { }
        Start-Sleep -Milliseconds 500
    }
    if (-not $ready) { throw 'Oracle/schema nao ficaram prontos. Execute bootstrap e verify.' }
    $admin = @{ Authorization = "Bearer $($env:DevelopmentAuth__Identities__0__Token)"; 'Idempotency-Key' = 'demo-m1-empresa-a-v1' }
    $company = Invoke-RestMethod "$baseUri/empresas" -Method Post -Headers $admin -ContentType 'application/json' -Body (
        @{ cnpj = '12ABC34501DE35'; razaoSocial = 'Empresa ficticia de demonstracao'; responsavelId = 'empresa-a' } | ConvertTo-Json)
    $headers = @{ Authorization = "Bearer $($env:DevelopmentAuth__Identities__1__Token)"; 'Idempotency-Key' = 'demo-m1-proposta-v1' }
    $body = @{ cnpj = $company.cnpj; faturamentoMensal = 10000; valorSolicitado = 1000; prazoMeses = 12; moeda = 'BRL' } | ConvertTo-Json
    $first = Invoke-RestMethod "$baseUri/propostas" -Method Post -Headers $headers -ContentType 'application/json' -Body $body
    $repeat = Invoke-RestMethod "$baseUri/propostas" -Method Post -Headers $headers -ContentType 'application/json' -Body $body
    if ($first.propostaId -ne $repeat.propostaId) { throw 'Falha de idempotencia na demonstracao.' }
    $view = Invoke-RestMethod "$baseUri/propostas/$($first.propostaId)" -Headers $headers
    [ordered]@{ empresaId = $company.empresaId; propostaId = $first.propostaId; repeticaoMesmoId = $true; estado = $view.estado; situacao = $view.situacaoOperacional } | ConvertTo-Json
}
finally {
    if ($null -ne $process) {
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
    Pop-Location
}
