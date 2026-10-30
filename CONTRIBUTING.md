# Contribuição

Comece por [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) e pela tarefa no [backlog](docs/BACKLOG.md). Use branches curtas; alterações assistidas usam o prefixo `codex/`. Cada PR deve identificar PC-xx, comportamento resultante e evidências.

Execute `pwsh -File scripts/verify.ps1` antes do PR. O escopo `-Scope Unit` serve ao desenvolvimento sem banco, mas não substitui a verificação completa. Não ignore integração para aprovar CI. Atualize ADRs quando mudar contratos do PA-1. Segredos, dados reais e artefatos de execução não entram no Git.

Migrations são aplicadas por um único processo, usando o usuário proprietário do schema. API e Worker não executam migrations no startup. Acrescente privilégios explícitos do usuário runtime em cada migration que criar objetos acessados pela aplicação. Oracle faz commits implícitos em DDL: revise scripts e recuperação antes de executá-los em ambiente persistente.

Por decisão do mantenedor em 30 de outubro de 2026, o repositório permanece sem licença por enquanto. Ainda não foi concedida uma licença de distribuição; qualquer inclusão futura exige nova escolha explícita.
