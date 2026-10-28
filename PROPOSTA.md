# Plataforma de concessão e acompanhamento de crédito

## Problema

Uma financeira recebe solicitações de empréstimo de pequenas empresas, avalia o risco do cliente, aprova ou rejeita a operação e acompanha os pagamentos. Hoje esse processo depende de planilhas, verificações manuais e sistemas desconectados, causando propostas duplicadas, demora nas decisões e divergências contábeis.

O desafio é construir o backend que automatiza esse processo e permite investigar falhas, praticando os requisitos da vaga de Desenvolvedor .NET Core Sênior para a Proposta Crédito.

## Fluxo principal

1. Uma empresa envia uma proposta com CNPJ, faturamento e valor solicitado.
2. O sistema valida os dados e registra a proposta.
3. Um motor de crédito avalia regras como comprometimento de receita, dívidas existentes e limite disponível.
4. A proposta é aprovada, rejeitada ou encaminhada para análise manual.
5. Uma aprovação gera um contrato e um cronograma de parcelas.
6. O sistema recebe pagamentos e atualiza o saldo devedor.
7. Um processamento diário identifica atrasos, concilia os valores e gera um arquivo de fechamento.

A consulta de risco pode começar com um serviço simulado que retorna dados fictícios e, ocasionalmente, apresenta lentidão ou indisponibilidade.

## Habilidades exercitadas

| Habilidade | Aplicação no projeto |
|---|---|
| C# e .NET | API REST, regras de negócio e processamento em segundo plano |
| Oracle e PL/SQL | Persistência de propostas, contratos, parcelas e pagamentos |
| Packages | Agrupar operações de crédito e fechamento contábil |
| Functions | Calcular saldo devedor ou valores em atraso |
| Procedures | Registrar pagamentos e executar a conciliação |
| Triggers | Auditar alterações em dados financeiros |
| Jobs | Executar uma rotina diária de identificação de atrasos |
| Redis | Cache de consultas de risco com prazo de validade |
| RabbitMQ | Processar análises de crédito e pagamentos de forma assíncrona |
| Git e CI/CD | Trabalhar com branches, revisão e pipeline de build e testes |
| Sonar | Acompanhar problemas de qualidade e cobertura das regras críticas |
| Datadog | Investigar erros, latência e processamento de mensagens |
| Jira e Scrum | Organizar backlog, critérios de aceite e pequenas entregas |
| Zendesk | Simular chamados e documentar diagnóstico e resolução |

O arquivo de fechamento pode ter um layout fictício documentado, com cabeçalho, detalhes e totais de controle. Isso permite praticar processamento de arquivos e validações semelhantes às de integrações regulatórias.

## Evolução em entregas

### 1. Cadastro e proposta de crédito

Criar o cadastro de empresas e os endpoints para enviar e consultar propostas, com persistência no Oracle.

**Critérios de aceite:** impedir valores inválidos, evitar duplicidade quando o cliente repetir uma requisição e manter o histórico de estados da proposta.

### 2. Motor de decisão

Implementar regras determinísticas e versionadas. Por exemplo, a parcela estimada não pode ultrapassar um percentual configurado do faturamento mensal.

**Critérios de aceite:** cada decisão deve registrar os dados considerados, a versão das regras e os motivos da aprovação ou rejeição.

### 3. Análise assíncrona

Usar RabbitMQ para processar propostas e Redis para armazenar temporariamente os resultados das consultas de risco.

**Critérios de aceite:** uma mensagem duplicada não pode gerar duas decisões; falhas temporárias devem permitir novas tentativas; falhas persistentes devem ficar disponíveis para investigação e reprocessamento.

### 4. Contratos e pagamentos

Gerar contratos e parcelas após a aprovação. Receber notificações de pagamento com um identificador externo único.

**Critérios de aceite:** um pagamento repetido não pode reduzir o saldo duas vezes; pagamentos parciais precisam ser tratados; uma falha durante a operação não pode deixar dados financeiros inconsistentes.

### 5. Fechamento e conciliação

Implementar as rotinas PL/SQL, o processamento diário e a exportação do arquivo.

**Critérios de aceite:** os totais devem corresponder aos lançamentos, divergências precisam ser identificadas e a rotina deve poder ser executada novamente sem duplicar resultados.

### 6. Operação e qualidade

Configurar pipeline, Sonar e Datadog. Preparar chamados simulados, como “pagamento recebido, mas parcela continua aberta”.

**Critérios de aceite:** conseguir seguir uma operação da API até o processamento em segundo plano, identificar a causa da falha e documentar uma recuperação segura.

## Escopo inicial recomendado

Começar com um monólito modular, uma API e um worker, no mesmo repositório. Separar os módulos de Propostas, Crédito, Contratos e Pagamentos. Essa estrutura oferece espaço para praticar limites de domínio, transações e mensageria sem exigir vários serviços desde o início.

A primeira meta concreta é **enviar uma proposta, avaliá-la com três regras e consultar a decisão com seus motivos**, usando .NET e Oracle, com testes das regras de negócio. Depois, acrescentar as demais tecnologias conforme as entregas exigirem.
