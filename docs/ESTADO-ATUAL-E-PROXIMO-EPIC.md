# Estado Atual e Proximo Epic

| Campo | Valor |
|---|---|
| Data de referencia | 2026-09-23 |
| Projeto | NEO-e |
| Estado | EPIC 5 em andamento; UI-001 a UI-003 implementados parcialmente |
| Proximo EPIC | EPIC 5 - continuar em UI-004 e UI-005 |
| Documento de referencia | [TASK-EPIC-05-WPF.md](../tasks/TASK-EPIC-05-WPF.md) |

## 1. Resumo

O projeto ja possui a solution .NET 8, as camadas Domain, Application, Contracts,
Infrastructure e App, alem dos projetos de testes.

O motor inicial de sincronizacao por CNPJ e NSU esta implementado. A janela WPF
ja possui shell MVVM, configuracao inicial e descoberta de certificados; o proximo
trabalho e conectar o lote completo a uma interface operacional.

## 2. Estado por Epic

### EPIC 1 - Solucao, configuracao e seguranca

Estado: base implementada.

- Solution e projetos .NET 8 criados.
- Configuracoes de armazenamento, ADN, ambiente, certificados e logging definidas.
- Ambiente Restrita e ambiente Producao possuem URLs separadas.
- Producao exige confirmacao explicita para troca de ambiente.
- Modelo de erros de dominio definido para certificado, rede, API, parsing e estado.

Pendencias antes de considerar o epic encerrado:

- Formalizar a politica de armazenamento de senhas com DPAPI ou Windows Credential Manager.
- Adicionar validacao automatizada de ausencia de segredos em logs e arquivos.

### EPIC 2 - Certificados A1 e mTLS

Estado: base implementada.

- Descoberta de arquivos `.pfx` e `.p12`.
- Leitura de subject, issuer, validade, thumbprint e chave privada.
- Carregamento com senha fornecida em memoria.
- Validacao de validade, chave privada e CNPJ do certificado.
- Cliente mTLS separado por certificado, com ciclo de vida reutilizavel.

Pendencias antes de considerar o epic encerrado:

- Confirmar a regra de extracao do CNPJ no contrato oficial do certificado.
- Criar testes com certificados sanitizados ou certificados de teste controlados.

### EPIC 3 - Cliente ADN e resiliencia HTTP

Estado: implementacao inicial concluida; homologacao ainda pendente.

- Cliente tipado `IAdnClient` criado.
- Consulta de DFe por NSU e consulta de eventos modeladas.
- Ambiente ativo usado para definir a URL.
- Retry limitado para erros transitorios e HTTP 429/5xx.
- Cancelamento propagado durante chamadas e backoff.
- `HttpClient` reutilizado por certificado, sem criar um cliente por request.
- Payload fiscal completo removido dos logs.

Pendencias antes de considerar o epic encerrado:

- Registrar contrato real da API em `docs/` com fixture sanitizada.
- Adicionar rate limiting local por empresa e global.
- Executar smoke test autorizado em producao restrita.
- Cobrir 400, 401, 403, 429, 5xx, timeout e resposta invalida com handler falso.

### EPIC 4 - NSU, lote e idempotencia

Estado: fluxo principal implementado e testado.

- Estado de sincronizacao independente por CNPJ.
- NSU negativo rejeitado.
- Persistencia SQLite do NSU e dos documentos.
- Parser de XML com decodificacao Base64 e GZip.
- Extracao de chave, CNPJ, datas, numero, serie e valor monetario.
- Escrita atomica de XML em pasta por CNPJ, tipo, ano e mes.
- Conteudo identico e ignorado; conteudo diferente nao sobrescreve arquivo silenciosamente.
- NSU so e confirmado depois do processamento dos documentos.
- Falha de documento impede a confirmacao do lote.
- Cancelamento e progresso por empresa foram incluidos no caso de uso.
- Carteira processada serialmente no MVP.

Pendencias antes de considerar o epic encerrado:

