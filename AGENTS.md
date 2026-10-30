# Proposta Crédito — contexto de execução

- Leia a tarefa em `harness/tasks/` e os contratos em `docs/specs/m1-cadastro-propostas.md` antes de alterar M1.
- Preparar: `pwsh -NoProfile -File scripts/bootstrap.ps1`.
- Verificar tudo: `pwsh -NoProfile -File scripts/verify.ps1`. Oracle real é obrigatório; não ignorar integração.
- Verificar somente unidades: `pwsh -NoProfile -File scripts/verify.ps1 -Scope Unit` (resultado parcial).
- API/Worker não aplicam migrations no startup. Migrations usam PC_OWNER; runtime usa PC_APP e grants explícitos.
- Módulos em `src/PropostaCredito.Infrastructure/Modules/`; cada serviço controla suas escritas. Não colocar regras de negócio nos endpoints.
- Empresa da proposta vem do vínculo persistido da identidade; clientes não escolhem empresa por header/body.
- Proposta, histórico, idempotência e outbox são uma transação Oracle. Responder aceitação somente após commit.
- Repetição idêntica devolve mesma resposta; corpo divergente com a mesma chave dá 409. Unicidade no banco sustenta concorrência.
- Não versionar `.env`, tokens, conexões, logs ou artefatos. Não expor dados de negócio nos logs.
- Consultar `docs/architecture/pa-1.md`, `docs/PLANO-IMPLEMENTACAO.md` e `docs/BACKLOG.md`. Registrar mudanças de contrato em ADR.
- O harness executa verificações independentes e registra evidências; declaração do agente não substitui testes. Não marcar aceites remotos sem execução real.
