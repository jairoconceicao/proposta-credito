# Backlog de implementação — Proposta Crédito

Referência: [plano de implementação](PLANO-IMPLEMENTACAO.md), [produto](../PROPOSTA.md) e [PA-1](architecture/pa-1.md).  
Data: 30 de outubro de 2026. Todos os itens estão pendentes; os IDs abaixo são locais, não números de Issues já criadas.

## Como executar

Cada item pode virar uma Issue com o mesmo ID, objetivo, dependências e aceite. Marcar `[x]` somente após evidências e revisão. Registrar bloqueios e links para PRs na própria tarefa. A ausência de dependência entre itens permite trabalho independente; não exige execução simultânea.

Prioridades: **P0** funda o projeto e entrega a primeira decisão; **P1** completa concessão resiliente e homologação; **P2** amplia pagamentos, fechamento e operação. Prioridade não elimina dependências nem adia testes necessários a uma entrega.

## M0 — Fundação e CI

### [ ] PC-01 — Preparar convenções do repositório

**Prioridade:** P0 · **Depende de:** nenhuma.

- Criar `.gitignore`, `.editorconfig`, templates de Issue/PR e convenções de contribuição com branches `codex/` para alterações assistidas.
- **Aceite:** build, segredos e artefatos locais ignorados; template solicita critérios e evidências; README distingue implementado de planejado. Registrar escolha de licença pelo mantenedor antes de adicioná-la.
- **Verificação:** revisar arquivos rastreados e simular preenchimento dos templates.

### [ ] PC-02 — Validar stack e registrar ADR inicial

**Prioridade:** P0 · **Depende de:** nenhuma.

- Comprovar SDK .NET, provedor Oracle, acesso a dados e mecanismo de migrations; selecionar imagens reproduzíveis e estratégia de testes.
- **Aceite:** ADR registra versões, comandos e resultado de conexão/transação; `global.json` fixa SDK compatível. Reafirmar módulos, propriedade de escritas e transações do PA-1.
- **Verificação:** prova executável com Oracle, incluindo commit e rollback; não adotar compatibilidade apenas presumida.

### [ ] PC-03 — Criar solução .NET e projetos iniciais

**Prioridade:** P0 · **Depende de:** PC-02.

- Criar API, Worker, projetos de testes e estrutura mínima dos módulos usados na primeira entrega.
- **Aceite:** restore/build/test executáveis; API e Worker iniciam; configuração por ambiente e cancelamento no encerramento funcionam. Primeiro teste verifica comportamento real, sem teste vazio para deixar CI verde.
- **Verificação:** build e teste a partir de checkout limpo; smoke test dos processos.

### [ ] PC-04 — Provisionar Oracle local e migrations

**Prioridade:** P0 · **Depende de:** PC-02, PC-03.

- Criar configuração local de infraestrutura, exemplo de variáveis sem segredos, schema versionado e usuário da aplicação com permissões apropriadas.
- **Aceite:** instância vazia recebe migrations e uma segunda aplicação não repete alterações; credenciais não são versionadas; readiness aguarda banco utilizável.
- **Verificação:** integração Oracle real com persistência e rollback; reproduzir preparação do zero em ambiente descartável.

### [ ] PC-05 — Automatizar preparação e verificação local

**Prioridade:** P0 · **Depende de:** PC-03, PC-04.

- Implementar `scripts/bootstrap.ps1` e `scripts/verify.ps1`; documentar pré-requisitos, preparação, execução e diagnóstico. Adicionar logs estruturados e health checks iniciais.
- **Aceite:** scripts falham com código diferente de zero em erro; execução repetida é previsível; readiness verifica schema/Oracle e liveness independe de terceiros. Segredos e dados de negócio não aparecem nos logs.
- **Verificação:** executar comandos documentados e provocar dependência ausente/configuração inválida.

### [ ] PC-06 — Implementar CI e checks de PR

**Prioridade:** P0 · **Depende de:** PC-03 para início; PC-04 e PC-05 para conclusão.

