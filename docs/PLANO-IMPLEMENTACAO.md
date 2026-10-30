# Plano de implementação — Proposta Crédito

Data de referência: 30 de outubro de 2026.  
Status: fundação M0 implementada e validada localmente; aceite remoto de CI pendente.
Execução: [backlog com tarefas e critérios de aceite](BACKLOG.md).

## Ponto de partida

O repositório Git já existe, com branch `main`, histórico inicial e remoto `origin` configurado. O planejamento começou com documentação do produto e da arquitetura. A entrega M0 acrescenta aplicação, testes, Oracle local, migrations, scripts e workflow de CI. A consulta remota de 30 de outubro de 2026 identificou `main` sem proteção; execução no Actions e checks obrigatórios permanecem pendentes. Veja [evidências e limites do M0](M0-VALIDACAO.md).

Este plano operacionaliza a [proposta do produto](../PROPOSTA.md), o [PA-1](architecture/pa-1.md) e a [proposta de evolução e AI-SDLC](PROPOSTA-EVOLUCAO-AI-SDLC.md). A sequência abaixo organiza os incrementos; não substitui seus contratos e invariantes.

## Resultado esperado

Construir um backend demonstrável que permita cadastrar uma empresa, enviar uma proposta, consultar uma decisão explicável, gerar contrato e parcelas, registrar pagamentos e executar fechamento conciliado. Cada etapa deve ter testes, instruções de execução e evidências no CI.

O primeiro resultado de negócio é enviar uma proposta, avaliá-la com três regras determinísticas e consultar os motivos. Cadastro e idempotência formam uma entrega anterior, utilizável e verificável, mesmo enquanto o processamento ainda estiver pendente.

## Diretrizes

- Manter monólito modular com API e Worker; módulos de Propostas, Crédito, Contratos, Pagamentos e Fechamento. Criar componentes quando a entrega exigir, evitando camadas vazias.
- Oracle é a fonte de verdade. Redis é cache; RabbitMQ transporta mensagens. Persistência, histórico, inbox/outbox e efeito de domínio devem respeitar as transações do PA-1.
- Fixar versões somente após verificar compatibilidade entre SDK, bibliotecas, Oracle e imagens de infraestrutura. Registrar a escolha e a comprovação em ADR.
- Usar dados fictícios. Regras de crédito são determinísticas e versionadas; IA auxilia a engenharia, não decide a concessão.
- Começar CI, logs estruturados e autorização junto da aplicação. Acrescentar testes de falha quando cada componente entrar, sem concentrar qualidade no último marco.
- Registrar decisões que alterem o PA-1 em `docs/adr/`; manter contratos e testes coerentes.

## Marcos e critérios de saída

| Marco | Tarefas | Entrega | Critério de saída |
|---|---|---|---|
| M0 — Fundação e CI | PC-01 a PC-06 | Solução .NET, Oracle local, migrations, scripts e GitHub Actions | Checkout limpo prepara o ambiente, compila e executa testes unitários e de integração; falhas impedem aprovação do CI. |
| M1 — Cadastro e propostas | PC-07 a PC-12 | Identidade por empresa, cadastro, envio/consulta, histórico, idempotência e piloto AI-SDLC | Requisições repetidas/concorrentes criam uma proposta e uma outbox; rollback não deixa efeitos; acesso entre empresas é bloqueado. |
| M2 — Primeira decisão completa | PC-13 a PC-18 | Regras versionadas, RabbitMQ, dispatcher, handlers e simulador determinístico | Uma proposta percorre API → Oracle/outbox → Worker → decisão consultável, com motivos, snapshot e versão. |
| M3 — Resiliência e análise manual | PC-19 a PC-25 | Retries, DLQs, bureau HTTP, polling/webhook, cache e decisão manual | Falhas e duplicatas são recuperáveis; resultados antigos não sobrescrevem decisões; indisponibilidade técnica não vira rejeição de crédito. |
| M4 — Contratos | PC-26 e PC-27 | Contrato e parcelas após aprovação | Um contrato por proposta; falha em parcela desfaz toda a criação; reentrega não duplica efeitos. |
| M5 — Pagamentos | PC-28 a PC-30 | Ingresso durável, baixa parcial e lançamentos PL/SQL | Pagamentos duplicados não alteram saldo duas vezes; estornos e divergências deixam trilha. |
| M6 — Fechamento | PC-31 a PC-33 | Job diário, conciliação e arquivo | Reexecução não duplica lançamentos; totais conferem; arquivo corresponde a um snapshot confirmado. |
| M7 — Entrega e operação | PC-34 a PC-40 | CD, telemetria, qualidade, recuperação e demonstração | Deploy de homologação verificável, recuperação ensaiada, documentação e evidências dos cenários críticos. |

M7 é transversal: decidir hospedagem (PC-34) após M0 e habilitar CD de homologação (PC-35) após M2. Instrumentação básica começa em M0/M1; dashboards crescem com cada componente. A conclusão de M7 ocorre após as entregas funcionais.

## Primeira sequência de trabalho

