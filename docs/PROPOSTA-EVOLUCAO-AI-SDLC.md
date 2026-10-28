# Proposta de evolução do Proposta Crédito e construção de um harness de AI-SDLC

Data: 28 de outubro de 2026  
Status: proposta para implementação  
Base: revisão da estrutura e da documentação local do Proposta Crédito

## 1. Objetivo

Evoluir o Proposta Crédito de uma proposta documental para um backend executável e verificável, com desenvolvimento organizado no GitHub e um harness de AI-SDLC integrado ao fluxo de engenharia.

AI-SDLC significa, nesta proposta, o uso de inteligência artificial no ciclo de desenvolvimento de software. O harness fornece contexto, prepara o ambiente, controla a execução, executa verificações e registra evidências das alterações produzidas com assistência de IA.

As três frentes devem avançar juntas: primeira entrega funcional, repositório GitHub com integração contínua e harness mínimo apoiado nas mesmas verificações usadas pelos desenvolvedores.

## 2. Diagnóstico da revisão

Na revisão inicial, a pasta continha quatro documentos e um `debug.log`, sem código de aplicação, testes, configuração de ambiente ou pipeline. Os comandos `git status` e `git remote` indicaram que a pasta não era um repositório Git. Essa constatação local não comprova a existência ou ausência de um repositório remoto separado no GitHub.

| Avaliação | Evidência | Implicação |
|---|---|---|
| Problema de negócio definido | [Proposta do produto](../PROPOSTA.md) | Concessão, contratos, pagamentos e conciliação estão ligados a situações concretas. |
| Arquitetura cuidadosa com falhas | [PA-1](architecture/pa-1.md) | Transações, outbox/inbox, duplicidade, concorrência e recuperação já têm diretrizes documentadas. |
| Garantias ainda sem comprovação | Cenários de aceite do PA-1 | Correção, desempenho e segurança precisam de implementação e evidências de execução. |
| Qualidade concentrada em uma entrega posterior | Entrega 6 da proposta do produto | Build, testes e logs básicos devem começar na primeira entrega. |
| Apresentação voltada a uma vaga | [README](../README.md) | Para portfólio, destacar também produto, decisões, execução e evidências técnicas. |

O principal risco é ampliar a arquitetura antes de comprovar o fluxo básico de negócio.

## 3. Direção técnica

Manter o monólito modular, com API e Worker, e o Oracle como persistência principal. Implementar o PA-1 por incrementos, explicitando o que cada entrega já garante e o que continua pendente.