- Criar GitHub Actions para PRs e `main`: restore, build, testes unitários, integração Oracle e relatórios; auditar/configurar checks obrigatórios de `main` conforme disponibilidade.
- **Aceite:** primeira execução remota aprovada; teste quebrado torna workflow vermelho; integração não é silenciosamente ignorada. PRs sem confiança não recebem segredos privilegiados. Proteções aplicadas ou limitação registrada.
- **Verificação:** guardar link da execução remota, resultado da falha controlada e evidência das configurações; validação YAML local sozinha não conclui a tarefa.

## M1 — Cadastro e propostas

### [ ] PC-07 — Definir identidade e autorização por empresa

**Prioridade:** P0 · **Depende de:** PC-03, PC-04.

- Definir autenticação, claims, vínculo identidade/empresa e papéis de administrador, empresa, analista e operador. Disponibilizar identidades fictícias de laboratório.
- **Aceite:** empresa deriva da identidade autenticada; credenciais inválidas são recusadas; permissões mínimas por operação. Autenticação de desenvolvimento não é habilitada em homologação.
- **Verificação:** testes de autenticação, escopo entre empresas e configuração por ambiente.

### [ ] PC-08 — Implementar cadastro de empresas

**Prioridade:** P0 · **Depende de:** PC-04, PC-07.

- Implementar `POST /empresas` idempotente para administrador e `GET /empresas/{id}` autorizado; normalizar e validar CNPJ conforme contrato especificado.
- **Aceite:** CNPJ único; repetição não duplica cadastro; dados inválidos são rejeitados; vínculo de identidade é estabelecido antes de receber propostas.
- **Verificação:** testes de contrato HTTP, unicidade Oracle, repetição e acesso indevido.

### [ ] PC-09 — Implementar envio e consulta transacional de propostas

**Prioridade:** P0 · **Depende de:** PC-05, PC-08.

- Implementar proposta, histórico, chave idempotente e outbox na mesma transação. Criar `POST /propostas` e `GET /propostas/{id}` conforme PA-1.
- **Aceite:** validar valores/prazo/moeda e empresa; retornar `202` e `Location` somente após commit; mesma chave/corpo retorna mesma resposta; corpo divergente retorna `409`. Persistir `UNIQUE(empresaId, operacao, chave)` e hash canônico.
- **Verificação:** testes HTTP e Oracle; consulta mostra estado e situação operacional. Antes do dispatcher, documentar que a análise permanece pendente.

### [ ] PC-10 — Comprovar idempotência, isolamento e rollback

**Prioridade:** P0 · **Depende de:** PC-06, PC-09.

- Exercitar os invariantes da criação com requisições concorrentes e falhas antes do commit.
- **Aceite:** uma proposta, um histórico inicial e um comando lógico por criação; mesma chave pode existir em empresas diferentes sem colisão; falha não deixa registros parciais; empresa não consulta proposta alheia.
- **Verificação:** testes determinísticos em Oracle no CI, com sincronização de concorrência e injeção controlada de falha; relatar contagens e respostas.

### [ ] PC-11 — Preparar contexto e contrato mínimo do harness

**Prioridade:** P0 · **Depende de:** PC-05, PC-06.

- Criar `AGENTS.md` com comandos/invariantes e modelo de tarefa em `harness/tasks/`, com escopo, aceite, limites e condições de parada.
- **Aceite:** contexto referencia a documentação existente; verificações são as mesmas do desenvolvimento/CI; execução registra tarefa, commit, configuração, comandos, resultados e pendências sem segredos.
- **Verificação:** preencher contrato para PC-09/PC-10 e conferir que seus critérios têm verificações independentes.

### [ ] PC-12 — Executar piloto do harness com idempotência

**Prioridade:** P0 · **Depende de:** PC-10, PC-11.

