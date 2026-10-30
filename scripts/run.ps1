#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Api', 'Worker')]
    [string] $Service
)

. "$PSScriptRoot/common.ps1"
Push-Location $script:RepositoryRoot
try {
    Initialize-DotnetEnvironment
    Import-LocalEnvironment
    $port = if ($Service -eq 'Api') { 5080 } else { 5081 }
    Invoke-Checked dotnet @('run', '--project', "src/PropostaCredito.$Service", '--configuration', 'Release', '--no-launch-profile', '--', '--urls', "http://127.0.0.1:$port")
}
finally {
    Pop-Location
}
