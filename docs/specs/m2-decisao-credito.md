# M2 — Primeira decisão assíncrona

## Regras laboratoriais v1

Versão imutável: `lab-v1`. Valores fictícios; não constituem política financeira real. BRL, valores decimais e arredondamento de parcela em duas casas, `MidpointRounding.AwayFromZero`. Parcela = valor solicitado / prazo, sem juros neste laboratório.

| Regra | Aprovar | Análise manual | Rejeitar |
|---|---|---|---|
| Receita | Parcela até 25% do faturamento mensal | Acima de 25%, até 30% inclusive | Acima de 30% |
| Endividamento | Dívida existente até 40% do faturamento | Acima de 40%, até 50% inclusive | Acima de 50% |
| Limite disponível | Valor até o limite total menos reservas confirmadas | — | Valor acima do disponível |

Rejeição prevalece sobre revisão; revisão prevalece sobre aprovação. Motivos são códigos estáveis de cada regra. Cada avaliação grava snapshot da proposta, risco/validade, disponível utilizado, versão e motivos. Decisões finais são únicas por análise; encaminhamento manual é avaliação histórica, sem decisão final nem contrato.

Simulador `risco-lab-v1`: dívida existente de 1000 BRL e limite total compartilhado de 50000 BRL por empresa. Validade: cinco minutos. Intenção Oracle é gravada antes do job; o resultado e a outbox são confirmados juntos. O job local não faz HTTP. Resultado vencido gera nova consulta enquanto o prazo total de 15 minutos permitir; prazo esgotado encaminha para revisão técnica, sem rejeição de crédito.

Exemplos: faturamento 10000, prazo 12, valores 1000/40000/33000 produzem respectivamente aprovação/rejeição/revisão. Parcela de 2500 passa a regra de receita; 2500.01 exige revisão; 3000 ainda exige revisão; 3000.01 rejeita. Dívida 4000/4000.01/5000/5000.01 segue as mesmas fronteiras 40%/50%. Valor igual ao disponível é permitido.

Na aprovação, uma atualização condicional reserva o valor somente se o saldo disponível continuar suficiente. Duas propostas de 30000 para uma empresa de limite 50000 não podem ser aprovadas juntas. A reserva, decisão, histórico, inbox e comando de contrato pertencem à mesma transação. Reservas não são liberadas automaticamente; cancelamento/expiração requerem contrato próprio.

## Fluxo e garantias

`SolicitarAnalise` → preparação/snapshot e outbox `SolicitarBureau` → intenção pronta → job determinístico → outbox `ResultadoBureauRecebido` → avaliação e, se aprovada, `GerarContrato`.

Inbox por consumidor/mensagem evita efeitos duplicados. Lock da proposta serializa transições; análise/consulta vigentes e estado são verificados antes da avaliação. Mensagens antigas são auditadas na inbox e não mudam decisões. ACK somente após commit. Payload tem apenas IDs; dados financeiros ficam no Oracle.

Dispatcher reivindica outbox por atualização condicional com lease/token, sem manter transação durante publicação. Confirms e `mandatory` são obrigatórios. Sem rota, nack ou timeout deixam pendência; token vencido não marca publicação. Queda após confirm pode duplicar entrega e é absorvida pela inbox.

Topologia segue PA-1: exchange direct, quatro filas quorum e suas DLQs, DLX, `at-least-once`, `reject-publish` e limites explícitos. Um nó persistente de laboratório não oferece alta disponibilidade. M2 rejeita schema inválido para DLQ e reconecta com pausa após erro técnico sem ACK; retries duráveis por tentativa e replay administrativo entram em M3.

GET preserva autorização por empresa e passa a expor versão das regras, motivos, instante da avaliação e da decisão final. Aprovação informa contrato pendente; geração de contrato entra em M4. API aceita propostas persistidas com broker temporariamente indisponível; limites operacionais de backlog serão completados em M3.