- Registrar uma execução assistida do incremento de idempotência e criar avaliações mínimas em `harness/evals/`.
- **Aceite:** verificação detecta solução defeituosa; execução para ao atingir limites; retomada usa progresso persistido; relatório distingue validado de pendente. Se PC-09 já estiver concluída, usar fixture ou branch descartável que reproduza o desafio.
- **Verificação:** evidências de aceitação, rejeição, interrupção e retomada; registrar duração/configuração e custo quando disponível, sem construir orquestrador próprio como pré-requisito.

## M2 — Primeira decisão completa

### [ ] PC-13 — Especificar as três regras de crédito

**Prioridade:** P0 · **Depende de:** PC-09.

- Documentar fórmula da parcela, arredondamento, comprometimento da receita, endividamento e limite disponível, com fixtures fictícias e versão.
- **Aceite:** exemplos de aprovação, rejeição, revisão manual e valores de fronteira têm resultados inequívocos; definir reserva concorrente quando o limite for compartilhado.
- **Verificação:** revisão dos exemplos antes do motor; transformar os casos em testes em PC-17.

### [ ] PC-14 — Provisionar RabbitMQ e contratos de mensagens

**Prioridade:** P0 · **Depende de:** PC-02, PC-05.

- Declarar exchange, quatro filas e DLQs, bindings e políticas quorum do PA-1. Versionar envelope e schemas, com identidade lógica, correlação, tentativa e replay explícitos.
- **Aceite:** mensagens persistentes, roteamento e dead-lettering configurados; versão escolhida suporta as políticas; laboratório de um nó documenta ausência de alta disponibilidade.
- **Verificação:** teste de topologia e roteamento no CI; schema inválido identificado; comprovar políticas efetivas no broker.

### [ ] PC-15 — Implementar dispatcher da outbox

**Prioridade:** P0 · **Depende de:** PC-09, PC-14.

- Reivindicar pendências por lease/token, respeitar `availableAt` e publicar com confirms e `mandatory`.
- **Aceite:** marcar enviado somente com confirm positivo e sem retorno de rota; nack/timeout/sem rota mantêm pendência; token vencido não atualiza registro; broker indisponível não perde proposta.
- **Verificação:** concorrência entre dispatchers e quedas antes/depois da publicação, incluindo publicação duplicada recuperável.

### [ ] PC-16 — Implementar preparação da análise e inbox

**Prioridade:** P0 · **Depende de:** PC-13, PC-15.

- Consumir `SolicitarAnalise`, fixar snapshot e versão das regras, gravar intenção de consulta e outbox com inbox na mesma transação. Usar inicialmente resultados determinísticos do simulador.
- **Aceite:** ACK somente após commit; `UNIQUE(consumer, messageId)` bloqueia efeitos duplicados; reentrega preserva análise e versão; HTTP externo nunca ocorre dentro da transação.
- **Verificação:** queda antes do ACK e execução concorrente da mesma mensagem; uma única análise/intenção efetiva.

### [ ] PC-17 — Implementar motor e consulta da decisão

**Prioridade:** P0 · **Depende de:** PC-13, PC-16.

- Executar regras com fixtures de risco, persistir motivos/snapshot/versão/autor e atualizar estado/histórico; aprovação gera `GerarContrato` na outbox.
- **Aceite:** uma decisão final por análise; rejeição/revisão manual não geram contrato; guarda de estado/versão e reserva de limite impedem decisões conflitantes e excesso de limite.
- **Verificação:** testes de fronteira, mensagens duplicadas/antigas e duas aprovações concorrentes pelo limite; GET não expõe payload bruto de risco. Comando de contrato fica pendente até M4.

### [ ] PC-18 — Demonstrar a primeira fatia de ponta a ponta

**Prioridade:** P0 · **Depende de:** PC-06, PC-10, PC-15, PC-16, PC-17.

- Criar demonstração reproduzível de envio → processamento → consulta, com casos aprovado, rejeitado e encaminhado para revisão.
- **Aceite:** resultado determinístico contém motivos e versão; correlação atravessa API/outbox/Worker; broker recuperado publica backlog. Identificar capacidades ainda pendentes de M3/M4.
- **Verificação:** teste ponta a ponta no CI e roteiro executável no README, sem depender de bureau externo.

