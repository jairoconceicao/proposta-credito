Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:RepositoryRoot = Split-Path $PSScriptRoot -Parent

function Assert-Tool([string] $Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Ferramenta obrigatoria ausente: $Name. Consulte docs/DEVELOPMENT.md."
    }
}

function Invoke-Checked([string] $Command, [string[]] $Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Comando '$Command' falhou (codigo $LASTEXITCODE)."
    }
}

function Initialize-DotnetEnvironment {
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    if (-not $env:DOTNET_CLI_HOME) {
        $env:DOTNET_CLI_HOME = Join-Path $script:RepositoryRoot '.local/dotnet'
    }
    if (-not $env:NUGET_PACKAGES) {
        $env:NUGET_PACKAGES = Join-Path $script:RepositoryRoot '.local/nuget/packages'
    }
}

function Import-LocalEnvironment {
    $envFile = Join-Path $script:RepositoryRoot '.env'
    if (Test-Path -LiteralPath $envFile) {
        $values = @{}
        foreach ($line in Get-Content -LiteralPath $envFile) {
            if ($line -match '^\s*(#.*)?$') { continue }
            if ($line -notmatch '^(ORACLE_PASSWORD|APP_USER_PASSWORD|PC_RUNTIME_PASSWORD|ORACLE_PORT)=([A-Za-z0-9]+)$') {
                throw 'Formato invalido no .env. Use os valores alfanumericos gerados pelo bootstrap.'
            }
            $values[$Matches[1]] = $Matches[2]
        }
        foreach ($key in @('ORACLE_PASSWORD', 'APP_USER_PASSWORD', 'PC_RUNTIME_PASSWORD')) {
            if (-not $values.ContainsKey($key) -or $values[$key] -notmatch '^[a-zA-Z][a-zA-Z0-9]{15,}$' -or $values[$key] -eq 'GENERATED_BY_BOOTSTRAP') {
                throw 'Senhas locais ausentes ou invalidas. Nao copie as marcacoes do .env.example.'
            }
        }
        $oraclePort = 15210
        if ($values.ContainsKey('ORACLE_PORT')) {
            $oraclePort = [int]$values['ORACLE_PORT']
        }
        if ($oraclePort -lt 1024 -or $oraclePort -gt 65535) { throw 'ORACLE_PORT deve estar entre 1024 e 65535.' }
        $env:ConnectionStrings__Oracle = "User Id=PC_APP;Password=$($values['PC_RUNTIME_PASSWORD']);Data Source=localhost:$oraclePort/FREEPDB1;Connection Timeout=5;"
        $env:ConnectionStrings__OracleMigrations = "User Id=PC_OWNER;Password=$($values['APP_USER_PASSWORD']);Data Source=localhost:$oraclePort/FREEPDB1;Connection Timeout=5;"
    }
    if (-not $env:ConnectionStrings__Oracle -or -not $env:ConnectionStrings__OracleMigrations) {
        throw 'Configure ConnectionStrings__Oracle e ConnectionStrings__OracleMigrations ou execute bootstrap.ps1.'
    }
}
