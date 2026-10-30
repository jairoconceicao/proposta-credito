# M2 — Evidências da primeira decisão assíncrona

Data: 30 de outubro de 2026. Escopo local: PC-13 a PC-18.

## Entregue

- Regras imutáveis `lab-v1`, parcela/arredondamento, fronteiras, motivos e fixtures determinísticas documentados e testados.
- RabbitMQ 4.2.9 por digest, oito filas quorum, exchanges/bindings, policies de dead lettering e limites conferidos no broker real.
- Dispatcher com lease/token, confirms rastreados, publicação persistente e `mandatory`; sem rota ou falha mantém outbox pendente.
- Três consumidores com ACK após commit, inbox idempotente e transições protegidas. Intenção durável e job local retomável geram risco fictício.
- Snapshot/versionamento, avaliação explicável, decisão final única, revisão manual histórica e reserva concorrente do limite compartilhado.
- GET autorizado expõe motivos, versão e instantes sem payload bruto do risco. Aprovação agenda contrato, pendente para M4.
- API/Worker reais demonstram os três resultados e recuperação de proposta aceita durante indisponibilidade do broker.

## Verificações executadas

| Verificação | Resultado |
|---|---|
| Restore/build Release | Aprovados, sem avisos/erros de compilação. |
| Testes unitários | 38 aprovados, 0 ignorados: inclui fronteiras das três regras, arredondamento, precedência e contrato de mensagens. |
| Integração Oracle/RabbitMQ | 27 aprovados, 0 ignorados; inclui as garantias anteriores de M0/M1. |
| Topologia real | 8 filas quorum, 5 policies efetivas e 8 bindings de negócio/DLQ conferidos no bootstrap. Usuário runtime sem tag administrativa. |
| Dispatcher | Dois concorrentes reivindicam uma pendência; token vencido/antigo não marca publicação; retorno por falta de rota mantém pendência e posterior publicação confirma. |
| Falha após confirm | Repetição produz entregas duplicadas; apenas uma análise, intenção e próxima outbox. |
| Commit antes de ACK | Fechamento do canal sem ACK nas três etapas provoca redelivery real e não duplica decisão/contrato agendado. |
| Quarentena | Schema não suportado é recusado, recebe nack sem requeue e chega à DLQ com metadados `x-death`. Nenhuma inbox/decisão indevida. |
| Limite compartilhado | Duas propostas concorrentes de 30000 para limite 50000 produzem uma aprovação; reserva final 30000. |
| Snapshot/ordem | Alteração posterior da entrada não substitui snapshot fixado; mensagem/análise antiga é auditada e ignorada. Reentrega preserva regra e resultado. |
| Risco vencido/prazo | Risco vencido gera nova consulta; prazo total esgotado encaminha para revisão sem decisão final nem contrato. |
| Falha antes de commit da decisão | Reserva, inbox, decisão e comando de contrato desfeitos; nova entrega conclui normalmente. |
| Hosts reais | API/Worker com live/ready; três resultados consultáveis com versão/motivos. Broker parado deixa Worker não pronto e API pronta; proposta persistida progride após reinício. |
| Harness | Sete avaliações controladas e piloto M2 completo aprovados. |

A migration foi aplicada sobre o banco M1 existente, incluindo backfill de contas de limite; o RabbitMQ foi provisionado vazio. Uma instância Oracle totalmente nova não foi criada nesta execução. O CI usa bootstrap com volumes descartáveis, mas não há afirmação de execução remota desta alteração.

## Execução do harness

Contrato: `harness/tasks/PC-13-primeira-decisao.json`. RunId: `m2-pilot-20261030`.

- Commit base: `2b2325d70caf94dde63752845da915b17c7efd7a`, com alterações M2 ainda não commitadas.
- Fingerprint do snapshot executado: `CC542F84FEE57D67FCBFE42F18CC8DA673BCA3E0FB8F03E29EC9C11F53E07404`.
- Etapas `verify` e `diff-check` aprovadas na primeira tentativa; status final `passed`.
- Duração ativa acumulada: aproximadamente 98 segundos. Modelo/configuração detalhada e custo não disponíveis permanecem desconhecidos.
- Checkpoint/logs sanitizados: `artifacts/harness/runs/m2-pilot-20261030/`.
- Avaliações dessa execução: `artifacts/harness/evals/eval-df06eb53e04f41e4a077eea202ef821c/`.
- TRXs: `artifacts/test-results/`; demonstração: `artifacts/m2-demo/results.json`.

Este relatório e o status do backlog foram escritos após o piloto; o fingerprint identifica o snapshot executado, sem pretender incluir um hash autorreferente deste relatório. Artefatos permanecem fora do Git; CI guarda resultados/checkpoints sanitizados.

## Ambiente e limites

Durante a preparação, falta de espaço no host interrompeu inicializações. Com autorização do mantenedor, foram removidos 21,59 GB de cache de build Docker inativo, preservando imagens e dados existentes. Oracle voltou saudável com seu volume preservado. Somente o volume RabbitMQ recém-criado, ainda sem mensagens, precisou ser reconstruído por conter metadados incompletos da primeira inicialização. O bootstrap normal não apaga volumes nem executa limpeza global.

Persistem limites explícitos: um nó de laboratório sem alta disponibilidade; bureau somente local; sem Redis, retry durável por tentativa, replay administrativo ou contrato gerado. Essas capacidades pertencem a M3/M4. Falha técnica não rejeita crédito. Reservas não expiram automaticamente. TLS é obrigatório no Worker fora de Development; conexão real a broker TLS/IdP externo faz parte do ambiente de implantação, não foi provisionada aqui.

Execução GitHub Actions e proteções remotas continuam separadas do aceite local, conforme PC-06. Não há promessa de capacidade, disponibilidade ou prontidão para produção baseada nesses testes.