## M3 — Resiliência e análise manual

### [ ] PC-19 — Implementar retries duráveis e quarentena

**Prioridade:** P1 · **Depende de:** PC-14, PC-15, PC-16.

- Persistir tentativas agendadas na outbox com a mesma identidade lógica; aplicar orçamento e unicidade por consumer/mensagem/replay/tentativa conforme PA-1.
- **Aceite:** retry só recebe ACK após persistência; inbox só conclui no sucesso; tentativas antigas não repetem execução; schema inválido/esgotamento vão à DLQ; falha ao persistir pausa consumo sem loop de requeue.
- **Verificação:** falha transitória, crash recorrente, banco indisponível e DLQ indisponível; comprovar retenção na origem.

### [ ] PC-20 — Implementar replay administrativo e monitor de pendências

**Prioridade:** P1 · **Depende de:** PC-07, PC-19.

- Criar operação autorizada para inspecionar/republicar via outbox e acompanhar estados sem progresso, inclusive mensagens sem correlação legível.
- **Aceite:** operador/motivo auditados; confirmação precede remoção da DLQ; identidade lógica preservada, novo replay registrado; inbox concluída impede repetir efeito. Capacidade da outbox limita aceitação com `503` antes de gravar nova proposta.
- **Verificação:** replay após sucesso, falha durante replay, operador sem permissão e outbox acima do limite.

### [ ] PC-21 — Criar simulador HTTP e submissão recuperável ao bureau

**Prioridade:** P1 · **Depende de:** PC-16, PC-19.

- Implementar simulador com submissão idempotente, consulta por chave, respostas pendentes/finais, timeout e falhas controladas. Job retoma intenções duráveis usando lease/token.
- **Aceite:** intenção existe antes do HTTP; chamada ocorre fora da transação; resposta perdida é reconciliada pela chave; orçamento HTTP/prazo não multiplica retries de fila; provedor sem garantia gera investigação manual.
- **Verificação:** crash após ACK antes do HTTP, timeout após aceite remoto e lease expirado durante chamada.

### [ ] PC-22 — Normalizar webhook, polling e resultados síncronos

**Prioridade:** P1 · **Depende de:** PC-21.

- Unificar validação, correlação, snapshot, finalização e outbox; autenticar webhook e validar timestamp/tamanho/schema.
- **Aceite:** callback responde sucesso após persistência, incluindo duplicata; banco indisponível permite reenvio; resultado sem correlação fica em quarentena; callback anterior à resposta de submissão é reconciliável.
- **Verificação:** webhook/polling simultâneos produzem um resultado efetivo; conteúdo conflitante é auditado; versão antiga não substitui resultado vigente.

### [ ] PC-23 — Adicionar cache Redis com fallback

**Prioridade:** P1 · **Depende de:** PC-22.

- Implementar cache-aside com chave protegida e contexto completo, TTL limitado pela validade do risco e snapshot Oracle em cache hit.
- **Aceite:** Redis fora do ar segue pelo bureau; pendência/timeout não entram no cache; falha de escrita não impede decisão; consultas iguais são coordenadas no banco, sem depender de lock Redis.
- **Verificação:** hit/miss, isolamento de contexto, Redis indisponível e vencimento durante espera na fila; risco vencido gera nova consulta ou revisão manual por prazo.

### [ ] PC-24 — Implementar decisão manual e prazo da análise

**Prioridade:** P1 · **Depende de:** PC-07, PC-17, PC-22.

- Implementar consulta autorizada da carteira e `POST /propostas/{id}/decisao-manual`, com motivo, versão esperada, idempotência e auditoria; prazo esgotado encaminha para revisão.
- **Aceite:** só analista autorizado decide; uma transição vence e concorrente recebe `409`; aprovação gera contrato na outbox; resultado tardio não reabre decisão; erro técnico não vira reprovação.
- **Verificação:** aprovação/rejeição simultâneas, repetição, escopo da carteira e callback tardio após decisão manual.

