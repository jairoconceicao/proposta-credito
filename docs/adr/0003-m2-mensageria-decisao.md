# ADR 0003 — Decisão assíncrona de M2

Data: 30 de outubro de 2026. Status: adotado, com validação local.

## Decisões

- RabbitMQ 4.2.9 management, imagem oficial fixada por digest; cliente .NET 7.2.2 fixado em lockfile. Topologia declarada por definitions, conferida por CLI no bootstrap. Usuário runtime sem tag administrativa e com permissões restritas ao namespace do projeto; management HTTP não é exposto no host. Worker exige TLS fora de Development.
- Publicações persistentes com confirms rastreados pelo cliente e `mandatory`. Basic return também faz a publicação falhar; apenas confirmação positiva sem retorno permite marcar outbox. Leases são atualizados por token, sem locks Oracle durante a chamada ao broker.
- Consumidores de ACK manual. Uma transação serializa a proposta, valida a análise vigente e consulta inbox antes de aplicar efeitos. ACK após commit; queda antes do ACK pode redeliver e não repete o domínio.
- Em M2, erros técnicos fecham a sessão sem ACK e há pausa antes de reconectar. Schema inválido vai para DLQ. Orçamento de retry durável por mensagem, replay administrativo e alertas mais completos pertencem a PC-19/PC-20; reconexão não é apresentada como essa implementação.
- Preparação grava snapshot e versão `lab-v1`; submissão grava intenção pronta. Job determinístico processa intenção localmente, sem HTTP, e confirma resultado/outbox juntos. M3 substitui a integração por simulador HTTP com leases/reconciliação, mantendo o contrato durável.
- Avaliação usa snapshot fixado da proposta, resultado de risco vigente e limite compartilhado. Reserva condicional do limite, decisão, histórico, inbox e comando de contrato são atômicos. Revisão manual é avaliação histórica, sem decisão final adicional. Decisões não reabrem.
- Risco vencido gera nova consulta preservando a versão; prazo total esgotado gera revisão técnica. Reserva aprovada não expira/libera automaticamente, conforme limite do PA-1.
- API continua pronta com Oracle saudável e broker indisponível; Worker tem prontidão adicional de broker/topologia, liveness independente. Limite operacional de backlog da API entra em M3.

## Regras e escopo

[Contrato lab-v1](../specs/m2-decisao-credito.md) especifica fórmulas, fronteiras, fixtures e precedência de resultados. Não há juros nem política real de concessão. Contrato/assinatura/desembolso não são gerados neste incremento: aprovação publica apenas o comando pendente para M4.

Um nó quorum persistente não oferece alta disponibilidade. Policies explícitas de `at-least-once` e `reject-publish` protegem dead lettering sob limites de capacidade, mas exigem operação/alertas e consumidores administrativos em marcos seguintes. Snapshot e auditoria ficam no Oracle; logs propagam IDs/correlação e tipos de erro, sem payload/valores.

Referências oficiais: [publisher confirms .NET](https://www.rabbitmq.com/client-libraries/dotnet-api-guide#publisher-confirms), [quorum e dead lettering](https://www.rabbitmq.com/docs/4.2/quorum-queues#dead-lettering), [definitions](https://www.rabbitmq.com/docs/definitions), [PA-1](../architecture/pa-1.md).
