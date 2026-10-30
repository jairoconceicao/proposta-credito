# Proposta Crédito — contexto de execução

- Leia a tarefa em `harness/tasks/` e os contratos em `docs/specs/m1-cadastro-propostas.md` antes de alterar M1.
- M2: leia `docs/specs/m2-decisao-credito.md` e `harness/tasks/PC-13-primeira-decisao.json`. Regras lab-v1 sao imutaveis; testes de fronteira fazem parte do contrato.
- Preparar: `pwsh -NoProfile -File scripts/bootstrap.ps1`.
- Verificar tudo: `pwsh -NoProfile -File scripts/verify.ps1`. Oracle real é obrigatório; não ignorar integração.
- Verificar somente unidades: `pwsh -NoProfile -File scripts/verify.ps1 -Scope Unit` (resultado parcial).
- API/Worker não aplicam migrations no startup. Migrations usam PC_OWNER; runtime usa PC_APP e grants explícitos.
- Módulos em `src/PropostaCredito.Infrastructure/Modules/`; cada serviço controla suas escritas. Não colocar regras de negócio nos endpoints.
- Empresa da proposta vem do vínculo persistido da identidade; clientes não escolhem empresa por header/body.
- Proposta, histórico, idempotência e outbox são uma transação Oracle. Responder aceitação somente após commit.
- Repetição idêntica devolve mesma resposta; corpo divergente com a mesma chave dá 409. Unicidade no banco sustenta concorrência.
- Mensageria: confirms + mandatory antes de marcar outbox; lease/token vigente; ACK somente apos commit de inbox/dominio. Reserva de limite e decisao pertencem a mesma transacao.
- `scripts/verify.ps1` inclui demonstracao de recuperacao que para/reinicia somente o RabbitMQ deste Compose; use ambiente de laboratorio e preserve os volumes.
- Não versionar `.env`, tokens, conexões, logs ou artefatos. Não expor dados de negócio nos logs.
- Consultar `docs/architecture/pa-1.md`, `docs/PLANO-IMPLEMENTACAO.md` e `docs/BACKLOG.md`. Registrar mudanças de contrato em ADR.
- O harness executa verificações independentes e registra evidências; declaração do agente não substitui testes. Não marcar aceites remotos sem execução real.