### [ ] PC-25 — Consolidar suíte de falhas da concessão

**Prioridade:** P1 · **Depende de:** PC-18, PC-20, PC-23, PC-24.

- Reunir cenários distribuídos e falhas de processo em testes reproduzíveis, com controles de tempo e sincronização explícitos.
- **Aceite:** reinício não perde efeitos confirmados; duplicatas não mudam decisões; cada pendência tem dono e caminho de recuperação; correlação permite diagnóstico.
- **Verificação:** executar no CI os cenários da matriz ao final; registrar limitações dos testes e do ambiente de um nó.

## M4 — Contratos

### [ ] PC-26 — Gerar contrato e parcelas atomicamente

**Prioridade:** P1 · **Depende de:** PC-13, PC-17, PC-19.

- Implementar handler `GerarContrato`, guarda de aprovação vigente e cálculo de parcelas conforme especificação financeira.
- **Aceite:** unicidade por proposta e por número de parcela; contrato, parcelas, estado `CONTRATADA` e inbox confirmados juntos; somas/arredondamento conferem; rejeitada não gera contrato.
- **Verificação:** duplicata, concorrência, comando fora de ordem e falha na criação de uma parcela desfazem efeitos indevidos.

### [ ] PC-27 — Expor consulta e recuperação de contratos

**Prioridade:** P1 · **Depende de:** PC-20, PC-24, PC-26.

- Expor contrato/parcelas no escopo da empresa e identificar aprovações com contrato pendente; integrar revisão manual à geração.
- **Aceite:** aprovação permanece visível durante falha operacional; recuperação não duplica contrato; demonstração diferencia geração de assinatura/desembolso, fora deste escopo.
- **Verificação:** teste ponta a ponta de aprovação automática/manual até contrato, consulta indevida e replay após sucesso.

## M5 — Pagamentos

### [ ] PC-28 — Especificar pagamentos e criar ingresso durável

**Prioridade:** P2 · **Depende de:** PC-07, PC-26.

- Definir valores parciais/excedentes, datas, alocação, estorno e payload; implementar ingresso autenticado, recibo Oracle/outbox e fila com retry/DLQ.
- **Aceite:** `UNIQUE(provedor, pagamentoExternoId)`; duplicata igual reconhecida, conteúdo divergente investigado; notificação sem parcela correlacionada vira pendência durável.
- **Verificação:** contrato HTTP, autenticação, duplicidade/concorrência e falha antes do commit.

### [ ] PC-29 — Implementar baixa e saldo em package PL/SQL

**Prioridade:** P2 · **Depende de:** PC-28.

- Criar package/procedure de alocação, lançamentos e baixa; function de saldo e trilha de auditoria com triggers restritas a esse fim.
- **Aceite:** pagamento parcial deixa saldo correto; procedure não faz commit parcial; inbox, lançamentos e saldo pertencem à mesma transação; concorrência não aplica valor duas vezes.
- **Verificação:** integração Oracle para parcial/total/excedente conforme especificação, crash antes do ACK e rollback no meio da operação.

### [ ] PC-30 — Implementar estorno e tratamento de divergências

**Prioridade:** P2 · **Depende de:** PC-20, PC-29.

- Registrar estorno compensatório, conciliar recibos pendentes e expor situação operacional no escopo autorizado.
- **Aceite:** estorno é idempotente e rastreável; registros originais são preservados; reprocessamento não duplica baixa; divergências não desaparecem silenciosamente.
- **Verificação:** pagamento sem correlação, repetição de estorno e recuperação do caso “pagamento recebido, parcela aberta”.

## M6 — Fechamento e conciliação

### [ ] PC-31 — Definir data contábil e job de fechamento

**Prioridade:** P2 · **Depende de:** PC-29, PC-30.

