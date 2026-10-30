#Requires -Version 7.0
[CmdletBinding()]
param([switch] $ExerciseBrokerRecovery)

. "$PSScriptRoot/common.ps1"
Push-Location $script:RepositoryRoot
$hosts = @()
$brokerStopped = $false

function Start-DemoHost([string] $Name) {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    [void]$start.ArgumentList.Add((Join-Path $script:RepositoryRoot "src/PropostaCredito.$Name/bin/Release/net10.0/PropostaCredito.$Name.dll"))
    [void]$start.ArgumentList.Add('--urls'); [void]$start.ArgumentList.Add("http://127.0.0.1:$port")
    $start.Environment['ASPNETCORE_ENVIRONMENT'] = 'Development'; $start.Environment['Authentication__Mode'] = 'Development'
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    @{ Name = $Name; Process = $process; Uri = "http://127.0.0.1:$port"; Stdout = $process.StandardOutput.ReadToEndAsync(); Stderr = $process.StandardError.ReadToEndAsync() }
}
function Wait-DemoHost($HostInfo, [string] $Probe = 'ready', [int] $Expected = 200) {
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($HostInfo.Process.HasExited) { throw "Host $($HostInfo.Name) encerrou antes da demonstracao." }
        try {
            $response = Invoke-WebRequest "$($HostInfo.Uri)/health/$Probe" -TimeoutSec 10 -SkipHttpErrorCheck
            if ($response.StatusCode -eq $Expected) { return }
        }
        catch [System.Net.Http.HttpRequestException] { }
        Start-Sleep -Milliseconds 250
    }
    throw "Probe $Probe de $($HostInfo.Name) nao retornou $Expected."
}
try {
    Initialize-DotnetEnvironment; Import-LocalEnvironment
    if ($ExerciseBrokerRecovery) {
        Invoke-Checked docker @('compose', 'stop', 'rabbitmq')
        $brokerStopped = $true
    }
    $apiHost = Start-DemoHost Api; $hosts += $apiHost
    Wait-DemoHost $apiHost
    $admin = @{ Authorization = "Bearer $($env:DevelopmentAuth__Identities__0__Token)"; 'Idempotency-Key' = 'demo-m1-empresa-a-v1' }
    $company = Invoke-RestMethod "$($apiHost.Uri)/empresas" -Method Post -Headers $admin -ContentType 'application/json' -Body (
        @{ cnpj = '12ABC34501DE35'; razaoSocial = 'Empresa ficticia de demonstracao'; responsavelId = 'empresa-a' } | ConvertTo-Json)
    $cases = @(@{ Amount = 1000; State = 'APROVADA' }, @{ Amount = 40000; State = 'REJEITADA' }, @{ Amount = 33000; State = 'EM_ANALISE_MANUAL' })
    $views = @()
    foreach ($case in $cases) {
        $headers = @{ Authorization = "Bearer $($env:DevelopmentAuth__Identities__1__Token)"; 'Idempotency-Key' = "demo-m2-$($case.State)-v1" }
        $body = @{ cnpj = $company.cnpj; faturamentoMensal = 10000; valorSolicitado = $case.Amount; prazoMeses = 12; moeda = 'BRL' } | ConvertTo-Json
        $accepted = Invoke-RestMethod "$($apiHost.Uri)/propostas" -Method Post -Headers $headers -ContentType 'application/json' -Body $body
        $views += @{ Id = $accepted.propostaId; Expected = $case.State; Headers = $headers }
    }
    if ($ExerciseBrokerRecovery) {
        $headers = @{ Authorization = "Bearer $($env:DevelopmentAuth__Identities__1__Token)"; 'Idempotency-Key' = "demo-recovery-$([Guid]::NewGuid().ToString('N'))" }
        $body = @{ cnpj = $company.cnpj; faturamentoMensal = 10000; valorSolicitado = 1; prazoMeses = 12; moeda = 'BRL' } | ConvertTo-Json
        $probe = Invoke-RestMethod "$($apiHost.Uri)/propostas" -Method Post -Headers $headers -ContentType 'application/json' -Body $body
        $before = Invoke-RestMethod "$($apiHost.Uri)/propostas/$($probe.propostaId)" -Headers $headers
        if ($before.estado -ne 'RECEBIDA') { throw 'Aceitacao com broker parado nao permaneceu duravel.' }
        $views += @{ Id = $probe.propostaId; Expected = 'APROVADA'; Headers = $headers }
    }
    $worker = Start-DemoHost Worker; $hosts += $worker
    Wait-DemoHost $worker live
    if ($ExerciseBrokerRecovery) {
        Wait-DemoHost $worker ready 503
        Wait-DemoHost $apiHost ready 200
        Invoke-Checked docker @('compose', 'up', '-d', '--wait', '--wait-timeout', '180', 'rabbitmq')
        $brokerStopped = $false
    }
    Wait-DemoHost $worker
    $results = @()
    foreach ($entry in $views) {
        $view = $null
        for ($attempt = 0; $attempt -lt 120; $attempt++) {
            $view = Invoke-RestMethod "$($apiHost.Uri)/propostas/$($entry.Id)" -Headers $entry.Headers
            if ($view.estado -in @('APROVADA', 'REJEITADA', 'EM_ANALISE_MANUAL')) { break }
            Start-Sleep -Milliseconds 250
        }
        if ($view.estado -ne $entry.Expected -or $view.versaoRegras -ne 'lab-v1' -or $view.motivos.Count -ne 3) {
            throw "Fluxo nao concluiu com decisao esperada para proposta $($entry.Id)."
        }
        $results += @{ propostaId = $entry.Id; estado = $view.estado; versaoRegras = $view.versaoRegras; motivos = $view.motivos }
    }
    [void](New-Item -ItemType Directory -Force -Path artifacts/m2-demo)
    @{ brokerRecovery = [bool]$ExerciseBrokerRecovery; results = $results } | ConvertTo-Json -Depth 5 |
        Tee-Object -FilePath artifacts/m2-demo/results.json
}
finally {
    foreach ($info in $hosts) {
        if (-not $info.Process.HasExited) { $info.Process.Kill($true); $info.Process.WaitForExit() }
        # Host logs intentionally stay out of published artifacts; no raw financial/provider data is printed.
        $info.Process.Dispose()
    }
    if ($brokerStopped) { & docker compose up -d --wait --wait-timeout 180 rabbitmq }
    Pop-Location
}
