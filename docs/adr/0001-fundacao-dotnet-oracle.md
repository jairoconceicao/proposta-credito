# ADR 0001 — Fundação .NET, Oracle e verificações de M0

Data: 30 de outubro de 2026.
Status: adotado para desenvolvimento; evidências e pendências em [M0-VALIDACAO.md](../M0-VALIDACAO.md).

## Contexto

O projeto parte de documentos e deve comprovar o PA-1 incrementalmente. M0 precisa de ambiente reproduzível, migrations, transações reais e CI antes de implementar propostas. Não há necessidade de criar serviços distribuídos ou módulos vazios para comprovar essa fundação.

## Decisão

| Componente | Escolha |
|---|---|
| SDK | .NET 10.0.403 x64, sem roll-forward e sem preview. |
| Hosts | ASP.NET Core API e Worker com `BackgroundService`; Worker usa host HTTP somente para saúde. |
| Banco local/CI | `gvenzl/oracle-free:23.26.3-slim-faststart`, digest `sha256:f5ff19033860d662c821cb04eb10483fa94f14f78eae252d054291ea07028093`. |
| Provider | `Oracle.EntityFrameworkCore` 10.23.26400; ODP.NET Core 23.26.400 resolvido em lockfile. |
| EF e ferramenta | EF Core Relational/Design e `dotnet-ef` 10.0.14. Relational explícito impede divergência entre dependência privada de design e runtime. |
| Correção transitiva | `System.Security.Cryptography.Pkcs` 10.0.14 explícito, evitando o mínimo 8.0.1 vulnerável resolvido pelo provider. |
| Testes | xUnit 2.9.3, runner 4.0.1, Test SDK 18.11.0 e WebApplicationFactory 10.0.14. |
| Automação | PowerShell 7 em Windows/Linux; bootstrap e verify compartilhados com GitHub Actions. |

A compatibilidade declarada de ODP.NET Core com .NET 10 consta nos [requisitos oficiais Oracle](https://docs.oracle.com/en/database/oracle/oracle-database/26/odpnt/InstallSystemRequirements.html). As versões são distribuídas pela Oracle no [NuGet](https://www.nuget.org/packages/Oracle.EntityFrameworkCore/10.23.26400). O provisionamento segue o [repositório da imagem](https://github.com/gvenzl/oci-oracle-free), incluindo usuário de schema, volume persistente e healthcheck.

Escolhemos migrations EF versionadas e um executável separado para aplicá-las. API/Worker nunca migram no startup. M0 cria somente metadados de plataforma; tabelas de negócio surgem em M1. O schema `PC_OWNER` pertence ao migrator, e `PC_APP` tem conexão e leitura explícita dos metadados. Concessões de escrita serão acrescentadas nas migrations dos módulos.

Migrations são serializadas pelo fluxo de preparação/deploy. O DDL Oracle não oferece rollback completo por transação; falha parcial requer diagnóstico e reparo, não promessa de atomicidade. Os testes de commit/rollback verificam DML real em conexões distintas.

Liveness exclui dependências; readiness exige Oracle e histórico de migrations compatível, incluindo recusa de schema mais novo. A separação segue o [modelo de health checks ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0). A saída é sanitizada, sem exceções Oracle/connection strings em respostas públicas.

## Consequências

- Requer Docker Linux, recursos para Oracle e PowerShell 7. Oracle não é substituído por SQLite/in-memory nos testes de integração.
- Lockfiles NuGet, SDK e digest de imagem tornam upgrades explícitos. Auditoria NuGet continua habilitada; vulnerabilidades não foram suprimidas para aprovar o build.
- Não há entidades, API de propostas ou mensageria de negócio no M0. Os módulos do PA-1 mantêm propriedade das escritas conforme forem implementados.
- Credenciais locais são aleatórias e ficam fora do Git. Não há licença de distribuição por decisão do mantenedor neste marco.
- A execução remota do CI e as proteções de `main` precisam de evidência no GitHub; validação local não substitui esse aceite.
