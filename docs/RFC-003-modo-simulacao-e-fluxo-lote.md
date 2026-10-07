# RFC-003: Modo de simulacao e fluxo de lote

- Status: Escopo da primeira fase aprovado; implementacao em andamento
- Data: 2026-10-07
- Responsavel: NEO-e
- Relacionados: RFC-001, RFC-002

## Resumo

Separar o ambiente de homologacao da simulacao local e melhorar a tela de sincronizacao em lote. A simulacao permite exercitar a selecao de empresas, o progresso, os resultados e as falhas sem certificado real, acesso de rede ou alteracao do estado fiscal persistido.

## Contexto e problema

A tela atual exige pelo menos um certificado selecionado, com senha, chave privada e validade vigente, para habilitar `Baixar (XML)`. Isso faz o botao parecer indisponivel quando ainda nao ha certificados validos carregados. A busca filtra nome do arquivo, subject e thumbprint, mas nao o CNPJ. O rotulo `Restrita` nao explica que esse ambiente chama a API de producao restrita/homologacao; nao existe hoje um modo local simulado.

A tela ja tem comandos de analise e recuperacao de lacunas e exportacao Excel, mas a recuperacao nao separa os NSUs por empresa. A tela nao apresenta claramente a versao do aplicativo nem um estado de progresso persistente apos a execucao.

## Objetivos

- Distinguir visual e funcionalmente simulacao local, homologacao e producao.
- Permitir exercitar o fluxo de lote offline com dados fiscais sinteticos.
- Tornar empresas localizaveis por nome, CNPJ, arquivo e identificador do certificado.
- Dar feedback de progresso, conclusao, cancelamento e erros por operacao.
- Facilitar a selecao de varias empresas com certificados distintos.
- Corrigir e expor as operacoes existentes de analise/recuperacao de lacunas e exportacao Excel.
- Exibir a versao real do assembly, sem inventar ou fixar um numero de versao na interface.

## Fora do escopo desta fase

- Webhooks de emissao, recebimento, cancelamento e inconsistencias tributarias.
- Importacao OFX, matching financeiro e conciliacao de retencoes.
- Manifestacao automatica em lote, recálculo tributario e conectores para ERPs.
- Geracao fiscal oficial de DANFSe ou de PDF de documentos fiscais. Esses artefatos dependem da definicao dos formatos suportados, da origem dos documentos e dos requisitos de apresentacao/autenticidade.
- Uso das amostras em `docs/fixtures/xmls` pelo simulador ate que sua origem e sanitizacao sejam verificadas. A presenca de valores fiscais e nomes sem marcador explicito de teste nao comprova que sejam dados ficticios.

Essas evolucoes ficam como itens de roadmap; nao devem ser simuladas como integracoes concluidas.

## Conceitos e seguranca

### Ambientes

- `Restrita`: API da NFS-e Nacional em producao restrita/homologacao. Pode realizar chamadas de rede e depende de certificado.
- `Producao`: API real. Continua sujeita a confirmacao explicita e nao e habilitada pela simulacao.
- `Simulacao local (TESTE)`: fluxo offline, baseado em dados sinteticos; nao e um ambiente da API nem altera o ambiente selecionado.

O modo de simulacao deve ser identificado com `TESTE` durante toda a execucao. Ativa-lo nao pode selecionar producao nem permitir fallback para a API. A simulacao nao deve carregar certificados, salvar senha, chamar clientes HTTP, atualizar NSU persistido ou sobrescrever XML de uma sincronizacao real. Saidas simuladas ficam em `%LOCALAPPDATA%\\NEO-e\\Simulacao`, organizadas por identificador de execucao.

Os erros mostrados ao usuario devem identificar a operacao e a empresa, sem registrar senha, chave privada, XML completo ou outro segredo.

### Dados sinteticos

O modo local usa dados de teste criados ou verificados como ficticios, sem CNPJ, nome, endereco ou documento fiscal de pessoa real. Fixtures sem procedencia comprovada nao podem ser promovidas a fixtures de simulacao/teste. Falhas previsiveis podem ser injetadas no cenario local para validar o estado de erro sem chamar a API.

## Desenho funcional

### Tela principal

