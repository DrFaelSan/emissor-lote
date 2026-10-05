# NEO-e

Aplicacao desktop Windows para sincronizacao de documentos fiscais eletronicos, organizacao de XMLs, manifestacao de documentos recebidos e cruzamento de valores.

## Objetivo

O projeto atende dois fluxos relacionados:

- RFC-001: download em massa de NFS-e via API Nacional, com certificado A1, NSU persistido e armazenamento idempotente.
- RFC-002: leitura de notas recebidas, consulta de eventos de manifestacao, envio controlado de eventos e cruzamento com valores de referencia.

A aplicacao deve ser Windows-first, local, auditavel e sem envio de certificados ou XMLs para um servidor proprio.

## Estado atual

O repositorio esta na fase de especificacao e planejamento tecnico. As RFCs e os backlogs estao sendo validados antes da criacao da solution .NET.

Documentos principais:

- [RFC Projeto.md](RFC%20Projeto.md): proposta inicial do produto.
- [RFC-001-Download-Massa-NFSe.md](RFC-001-Download-Massa-NFSe.md): download em massa de NFS-e.
- [RFC-002-Manifestacao-Cruzamento-Valores.md](RFC-002-Manifestacao-Cruzamento-Valores.md): manifestacao e cruzamento de valores.
- [TASKS-001-Implementacao-NFSe-WPF.md](TASKS-001-Implementacao-NFSe-WPF.md): backlog do RFC-001.
- [TASKS-002-Manifestacao-Cruzamento.md](tasks/TASKS-002-Manifestacao-Cruzamento.md): backlog do RFC-002.

## Stack planejada

- C# e .NET 8.
- WPF com MVVM.
- SQLite para estado, inventario e auditoria.
- `HttpClient` com `SocketsHttpHandler` para mTLS.
- `X509Certificate2` para certificados A1.
- `System.IO.Compression` para documentos compactados.
- Serilog ou adaptador de logging estruturado.
- CSV nativo e uma biblioteca aprovada para leitura de XLSX.

A biblioteca de UI, persistencia, logging e leitura de XLSX deve ser registrada em uma decisao tecnica antes do uso em producao.

## Arquitetura planejada

```text
src/
  NeoE.App/              WPF, Views, ViewModels e recursos visuais
  NeoE.Application/      Casos de uso, comandos e orquestracao
  NeoE.Domain/           Entidades, value objects e regras
  NeoE.Contracts/        DTOs externos e contratos de serializacao
  NeoE.Infrastructure/  HTTP, certificados, SQLite, XML e filesystem

tests/
  NeoE.UnitTests/
  NeoE.IntegrationTests/
  NeoE.ContractTests/

docs/
  api/
  decisions/
  evidence/
```

Dependencias devem apontar para dentro: a camada de dominio nao conhece WPF, HTTP, SQLite ou bibliotecas de apresentacao.

## Fluxos principais

### Download de NFS-e

1. Carregar configuracao e ambiente.
2. Descobrir certificados e vinculos com empresas.
3. Validar certificado, senha e destino.
4. Consultar a API usando o NSU persistido por CNPJ.
5. Decodificar e validar cada documento.
6. Gravar XML de forma atomica e idempotente.
7. Confirmar o novo NSU somente depois da gravacao.
8. Emitir resultado por empresa e por documento.

### Manifestacao

1. Indexar XMLs recebidos do RFC-001.
2. Consultar eventos atuais quando o contrato permitir.
3. Exibir documento, prazo, valor e resultado do cruzamento.
4. Exigir revisao humana antes de eventos conclusivos.
5. Enviar o evento com o certificado do destinatario correto.
6. Persistir resposta, protocolo, usuario local e timestamp.

### Cruzamento de valores

A prioridade de matching deve ser: chave exata, emitente/numero/serie, emitente/data/valor e totais por periodo. Ambiguidade nunca deve ser classificada como `OK` automaticamente.

## Ambientes

A aplicacao deve possuir pelo menos:

- Producao restrita/homologacao.
- Producao.
- Testes locais sem rede, usando fixtures e handlers HTTP falsos.

Producao nao pode ser o valor padrao. A troca de ambiente deve ser visivel e exigir confirmacao antes de operacoes com efeito fiscal.

## Seguranca

- Nunca registrar senha, chave privada, token, XML completo ou payload sensivel em log.
- Nunca desabilitar validacao de certificado TLS globalmente.
- Usar DPAPI ou Windows Credential Manager quando uma credencial precisar ser persistida.
- Carregar o certificado correto para cada CNPJ e validar validade e chave privada.
- Tratar justificativas e dados fiscais como informacao restrita.
- Usar arquivos temporarios com permissao adequada e remover temporarios no sucesso ou falha.
- Manter eventos de manifestacao auditaveis e append-only para o usuario local.

## Qualidade

Cada task deve ter uma verificacao proporcional ao risco:

- Dominio e matching: testes unitarios.
- HTTP e serializacao: testes de contrato com fixtures.
- mTLS e ambiente: smoke test controlado em homologacao.
- Estado NSU e manifestacao: testes de interrupcao, retomada e idempotencia.
- WPF: testes de ViewModel e verificacao manual dos estados de tela.
- Distribuicao: instalacao limpa, upgrade, desinstalacao e permissao de filesystem.

Antes de implementar um endpoint, registrar manual, Swagger, versao, ambiente, request e response sanitizados em `docs/api/`.

## Como iniciar a implementacao

1. Fechar as tasks de contrato da API.
2. Criar a solution .NET 8 conforme a estrutura planejada.
3. Configurar build, testes, analyzers e tratamento de warnings.
4. Implementar contratos e fixtures antes de chamadas reais.
5. Implementar o fluxo de uma empresa antes do processamento da carteira.
6. Validar homologacao com dados controlados.
7. Habilitar operacoes conclusivas somente apos aprovacao tecnica e fiscal.

Os comandos definitivos de build, teste, formatacao e publicacao devem ser adicionados aqui quando a solution for criada.

## Convencoes de arquivos

- Codigo e documentacao tecnica usam ASCII por padrao.
- Nomes de tipos e membros seguem PascalCase; variaveis e parametros seguem camelCase.
- Identificadores de task usam prefixo do epico, por exemplo `SYNC-003` ou `002-MAN-005`.
- Decisoes que alteram contrato devem gerar documento em `docs/decisions/`.
- Evidencias de homologacao devem ser sanitizadas antes de entrar no repositorio.
