# M0 — Evidências de implementação e validação

Data: 30 de outubro de 2026.
Escopo: PC-01 a PC-06, com aceite remoto de PC-06 pendente.

## Entregue

- Convenções de contribuição, templates de Issue/PR, ignore de artefatos/segredos e escolha do mantenedor de permanecer sem licença.
- SDK .NET e dependências fixados, lockfiles e [ADR da stack](adr/0001-fundacao-dotnet-oracle.md).
- API, Worker, infraestrutura Oracle e executável de migrations separados.
- Compose Oracle por versão/digest, volume persistente e inicialização de usuários owner/runtime com permissões separadas.
- Migrations de metadados, readiness de banco/schema e liveness independente.
- Scripts reproduzíveis de bootstrap, verificação, execução e smoke; workflow GitHub Actions reutilizando esses scripts.

## Verificações locais

Ambiente observado: Windows x64, SDK 10.0.403, runtime 10.0.14, Docker Desktop com engine Linux e Oracle Free da imagem fixada.

| Verificação | Resultado |
|---|---|
| Restore por lockfile e build Release | Aprovados, sem avisos/erros no build final. |
| Testes unitários | 8 aprovados, 0 ignorados: configuração incompleta/malformada sem vazamento de segredo e cancelamento cooperativo do Worker. |
| Integração Oracle real | 6 aprovados, 0 ignorados: reexecução de migrations, commit/rollback em conexões distintas, restrições do runtime e saúde da API. |
| Readiness negativo | `503` com Oracle inacessível ou schema incompatível; liveness permanece `200`. |
| Smoke de processos reais | API e Worker iniciam e respondem `200` em live/ready; processos do smoke são limpos ao finalizar. |
| Bootstrap de banco vazio | Container/volume descartáveis criados nesta entrega; migrations e grants aplicados com sucesso. |
| Reprodução a partir dos arquivos do commit | Snapshot do índice exportado sem `.env`, `bin`, `obj` ou caches; bootstrap e verify completos aprovados com porta, projeto Compose e volume independentes. Novamente 8 testes unitários, 6 de integração e os dois hosts aprovados. |
| Workflow | `actionlint` 1.7.12 aprovado; binário obtido do release oficial com checksum SHA-256 conferido. Execução no Actions ainda pendente. |
| Propagação de falhas | Durante validação, falhas de teste encerraram verify com código 1; corrigidas antes da execução aprovada. Isso não equivale à falha controlada remota exigida por PC-06. |
| Segredos/artefatos | `.env`, `.local`, `artifacts`, `bin` e `obj` ignorados pelo Git; senhas locais não são versionadas. |

Comandos principais: `pwsh -NoProfile -File scripts/bootstrap.ps1` e `pwsh -NoProfile -File scripts/verify.ps1`. TRXs ficam em `artifacts/test-results/`; instruções de reprodução em [DEVELOPMENT.md](DEVELOPMENT.md). Os artefatos gerados permanecem locais; este documento registra o resultado, não substitui os relatórios de uma execução futura.

## Pendências remotas de PC-06

A consulta somente de leitura a `repos/jairoconceicao/proposta-credito/branches/main/protection` retornou HTTP 404 com `Branch not protected`. Não houve alteração das configurações remotas nem push nesta entrega.

Para concluir PC-06 e o aceite integral de M0:

1. Publicar a branch e executar o workflow em PR ou por acionamento manual nessa branch.
2. Guardar o link da execução aprovada, incluindo testes Oracle e smoke no runner Linux.
3. Comprovar no Actions que teste quebrado deixa o check vermelho, sem integrar a falha em `main`.
4. Configurar revisão de PR e o check `Build, unit and Oracle integration` como obrigatórios; registrar eventual limitação do plano/permissões GitHub.

## Limites

Este marco não implementa cadastro, autorização de negócio, concessão, pagamentos ou mensageria. O Worker tem somente ciclo de vida e saúde. Não há CD nem garantia de produção. DDL Oracle pode confirmar parcialmente uma migration; o roteiro exige revisão e recuperação explícitas. API/Worker não recebem credenciais administrativas nem executam migrations no startup.