Para um projeto novo na data desta proposta, avaliar .NET 10 LTS, com SDK fixado em `global.json` e compatibilidade do provedor Oracle verificada antes da adoção. O suporte do .NET 10 vai até novembro de 2028, conforme a [política oficial do .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

Antes da implementação do motor de decisão, definir exemplos executáveis para:

- Fórmula da parcela e regra de arredondamento.
- Percentual máximo de comprometimento da receita.
- Teto de endividamento e cálculo do limite disponível.
- Limites inclusivos ou exclusivos e tratamento dos valores de fronteira.
- Entradas fictícias, versão das regras e resultados esperados.

O uso de IA proposto é voltado à engenharia de software. O motor de crédito continua seguindo as regras determinísticas e versionadas previstas no PA-1.

## 4. Plano de entregas

| Ordem | Entrega | Critério de conclusão |
|---|---|---|
| 1 | Fundação Git/GitHub | Histórico inicial, `.gitignore`, README atualizado, licença escolhida, Issues e modelo de PR. Revisar arquivos locais de diagnóstico antes de publicar. |
| 2 | Ambiente reproduzível e CI | Solução .NET, API, estrutura inicial dos módulos, Oracle local, migrations e scripts de preparação/verificação. Um checkout limpo consegue executar os testes. |
| 3 | Cadastro e propostas | Criar e consultar propostas com autorização por empresa, validação, histórico e idempotência persistida no Oracle. |
| 4 | Primeira fatia completa | Enviar proposta, processar, aplicar três regras e consultar a decisão com seus motivos, usando dados fictícios determinísticos. |
| 5 | Resiliência do PA-1 | Comprovar outbox/inbox, reentrega, concorrência, recuperação e DLQ conforme cada componente entrar. |
| 6 | Expansão funcional | Acrescentar bureau com falhas simuladas, cache, decisão manual, contratos, pagamentos e fechamento PL/SQL. |

Cada incremento deve incluir os testes das garantias que oferece. A etapa de resiliência amplia os cenários de falha; ela não adia as verificações necessárias às entregas anteriores.

Alterações na sequência ou nos mecanismos previstos no PA-1 devem ser registradas em decisões de arquitetura, mantendo documentação e implementação coerentes.

## 5. Harness mínimo de AI-SDLC

### 5.1. Fluxo de trabalho

Issue com critérios de aceite → contexto selecionado → branch isolada → implementação assistida → verificações → PR com evidências → revisão humana.

O arquivo `AGENTS.md` orienta o agente, mas não executa nem comprova as etapas. O runner e o CI devem produzir resultados verificáveis.

### 5.2. Estrutura inicial proposta

Os caminhos abaixo representam arquivos e diretórios a implementar; não indicam componentes já existentes.

```text
AGENTS.md
docs/
  adr/
  specs/
scripts/
  bootstrap.ps1
  verify.ps1
harness/
  tasks/
  evals/
.github/
  workflows/
  ISSUE_TEMPLATE/
  pull_request_template.md
```

### 5.3. Componentes e responsabilidades

| Componente | Responsabilidade |
|---|---|
| Contexto | `AGENTS.md` curto com comandos, limites dos módulos, invariantes e links para documentação detalhada. |
| Contrato da tarefa | Objetivo, critérios de aceite, escopo permitido, verificações exigidas e condições de parada. |
| Execução controlada | Limites de tempo, tentativas e custo; ambiente isolado e credenciais restritas ao necessário. |
| Verificação independente | Build, testes e regras de arquitetura executados pelo runner/CI; a declaração de sucesso do agente não é suficiente. |
| Registro da execução | Tarefa, commit, modelo/configuração, comandos, resultados, duração e pendências, com dados sensíveis removidos. |
| Retomada | Progresso persistido para continuar após interrupções e reconhecer o que ainda não foi validado. |

Começar com um fluxo simples, usando o agente de desenvolvimento disponível e scripts de verificação. A necessidade de um orquestrador próprio deve ser demonstrada pelo uso.

Trabalho incremental, preparação reproduzível e registros de progresso são práticas discutidas em [Effective harnesses for long-running agents, da Anthropic](https://www.anthropic.com/research/effective-harnesses-for-long-running-agents).

### 5.4. Primeiro piloto: idempotência de propostas

Usar a implementação de idempotência de `POST /propostas` como primeira tarefa do harness, com os seguintes critérios:

1. Mesma chave e mesmo corpo retornam a mesma proposta.
2. Mesma chave com corpo diferente retorna `409 Conflict`.
3. Requisições concorrentes produzem apenas uma proposta.
4. Falha antes do commit não deixa efeitos parciais.

Além dos testes da aplicação, criar avaliações do próprio harness:

- Detectar uma solução incorreta diante dos critérios de aceite.
- Interromper a execução após falhas repetidas ou esgotamento dos limites definidos.
- Retomar uma execução interrompida a partir do estado registrado.
- Produzir evidências suficientes para revisão da alteração.

Acompanhar taxa de tarefas aceitas, regressões, retrabalho humano, duração e custo por tarefa aceita. Registrar a configuração usada para permitir comparações entre execuções.

## 6. Organização no GitHub

### 6.1. Repositórios

Começar com um único repositório `proposta-credito`, contendo aplicação, infraestrutura local, documentação e harness. Isso permite evoluir os critérios de aceite e as verificações junto com o código.

Depois de reutilizar o harness em outro projeto, avaliar a extração da parte genérica para um segundo repositório, como `ai-sdlc-harness`. As regras e avaliações específicas de crédito permanecem no Proposta Crédito.

### 6.2. Configuração inicial

- Branch `main` com PR obrigatório e verificações exigidas.
- Branches curtas e Issues com critérios de aceite.
- CI de build, testes unitários e testes de integração com Oracle.
- Modelo de PR com problema, comportamento resultante e validação.
- README com instruções reproduzíveis, demonstração e limitações conhecidas.
- Escolha explícita de licença e revisão dos arquivos antes da publicação.

Rulesets estão disponíveis em repositórios públicos no GitHub Free; para repositórios privados, a disponibilidade depende do plano. Consultar a [documentação de rulesets do GitHub](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/about-rulesets) ao configurar as proteções.

Para executar o harness no GitHub Actions, adotar permissões mínimas e manter código de PR sem acesso a segredos privilegiados, conforme a [referência de segurança do GitHub Actions](https://docs.github.com/en/actions/reference/security/secure-use).

## 7. Primeira meta concreta

Produzir um PR verificável com:

1. Ambiente local documentado e reproduzível.
2. Estrutura inicial da aplicação e migrations do Oracle.
3. Cadastro e consulta de propostas com autorização por empresa.
4. Idempotência persistida e testes de duplicidade, concorrência e rollback.
5. Pipeline de build e testes.
6. Execução piloto do harness com registro das evidências e pendências.

O aceite exige que outra pessoa consiga preparar o ambiente a partir das instruções e executar as verificações. A primeira fatia com decisão de crédito vem no incremento seguinte.

## 8. Limites desta proposta

Este documento registra recomendações da revisão local. Sua criação não inicializa Git, não cria repositório remoto e não configura workflows ou o harness.

A revisão não validou código de aplicação, desempenho ou segurança em execução, pois não havia aplicação disponível. As garantias documentadas devem ser comprovadas pelas entregas e pelos testes correspondentes.
