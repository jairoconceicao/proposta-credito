#Requires -Version 7.0
[CmdletBinding()]
param()

. "$PSScriptRoot/common.ps1"
Push-Location $script:RepositoryRoot
$processes = @()
try {
    Initialize-DotnetEnvironment
    Import-LocalEnvironment
    $outputPath = Join-Path $script:RepositoryRoot 'artifacts/smoke'
    [void](New-Item -ItemType Directory -Force -Path $outputPath)
    foreach ($hostName in @('Api', 'Worker')) {
        $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
        $listener.Start()
        $port = $listener.LocalEndpoint.Port
        $listener.Stop()
        $dll = Join-Path $script:RepositoryRoot "src/PropostaCredito.$hostName/bin/Release/net10.0/PropostaCredito.$hostName.dll"
        $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
        [void]$start.ArgumentList.Add($dll)
        [void]$start.ArgumentList.Add('--urls')
        [void]$start.ArgumentList.Add("http://127.0.0.1:$port")
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        $start.RedirectStandardOutput = $true
        $start.RedirectStandardError = $true
        $process = [Diagnostics.Process]::Start($start)
        $processes += $process
        # Drain both pipes asynchronously; never let a full log pipe block the host.
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $ready = $false
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            if ($process.HasExited) { throw "$hostName encerrou antes do smoke test." }
            try {
                $live = Invoke-WebRequest "http://127.0.0.1:$port/health/live" -TimeoutSec 2 -SkipHttpErrorCheck
                $health = Invoke-WebRequest "http://127.0.0.1:$port/health/ready" -TimeoutSec 12 -SkipHttpErrorCheck
                if ($live.StatusCode -eq 200 -and $health.StatusCode -eq 200) { $ready = $true; break }
            }
            catch [System.Net.Http.HttpRequestException] { }
            Start-Sleep -Milliseconds 500
        }
        if (-not $ready) { throw "$hostName nao ficou pronto para o smoke test." }
        Write-Host "${hostName}: liveness e readiness aprovados."
        # Process.Kill is only cleanup; cooperative worker cancellation is verified in unit tests.
        $process.Kill($true)
        $process.WaitForExit()
        $stdout.GetAwaiter().GetResult() | Set-Content (Join-Path $outputPath "$hostName.stdout.log")
        $stderr.GetAwaiter().GetResult() | Set-Content (Join-Path $outputPath "$hostName.stderr.log")
    }
}
finally {
    foreach ($process in $processes) {
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
    Pop-Location
}