- Reorganizar configuracao, carteira de empresas, acoes e resumo seguindo a referencia visual fornecida.
- Mostrar a versao do app e a descricao do ambiente ativo.
- Informar de forma persistente o estado da operacao; exibir barra de progresso, empresa atual, contagens e erros durante o lote, alem de cancelamento quando aplicavel.
- Manter a selecao multipla por certificado; filtrar por nome da empresa, CNPJ/CPF, nome/caminho do arquivo e thumbprint.
- Disponibilizar selecao visivel, limpeza da selecao e indicacao de empresas validas, invalidas e simuladas.
- Apresentar as acoes de lacunas, recuperacao e exportacao Excel com habilitacao coerente com selecao e modo de operacao.

### Fluxo de simulacao

1. O usuario ativa explicitamente `Simulacao (TESTE)`.
2. A interface apresenta empresas e documentos sinteticos sem requerer pastas ou certificados reais.
3. O usuario pode pesquisar, selecionar mais de uma empresa e iniciar o lote.
4. O cenario processa localmente, reporta progresso e resultados deterministas e permite cancelar.
5. Erros simulados sao visiveis e contabilizados; a execucao nunca se apresenta como uma chamada bem-sucedida a SEFIN/SEFAZ.
6. Ao desativar simulacao, os dados sinteticos sao removidos da carteira visivel e o fluxo normal volta a exigir configuracao e certificados validos.

### Fluxo real de lote

Continuar usando os casos de uso existentes e preservar as regras de certificado por CNPJ, confirmacao de NSU somente apos gravacao dos documentos, confirmacao explicita de producao e cancelamento cooperativo. O botao deve explicar os pre-requisitos quando desabilitado, em vez de parecer travado.

### Lacunas e exportacao

Analise e recuperacao devem operar apenas sobre as empresas selecionadas, usar cancelamento explicito e separar os NSUs de cada CNPJ. A recuperacao nao pode aplicar lacunas de uma empresa a outra. A exportacao de Excel deve produzir dados da execucao real ou sintetica correspondente, sem registros placeholder invalidos.

Geracao de DANFSe e visualizacao de relatorio PDF ficam como fase posterior, depois da definicao de formatos e da aprovacao de uma representacao que nao possa ser confundida com documento fiscal oficial.

## Arquitetura

- Manter WPF/MVVM e preservar a separacao Domain/Application/Infrastructure.
- Implementar a simulacao como fluxo local explicitamente separado da sincronizacao real. O caminho de simulacao nao recebe dependencia do cliente ADN nem do carregador de certificados.
- Manter o contexto de ambiente da API (`Restrita`/`Producao`) independente do estado local de simulacao.
- Isolar saidas simuladas em caminho dedicado e nao usar o repositorio de NSU de producao.
- Cobrir a simulacao e as regras de selecao/progresso com testes que nao dependam de certificado real, API externa ou dados fiscais reais.

## Criterios de aceite

1. A tela distingue `Restrita (homologacao)` de `Simulacao (TESTE)` e de `Producao`.
2. A simulacao permite executar e cancelar um lote offline sem certificado e sem chamada de rede.
3. O modo simulado mostra progresso, estado final e erros do cenario; nenhuma falha e convertida em sucesso.
4. A busca encontra registros por empresa, CNPJ/CPF e arquivo, e permite operar com varias empresas selecionadas.
5. `Baixar (XML)` explica os pre-requisitos ausentes no fluxo real e e habilitado para um cenario simulado selecionado.
6. A analise e recuperacao de lacunas tratam os NSUs por empresa e respeitam cancelamento.
7. A exportacao Excel do resumo nao usa CNPJ ou totais placeholder.
8. Versao do aplicativo exibida vem dos metadados reais do assembly.
9. Os testes nao usam dados fiscais reais e verificam que simulacao nao chama rede nem persiste NSU real.

## Validacao

- `dotnet build NEO-e.slnx`
- `dotnet test tests/NEO-e.UnitTests/NEO-e.UnitTests.csproj`
- `dotnet test tests/NEO-e.IntegrationTests/NEO-e.IntegrationTests.csproj`
- Revisao manual dos estados da tela em Windows: inicial, vazio, simulacao, lote em andamento, cancelado e com erro.

## Roadmap

1. Aprovacao e sanitizacao de fixtures fiscais para uso em cenarios locais.
2. DANFSe auxiliar e relatorio PDF com formatos, fontes e rotulagem definidos.
3. Webhooks, OFX, conciliacao financeira, manifestacao em lote e conectores ERP, cada qual com RFC e contrato independente.
