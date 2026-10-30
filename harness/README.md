# Harness mínimo de AI-SDLC

O agente desenvolve a tarefa; este runner executa verificações independentes e guarda checkpoints/evidências. Não invoca API/modelo e não exige chave OpenAI. Contratos em `tasks/` são código revisável; `AGENTS.md` fornece contexto, não comprova sucesso.

Após `scripts/bootstrap.ps1`, execute o piloto:

```powershell
pwsh -NoProfile -File harness/run.ps1 -TaskPath harness/tasks/PC-09-idempotencia.json -RunId meu-piloto
```

Para ensaiar interrupção após a primeira etapa e retomar:

```powershell
pwsh -NoProfile -File harness/run.ps1 -TaskPath harness/tasks/PC-09-idempotencia.json -RunId meu-piloto-retomada -StopAfterStep 1
# Saída 2 significa checkpoint interrompido intencionalmente.
pwsh -NoProfile -File harness/run.ps1 -TaskPath harness/tasks/PC-09-idempotencia.json -RunId meu-piloto-retomada -Resume
```

RunIds são únicos; não reutilizar uma execução para outro snapshot. Código/tarefa alterados recusam retomada. Tentativas de etapas interrompidas são consumidas; orçamento esgotado requer revisão e novo contrato/RunId. Etapas já aprovadas não repetem na retomada. O runner bloqueia uso simultâneo do mesmo RunId e mata processos que excedem o timeout.

`artifacts/harness/runs/<RunId>/state.json` registra commit base, fingerprint, configuração informada, comandos, tentativas, logs, tempo ativo e pendências. Todos esses artefatos são ignorados pelo Git; relatórios resumidos sem segredos podem ser incorporados à documentação. O commit base sozinho não representa alterações locais: confira o fingerprint. `cost=null` significa custo não observado, não custo zero.

Avaliações do runner:

```powershell
pwsh -NoProfile -File harness/evals/run.ps1
```

Sete cenários: fixture que duplica propostas; falha repetida; timeout; orçamento acumulado; interrupção/retomada; segredo conhecido redigido; contrato alterado recusado. As saídas `failed` dos casos negativos são esperadas; o script de avaliação precisa encerrar com 0. O verify completo e o CI já executam essas avaliações.

O piloto real executa `scripts/verify.ps1`, incluindo os testes HTTP/Oracle de propostas, e `git diff --check`. Para uma tarefa nova, copie `tasks/template.json` e preencha objetivo, escopo, critérios e verificadores. O escopo é orientação, não restrição de filesystem. O runner não substitui isolamento do ambiente, revisão humana, nem mede qualidade geral de um agente. Veja [ADR 0002](../docs/adr/0002-m1-identidade-idempotencia-harness.md).