- Especificar corte, fuso, dias de atraso e tratamento de pagamentos tardios; implementar rotina PL/SQL e job com execução única/lease por data e versão.
- **Aceite:** reexecução não duplica lançamentos; concorrentes não fecham a mesma versão; falha registra progresso/erro e permite retomada segura.
- **Verificação:** duas execuções concorrentes, interrupção e casos de fronteira de data/atraso.

### [ ] PC-32 — Conciliar e persistir snapshot de fechamento

**Prioridade:** P2 · **Depende de:** PC-31.

- Conciliar recibos, lançamentos, parcelas e saldos; persistir totais, divergências e versão imutável do fechamento confirmado.
- **Aceite:** totais derivam dos lançamentos; divergência é investigável; pagamento tardio produz ajuste rastreável sem alterar arquivo já confirmado.
- **Verificação:** massa fictícia com valores conhecidos, divergência provocada e repetição do fechamento.

### [ ] PC-33 — Exportar arquivo com controles e publicação atômica

**Prioridade:** P2 · **Depende de:** PC-32.

- Documentar layout fictício com cabeçalho, detalhes, totais, encoding e manifesto/checksum; exportar somente snapshot confirmado.
- **Aceite:** leitor confere totais; arquivo parcial nunca fica visível como final; repetição da mesma versão é previsível; armazenamento é durável no ambiente de destino.
- **Verificação:** validar arquivo exportado, interromper gravação/publicação e repetir sem duplicar resultado.

## M7 — Entrega, qualidade e operação

### [ ] PC-34 — Definir homologação e estratégia de deploy

**Prioridade:** P1 · **Depende de:** PC-06.

- Registrar ADR com hospedagem, custos previstos, recursos de API/Worker/Oracle/RabbitMQ/Redis, rede, TLS, segredos, persistência e migração de schema.
- **Aceite:** destino e orçamento escolhidos pelo mantenedor; definir fronteira laboratório/homologação, backup, smoke tests e reversão de aplicação/schema. A tarefa não presume contratação de recursos.
- **Verificação:** revisar compatibilidade do ambiente com a stack e preparar plano executável de deploy.

### [ ] PC-35 — Implementar CD de homologação

**Prioridade:** P1 · **Depende de:** PC-18, PC-34.

- Empacotar API/Worker e implementar deploy do artefato versionado aprovado pelo CI; migrations em etapa controlada e sem corrida entre instâncias.
- **Aceite:** commit/versão implantados são identificáveis; permissões e segredos restritos; readiness/smoke test verificam o fluxo de decisão; falha interrompe promoção e permite recuperação documentada.
- **Verificação:** primeiro deploy real, redeploy da mesma versão e ensaio de falha/recuperação; guardar evidências. Expandir smoke tests conforme contratos, pagamentos e fechamento entrarem.

### [ ] PC-36 — Consolidar Sonar e checks de arquitetura

**Prioridade:** P2 · **Depende de:** PC-06, PC-17.

- Integrar análise de código e cobertura das regras críticas; verificar referências e propriedade dos módulos.
- **Aceite:** gate e critérios documentados; falha bloqueia check; código de PR sem confiança continua sem segredos privilegiados. Cobertura não substitui testes de concorrência e transação.
- **Verificação:** provocar violação de fronteira e problema de qualidade conhecido; confirmar detecção no pipeline.

### [ ] PC-37 — Consolidar telemetria e runbooks

**Prioridade:** P2 · **Depende de:** PC-25, PC-27, PC-30, PC-33.

- Integrar Datadog, dashboards/alertas e runbooks de DLQ, submissão incerta, contrato pendente, pagamento não baixado e fechamento divergente.
- **Aceite:** traces atravessam HTTP, outbox, filas e jobs; métricas cobrem idade/backlog/erros/cache; IDs ficam em logs/traces, não em labels de alta cardinalidade. Alertas distinguem negócio de falha técnica.
- **Verificação:** incidente simulado gera alerta útil e é resolvido seguindo o runbook; evidência sem dados sensíveis.

### [ ] PC-38 — Ensaiar capacidade e recuperação

**Prioridade:** P2 · **Depende de:** PC-25, PC-27, PC-35.

