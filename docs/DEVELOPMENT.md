# Desenvolvimento local e CI

## O que existe em M0 e M1

API HTTP, Worker com encerramento cooperativo, health checks, Oracle, migrator independente, testes e CI. M1 acrescenta cadastro de empresas, autenticação/autorização, propostas com histórico/outbox e idempotência transacional. Veja [contrato M1](specs/m1-cadastro-propostas.md). Handlers e decisões entram em M2.

## Pré-requisitos

- .NET SDK **10.0.403** x64, fixado em `global.json`.
- PowerShell **7** (`pwsh`), inclusive em Linux.
- Docker com engine Linux e Compose com suporte a `up --wait`.
- Memória disponível para Oracle; reservar pelo menos 4 GB ao Docker e espaço para a imagem/volume.
- Acesso ao NuGet e ao registry Docker no primeiro bootstrap.

Os scripts mantêm cache do SDK/NuGet em `.local/` quando `DOTNET_CLI_HOME`/`NUGET_PACKAGES` não foram definidos pelo ambiente. Essa pasta é ignorada pelo Git.

## Preparar e verificar

Na raiz do repositório:

```powershell
pwsh -NoProfile -File ./scripts/bootstrap.ps1
pwsh -NoProfile -File ./scripts/verify.ps1
```

Bootstrap cria `.env` com senhas aleatórias, inicia Oracle, aguarda a saúde do container, restaura dependências pelo lockfile e aplica migrations. Não copie as marcações de `.env.example`: elas documentam os nomes das variáveis, não são credenciais utilizáveis. A primeira execução pode levar vários minutos. Executar bootstrap novamente preserva dados e senhas.

Verify executa restore com `--locked-mode`, build Release, testes unitários, migrations, integração Oracle, smoke HTTP de API/Worker e sete avaliações do harness. Qualquer comando que falhar encerra a verificação com erro. Relatórios TRX ficam em `artifacts/test-results/`. Logs do smoke ficam em `artifacts/smoke/` e não são publicados automaticamente.

Para desenvolvimento sem banco:

```powershell
pwsh -NoProfile -File ./scripts/verify.ps1 -Scope Unit
```

Esse comando é explicitamente parcial. Não conclui M0 e não é usado para substituir integração no CI. A suíte de integração falha, em vez de ser ignorada, quando Oracle ou suas conexões não estão disponíveis.

## Executar os hosts

Em terminais separados, após bootstrap:

```powershell
pwsh -NoProfile -File ./scripts/run.ps1 -Service Api
pwsh -NoProfile -File ./scripts/run.ps1 -Service Worker
```

| Processo | Liveness | Readiness |
|---|---|---|
| API | `http://127.0.0.1:5080/health/live` | `http://127.0.0.1:5080/health/ready` |
| Worker | `http://127.0.0.1:5081/health/live` | `http://127.0.0.1:5081/health/ready` |

Liveness retorna `200` sem consultar dependências. Readiness retorna `200` apenas com acesso ao Oracle, histórico de migrations exatamente compatível e marcador de schema esperado; retorna `503` quando banco/schema não estão prontos. Os hosts não migram o banco no startup. Os logs são JSON; detalhes de comandos/conexões EF estão desativados, e health checks não devolvem exceções do provedor.

`Ctrl+C` encerra os hosts. O Worker deste marco mantém apenas o ciclo de vida; processamento de mensagens entra em M2. O smoke usa portas temporárias e limpa somente os processos que iniciou; o teste unitário do Worker verifica cancelamento cooperativo separadamente.

## Oracle e credenciais

O banco escuta somente em `127.0.0.1:15210`, serviço `FREEPDB1`. A imagem está fixada por versão e digest em `compose.yaml`; dados ficam no volume do projeto. Na primeira execução, `bootstrap.ps1 -OraclePort 15211` escolhe outra porta. Com `.env` existente, altere `ORACLE_PORT` nesse arquivo e execute bootstrap novamente. Para um ambiente descartável independente, defina também `COMPOSE_PROJECT_NAME` diferente antes de iniciar, evitando compartilhar volume/container com o ambiente original.

