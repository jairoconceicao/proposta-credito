# ADR 0002 — Identidade, idempotência e harness mínimo de M1

Data: 30 de outubro de 2026. Status: adotado.

## Decisões

1. Identidade usa Bearer: tokens opacos aleatórios somente em Development; JWT externo validado em homologação/produção. O modo de laboratório não é fallback de produção. Chaves locais ficam no `.env`, fora do Git. Papéis e vínculos são independentes: possuir papel Empresa não dispensa vínculo no Oracle.
2. Um sujeito pertence a uma empresa neste incremento, com vínculo criado por administrador. O cadastro do emissor externo e a gestão posterior de vínculos têm evolução própria. Consulta alheia retorna 404. Não criar bypass de autorização para administradores consultarem propostas financeiras.
3. Reserva idempotente por PK é gravada na mesma transação antes do domínio. Corpo canônico divergente dá 409; perdedores da corrida leem a resposta do vencedor após rollback de sua transação. Restrições Oracle garantem unicidade entre processos.
4. Oracle runtime recebe somente grants necessários às novas tabelas. EF mantém um contexto transacional compartilhado por enquanto; serviços dos módulos controlam suas escritas. Essa composição não muda o monólito modular do PA-1.
5. CNPJ numérico e alfanumérico seguem o contrato atual da Receita. Campos extras e valores monetários que exigiriam arredondamento são recusados.
6. Harness inicial é um runner de verificações PowerShell usando os scripts já usados pelo CI. O agente desta sessão implementa; o runner não chama modelo nem usa API key. Não construir orquestrador de agentes neste marco.

## Harness e limites

O contrato versionado contém objetivo, escopo orientativo, aceite, comandos e limites. Execução guarda commit base e fingerprint do conteúdo (incluindo alterações não commitadas), configuração disponível, resultados, duração ativa, tentativas e logs sanitizados. Modelo/configuração detalhada e custo não disponíveis são explicitamente desconhecidos.

Checkpoint é atualizado por troca de arquivo; lock de arquivo impede dois runners no mesmo RunId. Retomada valida código e tarefa, pula etapas aprovadas e respeita tentativas já consumidas. Timeout mata a árvore de processos da verificação. Tempo acumulado limita execução ativa; tempo offline não é contado. Mudança de código/contrato exige novo RunId.

Escopo no JSON orienta o agente; o runner não é uma sandbox de segurança. Comandos de um contrato devem ser revisados como código. As verificações são confiáveis no limite das permissões do processo. Sanitização reduz exposição dos segredos conhecidos do ambiente/`.env`, sem prometer detectar todo dado sensível arbitrário.

Avaliações controladas comprovam rejeição de fixture incorreta, limite de tentativas, timeout, orçamento, retomada, redação de segredo e rejeição de contrato alterado. O piloto usa os testes reais de HTTP/Oracle; avaliações de fixture não equivalem a medir capacidade geral de um modelo nem a uma mutação da aplicação de produção.

Referências: [contrato M1](../specs/m1-cadastro-propostas.md), [ASP.NET Core AuthenticationHandler](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/?view=aspnetcore-10.0), [JWT Bearer](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0), [harness](../../harness/README.md).