1. PC-01 e PC-02: organizar contribuições e comprovar a combinação de SDK/provedor/banco.
2. PC-03 e PC-06: criar solução e CI de build/testes assim que houver código verificável.
3. PC-04 e PC-05: preparar Oracle, migrations e scripts; completar PC-06 com integração real.
4. PC-07 e PC-08: identidade de desenvolvimento e cadastro de empresas.
5. PC-09 e PC-10: proposta transacional e testes de idempotência, concorrência e rollback.
6. PC-11 e PC-12: contexto e piloto mínimo do harness usando as verificações existentes.
7. PC-13 a PC-18: implementar o caminho completo até a decisão.

PC-06 pode começar após PC-03, mas só termina com os testes Oracle de PC-04/PC-05. Não declarar M0 concluído com testes de integração ignorados ou substituídos por um banco em memória.

## Recorte dos primeiros PRs

| PR sugerido | Conteúdo | Evidência esperada |
|---|---|---|
| 1 — Estrutura e CI inicial | PC-01, PC-02, PC-03 e início de PC-06 | SDK fixado, build e primeiro teste significativo executados no CI. |
| 2 — Oracle reproduzível | PC-04, PC-05 e conclusão de PC-06 | Banco vazio recebe migrations; scripts e integração funcionam localmente e no runner. |
| 3 — Empresas e acesso | PC-07 e PC-08 | Testes de identidade, escopo, CNPJ e duplicidade. |
| 4 — Propostas idempotentes | PC-09 e PC-10 | Contrato HTTP e provas de unicidade, isolamento e atomicidade no Oracle. |
| 5 — Piloto AI-SDLC | PC-11 e PC-12 | Verificações independentes e registro de uma execução da tarefa de idempotência. |

Os PRs são sugestões de recorte. Uma tarefa grande deve virar subtarefas antes da implementação; cada PR precisa preservar comportamento coerente e verificável. O piloto pode acompanhar o PR de idempotência se o contexto já estiver preparado.

## CI e CD

**CI:** executar em PRs e pushes para `main`, com restore, build, testes unitários, integração Oracle e relatórios de falha. Adicionar RabbitMQ e Redis aos testes conforme as respectivas entregas. O script local de verificação e o CI devem executar as mesmas verificações essenciais. Validar limites de recursos e tempo do runner na primeira execução real.

**CD:** PC-34 define provedor, custos, ambiente de homologação, armazenamento, rede, segredos e estratégia de migrations/recuperação. PC-35 produz artefatos versionados, promove a versão aprovada pelo CI, executa smoke tests e demonstra recuperação. O plano não escolhe um provedor nem provisiona recursos pagos. Produção exige decisão própria de capacidade, disponibilidade e política de dados.

Proteções de `main` e checks obrigatórios serão configurados e verificados em PC-06, respeitando as funcionalidades disponíveis no repositório. Publicação e deploy são atividades futuras das tarefas; este planejamento não os executa.

## Definição de pronto

- Critérios da tarefa demonstrados por testes ou evidências de execução adequadas ao comportamento.
- Build e verificações pertinentes aprovados; pendências registradas explicitamente, sem marcar a tarefa concluída.
- Migrations, contratos de mensagens e regras versionados quando aplicável; falhas não deixam efeitos financeiros parciais.
- Autorização, idempotência e concorrência verificadas nos limites afetados.
- Logs e traces permitem investigação sem expor dados sensíveis; credenciais ficam fora do Git.
- README, especificações, ADRs e procedimentos operacionais atualizados conforme a mudança.
- PR descreve problema, comportamento resultante, validação e limitações; revisão humana antes do merge.

## Decisões e riscos a resolver

| Assunto | Ação / tarefa |
|---|---|
| SDK, acesso a dados, migrations e imagens compatíveis | Executar prova mínima e registrar versões em PC-02; validar Oracle local/CI em PC-04 e PC-06. |
| Fórmulas, arredondamento e limites de aprovação | Especificar exemplos e fronteiras em PC-13 antes do motor. |
| Identidade e vínculo empresa/usuário | Definir contrato em PC-07; mecanismo de laboratório deve ficar restrito ao desenvolvimento. |
| Banco e containers no runner | Medir prontidão e consumo de recursos em PC-06; documentar alternativa de runner se necessário. |
| Custo e destino de hospedagem | Resolver em PC-34 antes do CD; não presumir Azure ou outro provedor. |
| Garantias do bureau | Simulador precisa de chave idempotente e consulta por chave em PC-21. |
| Regras financeiras e data contábil | Definir pagamento parcial, excedente, estorno e datas em PC-28/PC-31. |
| Operação real | Definir retenção, backup, RPO/RTO, volume e disponibilidade em PC-38; demonstração local não comprova prontidão de produção. |

Não há prazo fechado: a primeira rodada de M0/M1 deve fornecer dados para estimar as demais entregas. Prioridade e dependências estão no backlog. O arquivo local é a referência inicial das tarefas; publicação como Issues ou em uma ferramenta de gestão pode preservar os IDs `PC-xx`.