- Implementar deteccao e backup de banco SQLite corrompido.
- Confirmar a semantica final de `ultimoNsu`, `ultNSU` e `maxNSU` com o contrato oficial.
- Criar testes de interrupcao em cada etapa do lote.
- Separar explicitamente resultados de sucesso, erro, ignorado e cancelado em evidencia de execucao.

## 3. Evidencia de validacao

Em 2026-09-23 foram executados:

- `dotnet build NEO-e.slnx --no-restore`: sucesso.
- `dotnet test NEO-e.slnx --no-restore`: 8 testes aprovados.
- Diagnostico dos arquivos alterados: nenhum erro encontrado.

Os testes unitarios adicionados cobrem:

- Rejeicao de NSU negativo.
- Monotonicidade do estado de sincronizacao.
- Decodificacao GZip e parsing de XML com namespace.
- Escrita idempotente e limpeza do arquivo temporario.
- Persistencia e leitura de documento no SQLite.

## 4. EPIC 5 - WPF moderno em andamento

O objetivo do EPIC 5 e disponibilizar o fluxo principal para o usuario sem
acessar controles visuais diretamente a partir do ViewModel.

Estado atual do incremento:

- `UI-001` iniciado com shell WPF, tema, navegacao basica e estado visual.
- `UI-002` implementado com selecao de pasta de certificados, destino, ambiente, estrutura e validacao.
- Composicao da aplicacao feita por `Host` e injecao de dependencias.
- Descoberta de certificados executada de forma assincrona pelo ViewModel.
- Tabela de certificados com selecao, validade, thumbprint, senha mascarada e situacao.
- Senha encaminhada ao ViewModel somente em memoria durante a sessao.
- `UI-003` implementado com tabela, busca e selecao em massa.

### Ordem de implementacao restante

1. `UI-004` - Entrada de senha mascarada, mantida somente durante o lote.
2. `UI-005` - Comandos de baixar, resetar NSU e parar com cancelamento cooperativo.
3. `UI-006` - Progresso, contadores e mensagens acionaveis sem dados sensiveis.

### Criterios de entrada

- O EPIC 4 deve continuar compilando e passando os testes existentes.
- O ViewModel deve depender de interfaces da Application, nunca de controles WPF.
- O ambiente ativo deve aparecer claramente na tela.
- Producao deve iniciar bloqueada ate confirmacao explicita.
- Senhas nao podem ser persistidas em `appsettings.json`, ViewModel ou log.

### Primeiro incremento recomendado

Implementar `UI-001` e `UI-002` em conjunto:

- criar o ViewModel principal;
- expor o ambiente ativo e o estado da configuracao;
- carregar e listar certificados;
- configurar pasta de destino;
- representar estados de pronto, carregando, vazio e erro;
- manter a janela responsiva usando operacoes assincronas.

Depois disso, `UI-003` pode consumir a descoberta de certificados ja existente
e preparar a carteira para os comandos de sincronizacao.

## 5. Validacao do EPIC 5 ate o momento

- `dotnet build src/NEO-e.App/NEO-e.App.csproj --no-restore`: sucesso.
- `dotnet test NEO-e.slnx --no-restore`: 8 testes aprovados.
- Diagnostico dos arquivos WPF alterados: nenhum erro encontrado.

O proximo incremento deve conectar a senha carregada e a selecao da carteira ao
caso de uso de sincronizacao, mantendo confirmacao explicita para reset de NSU
e cancelamento cooperativo.

## 6. Riscos para a transicao

- A API ADN ainda precisa de contrato oficial e fixture sanitizada antes de homologacao.
- A UI nao deve habilitar operacoes conclusivas ou producao por padrao.
- A senha do certificado deve permanecer somente em memoria durante a operacao.
- O parser deve ser ajustado se o schema oficial divergir dos nomes usados nas fixtures atuais.

## 7. Referencias

- [EPIC 4 - NSU, lote e idempotencia](../tasks/TASK-EPIC-04-SINCRONIZACAO.md)
- [EPIC 5 - WPF moderno](../tasks/TASK-EPIC-05-WPF.md)
- [Contrato da API ADN](../tasks/TASK-EPIC-00-API-ADN.md)
- [README do projeto](../README.md)