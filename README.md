# Proposta Crédito

Backend de concessão e acompanhamento de crédito com monólito modular .NET e Oracle.

## Estado atual e execução

Fundação M0 e cadastro/propostas M1 implementados: identidade por empresa, cadastro, envio/consulta, idempotência transacional, histórico/outbox e harness mínimo. Em M1, propostas ficam `RECEBIDA` aguardando processamento; motor de decisão e RabbitMQ entram em M2. CD está previsto para PC-34/PC-35. Consulte as [evidências de M0](docs/M0-VALIDACAO.md) e o [contrato M1](docs/specs/m1-cadastro-propostas.md) para distinguir implementação de validação remota.

Pré-requisitos: .NET SDK 10.0.403, PowerShell 7 e Docker com engine Linux.

```powershell
pwsh -NoProfile -File ./scripts/bootstrap.ps1
pwsh -NoProfile -File ./scripts/verify.ps1
```

Para iniciar a API: `pwsh -NoProfile -File ./scripts/run.ps1 -Service Api`. Para o Worker, use `-Service Worker` em outro terminal. Saúde da API em `http://127.0.0.1:5080/health/live` e `/health/ready`; Worker na porta 5081.

Veja [desenvolvimento, migrations e diagnóstico](docs/DEVELOPMENT.md), a [decisão da stack](docs/adr/0001-fundacao-dotnet-oracle.md) e as [convenções de contribuição](CONTRIBUTING.md). Por decisão do mantenedor, o repositório permanece sem licença por enquanto.

Para demonstrar cadastro e proposta idempotente: `pwsh -NoProfile -File scripts/demo-m1.ps1` após bootstrap/verify. Tokens locais são gerados no `.env`, sem serem impressos. Veja [autenticação e contrato HTTP](docs/specs/m1-cadastro-propostas.md) e [execução do harness](harness/README.md).

## Proposta e arquitetura

- [Plano de implementação e marcos de entrega](docs/PLANO-IMPLEMENTACAO.md)
- [Backlog de tarefas, dependências e critérios de aceite](docs/BACKLOG.md)
- [Proposta do produto e sequência de entregas](PROPOSTA.md)
- [Proposta de evolução, harness de AI-SDLC e organização no GitHub](docs/PROPOSTA-EVOLUCAO-AI-SDLC.md)
- [Arquitetura PA-1: decisões, gaps resolvidos e critérios de aceite](docs/architecture/pa-1.md)
- [Diagrama Mermaid da concessão de crédito](docs/architecture/pa-1.mmd)

## Objetivo

O objetivo deste repositorio é treinar as habilidades necessárias para a vaga de Analista Desenvolvedor .NET Core Sênior no cliente Proposta Crédito. Os requisitos para a vaga são:

- Conhecimento em .Net Core, C#;
- Visual Studio, APIs REST, Git, Redis,
- Scrum, Jira,
- Zendesk, esteiras de CI/CD,
- Ferramentas para qualidade de código (SONAR), DataDog. RabbitMQ.
- Conhecimento em Banco de dados: Oracle - Codificação/Desenvolvimento de PACKAGES, FUNCTIONS, PROCEDURES, TRIGGERS, JOBS (PL/SQL)
- Características comportamentais desejadas:
- Boa comunicação, postura, proatividade e resiliência.
- Participar do entendimento dos Projetos, analisar melhorar solução, desenvolver, acompanhar implantações das soluções desenvolvidas, garantindo a qualidade no desenvolvimento dos projetos e - resolução das ocorrências relacionadas.
- Seguindo a metodologia da empresa, com as devidas documentações relacionadas.
- Desenvolvimento de aplicações/processos utilizados em Bancos e Financeiras, desenvolvimento de sistema contábil, arquivos regulatórios,
- Backoffice, pós-vendas e motor de crédito.