- Definir volume, concorrência, retenção e RPO/RTO para o ambiente escolhido; medir latência/backlog e ensaiar restauração Oracle e falha do broker.
- **Aceite:** relatar resultados e limites medidos; restaurar dados e reconciliar pendências sem efeitos duplicados; avaliar metas do PA-1 sem prometer capacidade não testada.
- **Verificação:** relatório de carga, recuperação e ajustes necessários. Um nó de broker não pode ser reportado como tolerante à perda de nó.

### [ ] PC-39 — Publicar demonstração e simular suporte

**Prioridade:** P2 · **Depende de:** PC-12, PC-33, PC-35, PC-36, PC-37, PC-38.

- Atualizar README com execução, arquitetura, demonstração, decisões e limitações; criar chamados fictícios e retrospectiva do piloto AI-SDLC.
- **Aceite:** outra pessoa reproduz fluxo completo com dados fictícios e encontra evidências; suporte registra diagnóstico/recuperação. Jira/Zendesk podem ser representados por templates locais até haver integrações definidas.
- **Verificação:** execução independente do roteiro e revisão das evidências; nenhuma integração externa é declarada pronta apenas por existir um template.

### [ ] PC-40 — Revisar aceite final e pendências

**Prioridade:** P2 · **Depende de:** PC-39.

- Conferir matriz PA-1, testes financeiros, CI/CD e documentação contra os resultados entregues.
- **Aceite:** cada cenário tem teste/evidência vinculada; desvios têm ADR ou tarefa aberta; status de marcos reflete execução. Assinatura, desembolso e produção ficam fora da demonstração até escopo específico.
- **Verificação:** revisão final rastreável, com links para PRs, execuções de pipeline e limitações remanescentes.

## Rastreabilidade dos cenários do PA-1

Esta matriz indica quem deve implementar a verificação; não comprova que ela já foi executada. Vincular teste e execução ao concluir cada item.

| Cenário de aceite | Tarefas responsáveis |
|---|---|
| Requisições simultâneas com mesma chave; corpo divergente | PC-09, PC-10 |
| Banco falha antes do commit de criação | PC-09, PC-10 |
| Broker indisponível depois do commit | PC-15, PC-18 |
| Dispatcher cai após confirmação e antes de marcar publicação | PC-15, PC-16, PC-25 |
| Consumer cai após commit e antes do ACK | PC-16, PC-25, PC-26 |
| Worker cai depois do ACK e antes do HTTP ao bureau | PC-21, PC-25 |
| Bureau aceita e resposta HTTP se perde | PC-21, PC-25 |
| Callback chega antes do retorno da submissão | PC-22, PC-25 |
| Webhook e polling finalizam juntos | PC-22, PC-25 |
| Redis indisponível ou risco vence na fila | PC-23, PC-25 |
| Resultado antigo após decisão manual | PC-24, PC-25 |
| Aprovações concorrentes disputam limite | PC-13, PC-17, PC-25 |
| Aprovação e rejeição manual concorrem | PC-24, PC-25 |
| Falha ao gerar uma parcela | PC-26, PC-27 |
| Mensagem inválida ou tentativas esgotadas | PC-19, PC-25 |
| DLQ indisponível | PC-14, PC-19, PC-25 |
| Replay depois de efeito confirmado | PC-20, PC-27 |
| Empresa tenta consultar proposta alheia | PC-07, PC-10 |

## Controle dos marcos

- [ ] M0 — Fundação e CI comprovados.
- [ ] M1 — Cadastro, propostas e piloto AI-SDLC comprovados.
- [ ] M2 — Primeira decisão de ponta a ponta comprovada.
- [ ] M3 — Resiliência e decisão manual comprovadas.
- [ ] M4 — Contratos e parcelas comprovados.
- [ ] M5 — Pagamentos e estornos comprovados.
- [ ] M6 — Fechamento e arquivo comprovados.
- [ ] M7 — CD, operação e demonstração comprovados.
