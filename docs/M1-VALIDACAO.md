# M1 — Evidências de implementação

Data: 30 de outubro de 2026. Escopo local: PC-07 a PC-12.

## Entregue

- Autenticação local restrita a Development e configuração de JWT externo; papéis de administrador, empresa, analista e operador.
- Cadastro/consulta de empresas, CNPJ numérico/alfanumérico e vínculo persistido entre identidade e empresa.
- Envio/consulta de propostas com validação, `202` após commit, histórico, idempotência e outbox na mesma transação Oracle.
- Repetição canônica devolve mesma resposta/Location; corpo divergente retorna `409`; escopo por empresa e consulta alheia `404`.
- Migration e grants explícitos, hooks de teste apenas por DI, erros HTTP sanitizados e fixture de demonstração repetível.
- `AGENTS.md`, contrato da tarefa, runner com limites/checkpoint, avaliações e piloto real. Ver [harness](../harness/README.md).

## Verificações realizadas

| Verificação | Evidência |
|---|---|
| Build Release e dependências fixadas | Build sem avisos/erros; restore por lockfile. |
| Reprodução limpa | Cópia Git descartável, cache novo, projeto Compose `proposta-credito-m1-clean`, Oracle vazio na porta 15212: bootstrap/migrations e verify completos aprovados, com as mesmas contagens de testes e sete avaliações. |
| Testes unitários | 23 aprovados, 0 ignorados; CNPJ, validação monetária, canonicalização, configuração Oracle e encerramento do Worker. |
| Integração Oracle real | 15 aprovados, 0 ignorados, incluindo os 6 de M0. |
| Corrida com mesma chave/corpo | 8 requisições sincronizadas antes da reserva; uma proposta, histórico, outbox e resposta idempotente. |
| Corrida com corpos divergentes | Uma aceitação, um conflito e uma proposta. |
| Falha após escritas, antes do commit | Proposta/histórico/outbox/chave ausentes após rollback; repetição com hook normal aceita. |
| Isolamento | Chave igual independente entre empresas; GET alheio bloqueado; header de empresa não altera vínculo. |
| Autenticação/autorização | Ausência/token inválido `401`; papéis indevidos e identidade sem vínculo `403`; modo local bloqueado em Staging/Production. |
| Entrada inválida | Valores/prazo/moeda/CNPJ inválidos, chave ausente e propriedades extras rejeitados sem proposta persistida. |
| Smoke | API e Worker com live/ready `200`. |
| Demonstração HTTP real | `scripts/demo-m1.ps1` cadastrou fixture, enviou/repetiu proposta com mesmo ID e consultou `RECEBIDA`/`AGUARDANDO_PROCESSAMENTO`, sem imprimir credenciais. |
| Avaliações do harness | Sete cenários aprovados: duplicidade incorreta, tentativas, timeout, orçamento total, retomada, sanitização e rejeição de contrato alterado. |

O teste de ciclo de vida herdado de M0 foi ajustado para aceitar cancelamento antes da primeira instrução de `ExecuteAsync`, estado legítimo do agendamento assíncrono em .NET 10; falhas continuam reprovadas.

## Piloto AI-SDLC

Contrato: `harness/tasks/PC-09-idempotencia.json`. RunId local: `m1-pilot-20261030`.

- Base Git: `b88a1e8162b9e970929df5e4bc1e28af7dfbbf5b`, com alterações M1 ainda não commitadas.
- Fingerprint do snapshot executado: `A32CFCA49E069491BCC3780BCDD9FEAE4C8168F8DB112C043486E5B503F9C34A`.
- Verificação completa aprovada na primeira tentativa; checkpoint interrompido intencionalmente após essa etapa.
- Retomada executou somente `diff-check`; ambas as etapas aprovadas, uma tentativa cada, status final `passed`.
- Tempo ativo acumulado observado: aproximadamente 43 segundos. Configuração detalhada/modelo e custo não expostos; custo registrado como desconhecido.
- Checkpoint e logs sanitizados em `artifacts/harness/runs/m1-pilot-20261030/`; resultados das avaliações da execução em `artifacts/harness/evals/eval-d1bd370c62bd49eb9b3a63f39336c959/`.

Este relatório e a atualização de status do backlog foram acrescentados após a execução; o fingerprint acima identifica o snapshot do piloto, não é um hash autorreferente deste relatório. Relatórios de execução permanecem ignorados pelo Git. O CI foi preparado para guardar TRXs, checkpoints e resultados de avaliações, sem publicar os logs brutos.

## Limites e pendências

O aceite remoto de PC-06 continua separado: nenhuma execução GitHub Actions desta alteração é declarada aprovada sem evidência. Não foi provisionado/conectado um IdP externo. O modo JWT está preparado para configuração; os cenários locais exercitam credenciais fictícias e bloqueio de ambiente.

M1 recebe propostas, mas ainda não as avalia nem publica no RabbitMQ. Estado pendente é explícito; M2 implementa dispatcher, consumidores e motor. O runner verifica código e retoma etapas; não orquestra modelos nem oferece isolamento de segurança de processos. As fixtures controladas não substituem o piloto HTTP/Oracle nem medem capacidade geral de IA.