| Identidade | Uso | Permissões |
|---|---|---|
| `SYSTEM`/`SYS` | Inicialização do container | Administração local; não usados pelos hosts. |
| `PC_OWNER` | Migrations e testes transacionais | Proprietário do schema, provisionado pela imagem. |
| `PC_APP` | API e Worker | Conexão, leitura de metadados/histórico, SELECT/INSERT nas tabelas M1 e UPDATE da resposta idempotente; sem DDL, DELETE ou escrita de metadados. |

Os scripts carregam as conexões no ambiente dos processos, sem imprimi-las. Fora do ambiente Compose, configure `ConnectionStrings__Oracle` (runtime) e `ConnectionStrings__OracleMigrations` (owner) via mecanismo de segredos do ambiente. Os nomes de schema/usuário runtime são os definidos acima. Não coloque conexões com senha em `appsettings.json`, histórico de comandos, Issues ou PRs.

M1: executar bootstrap novamente acrescenta tokens locais ao `.env`, sem trocar senhas Oracle. `run.ps1` e o smoke selecionam Development explicitamente; `verify.ps1` configura o modo fictício dos testes. Hosts iniciados diretamente fora de Development exigem JWT externo configurado; modo local em Staging/Production falha no startup. Não habilite Development em uma implantação real. `scripts/demo-m1.ps1` inicia sua própria API temporária e não imprime credenciais.

Os usuários são criados na primeira inicialização do volume. Trocar apenas a senha em `.env` depois disso não troca a senha no Oracle; restaure os valores originais ou execute uma rotação explícita no banco. Não destrua um volume com dados que deseja preservar.

## Alterar o schema

Carregue o ambiente local no terminal e restaure a ferramenta fixada:

```powershell
. ./scripts/common.ps1
Initialize-DotnetEnvironment
Import-LocalEnvironment
dotnet tool restore
dotnet ef migrations add NomeDaMudanca --project src/PropostaCredito.Infrastructure --startup-project src/PropostaCredito.Infrastructure --output-dir Persistence/Migrations
```

Revise o código e os grants antes de aplicar via bootstrap. Atualize o contrato de compatibilidade do readiness quando o schema exigir. O EF mantém histórico em `PC_OWNER.__EFMigrationsHistory`. Nova execução não reaplica migrations confirmadas. Alterações de pacotes exigem `dotnet restore PropostaCredito.sln` sem `--locked-mode`, revisão dos lockfiles e commit deles.

DDL Oracle tem commits implícitos: uma migration parcialmente executada pode exigir reparo antes de tentar novamente. Histórico EF não torna DDL atômico. Execute migrations em um único processo; em ambientes persistentes, revise backup, compatibilidade e procedimento de recuperação antes do deploy. API/Worker recebem apenas os grants necessários a cada entrega.

## Diagnóstico

- Docker ausente/sem acesso: confirme engine Linux com `docker info`; bootstrap para antes de alterar schema.
- Porta ocupada: ajuste `ORACLE_PORT`; health do container sozinha não comprova acesso pela porta do host.
- Conexão inválida: confira variáveis/senhas sem exibi-las; aplicação recusa configuração incompleta.
- Migrator com erro: confira saúde, credenciais, privilégios e migrations aplicadas com uma ferramenta Oracle local; a saída pública do migrator é sanitizada.
- Readiness `503`: diferencie banco inacessível de migration ausente/mais nova com uma consulta autorizada ao histórico; nunca migre automaticamente o runtime para ocultar incompatibilidade.
- Bootstrap após interrupção: execute novamente; não apague `.env` mantendo um volume inicializado com outras senhas.

Para parar e preservar os dados: `docker compose stop`. Recriação de volume só deve ser feita explicitamente para um ambiente descartável; o bootstrap não apaga dados.

## CI e proteção de main

O workflow `.github/workflows/ci.yml` executa em PRs, pushes para `main` e acionamento manual. Usa runner Ubuntu, SDK fixado, o mesmo bootstrap/verify local, banco descartável, senhas geradas por execução e token com `contents: read`. Actions são fixadas por SHA; não usa `pull_request_target` nem segredos de deploy. O encerramento remove somente o volume descartável do runner. TRXs são guardados por sete dias.

O check esperado é **Build, unit and Oracle integration**. Depois de publicar esta branch e comprovar o workflow no GitHub, configurar esse check e revisão de PR como obrigatórios em `main`, conforme recursos disponíveis. Uma execução com teste quebrado deve falhar antes de concluir PC-06. A implementação local não comprova execução remota, e este commit não inclui CD (PC-34/PC-35).
