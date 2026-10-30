# M1 — Cadastro, identidade e propostas

## Identidade e escopo

Autenticação usa `Authorization: Bearer <credencial>`. No modo `Development`, credenciais aleatórias mapeiam sujeitos e papéis fictícios definidos no ambiente, sem endpoint público de emissão. Esse modo é recusado fora de Development. O bootstrap acrescenta tokens ao `.env` existente, preservando senhas e dados de M0; os scripts não os imprimem.

Fora do laboratório, configurar `Authentication__Mode=External`, `Authentication__Authority=https://...` e `Authentication__Audience`. O handler JWT valida assinatura, emissor, audiência e expiração; usa `sub` como identidade e `role` como papel. A conexão real a um emissor externo depende do ambiente escolhido; não foi provisionado um provedor de identidade nesta entrega.

Papéis: `Administrador` cadastra/consulta empresas; `Empresa` consulta seu cadastro e envia/consulta suas propostas. `Analista` e `Operador` já são reconhecidos, mas não recebem endpoints de concessão neste marco. Health checks são públicos e não expõem dados de negócio.

`IDENTIDADE_EMPRESA` associa um `sub` a uma empresa. O administrador estabelece o vínculo no cadastro; o sujeito deve corresponder a uma identidade administrada pelo emissor. M1 suporta uma empresa por sujeito. Propostas não aceitam `empresaId`; headers de empresa não mudam o escopo. Consulta alheia recebe `404`, sem confirmar a existência do recurso. Troca de vínculo, carteira do analista e revogação são evoluções com contrato próprio.

## HTTP

Todas as escritas exigem uma única `Idempotency-Key`: 1 a 128 caracteres ASCII `[A-Za-z0-9._:-]`. Erros de negócio/entrada usam Problem Details sanitizado; falhas técnicas retornam `503`, permitindo repetição com a mesma chave. JSON com propriedades desconhecidas é rejeitado.

| Operação | Papel | Corpo / resposta |
|---|---|---|
| `POST /empresas` | Administrador | `cnpj`, `razaoSocial` (1–200), `responsavelId` (sub de 1–128); retorna `201`, `Location`, `empresaId`, CNPJ normalizado, nome e instante UTC. |
| `GET /empresas/{id}` | Administrador ou Empresa vinculada | Retorna o cadastro autorizado; inexistente/alheio retorna `404`. |
| `POST /propostas` | Empresa vinculada | `cnpj`, `faturamentoMensal`, `valorSolicitado`, `prazoMeses`, `moeda`; retorna `202`, `Location`, `propostaId`, `estado=RECEBIDA` após commit. |
| `GET /propostas/{id}` | Empresa vinculada | ID, empresa, estado/versão, instante UTC, situação operacional, motivos, data de decisão e histórico. Em M1, motivos vazios e decisão nula. |

Valores monetários: positivos, até `999999999999.99`, com no máximo duas casas; não arredondar silenciosamente entrada. Prazo: 1–60 meses. Moeda: BRL, normalizada para maiúsculas. CNPJ deve corresponder ao cadastro da identidade.

CNPJ aceita formato compacto ou máscara completa, normaliza caixa/pontuação e verifica os dois DVs. Suporta formato numérico e as 12 posições alfanuméricas do novo formato, com dois DVs numéricos. Algoritmo conforme [manual técnico da Receita](https://www.gov.br/receitafederal/pt-br/centrais-de-conteudo/publicacoes/documentos-tecnicos/cnpj/manual-dv-cnpj.pdf): valores ASCII menos 48, pesos e módulo 11. Validar DV não comprova registro real; todas as fixtures são fictícias.

## Idempotência e transação

Empresas usam escopo `admin:<sub>` + operação + chave. O CNPJ e o responsável têm unicidade própria: nova chave para cadastro já existente recebe `409`. Propostas usam empresa derivada da identidade + operação + chave; a mesma chave em empresas distintas é independente.

O hash SHA-256 inclui versão do contrato e campos normalizados, prefixados por tamanho UTF-8. Ordem/indentação do JSON, pontuação do CNPJ, escala decimal e caixa da moeda não mudam o resultado. Alterações de nome/valores significativas mudam o hash e causam `409` com chave reutilizada.

Uma transação Oracle reserva a chave única antes dos efeitos, grava proposta, histórico inicial, outbox e resposta idempotente, e confirma tudo junto. Concorrentes disputam a PK no banco: o perdedor lê a resposta confirmada e verifica o hash. Falha desfaz também a reserva; nenhuma aceitação é respondida antes do commit. Não há cache/lock em memória sustentando unicidade.

O evento `SolicitarAnalise` mantém IDs lógicos, análise, correlação/causação, versão, tentativa/replay e payload mínimo de proposta. Não carrega CNPJ ou valores. Em M1, a publicação permanece pendente e o GET informa `AGUARDANDO_PROCESSAMENTO`; dispatcher, fila e decisões entram em M2. As colunas preparadas não comprovam processamento de mensagens.

Runtime recebe SELECT/INSERT nas tabelas de entrada e UPDATE somente da resposta idempotente. Não recebe DDL, DELETE ou atualização de metadados. Leitura de outros tenants é controlada nos serviços; Oracle VPD/RLS não está implementado. As instâncias da API são confiáveis e compartilham um usuário runtime.

## Verificação

`scripts/verify.ps1` executa testes unitários, integração Oracle, smoke e avaliações do harness. Os testes sincronizam concorrentes antes da reserva, sem usar sleeps como garantia de corrida. Um hook substituído apenas via DI nos testes injeta falha após as escritas e antes do commit; nenhum endpoint/header habilita essa falha.

`scripts/demo-m1.ps1` inicia uma API temporária e demonstra cadastro, envio, repetição e consulta. Usa sujeitos locais `admin-local` e `empresa-a`; mantém a fixture no Oracle e pode ser repetido com as mesmas chaves. Não imprime tokens. Execute após bootstrap e verify. Se já houver outro cadastro para `empresa-a`, use identidades/fixture próprias em vez de apagar seus dados.
