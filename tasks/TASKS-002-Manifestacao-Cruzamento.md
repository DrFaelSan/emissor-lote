# TASKS-002: Manifestacao e Cruzamento de Valores

Backlog tecnico derivado da RFC-002 para C# / .NET 8 / WPF, integrado ao RFC-001.

## Premissas

- RFC-001 fornece certificados, carteira, armazenamento de XML, logging e infraestrutura comum.
- NF-e e NFS-e possuem contratos, eventos, prazos e autoridades potencialmente diferentes.
- Nenhum evento ou prazo deve ser implementado com base apenas nesta RFC; validar manual, schemas e ambiente de homologacao.
- Acoes conclusivas exigem confirmacao explicita e trilha de auditoria.
- O MVP comeca em modo somente leitura e avanca para manifestacao apos contrato validado.
- NF-e e NFS-e devem possuir adaptadores separados ate que equivalencia de contrato seja comprovada.
- A ausencia de evento nao deve ser interpretada automaticamente como aceite ou rejeicao.
- O cruzamento de valores e apoio a decisao; nunca autoriza evento fiscal sozinho.

## Status e criterios de prontidao

Estados permitidos: `Backlog`, `Ready`, `In Progress`, `Blocked`, `In Review` e `Done`.

Uma task de envio de evento so pode entrar em `Ready` depois de existir fixture valida, schema confirmado, ambiente de homologacao e regra de transicao documentada. Uma task de producao exige aprovacao tecnica e fiscal separadas.

## Definition of Done

- Testes automatizados ou evidencia reproduzivel.
- Nenhuma senha, chave privada ou token em log.
- Toda manifestacao registra empresa, documento, evento, usuario local, timestamp, request sanitizado, resposta e resultado.
- Datas e valores usam tipos apropriados: `DateTimeOffset` e `decimal`.
- Operacoes de rede e lote aceitam `CancellationToken`.
- Eventos duplicados, rejeitados e ambiguos nao sao tratados como sucesso.
- Sem emojis, comentarios de codigo ou dados fiscais reais em arquivos versionados.
- Toda task informa validacao, risco e escopo negativo.

## Gates especificos

### Gate M1 - Contrato fiscal

- Evento, schema, autoridade, endpoint, assinatura, prazo e rejeicoes identificados.
- NF-e e NFS-e possuem contrato e estado separados quando necessario.

### Gate M2 - Decisao humana

- Eventos conclusivos exigem resumo, confirmacao explicita e registro da decisao.
- Divergencia, ambiguidade ou falta de referencia bloqueia confirmacao automatica.

### Gate M3 - Auditoria

- Request sanitizado, resposta, codigo, protocolo, certificado por thumbprint, usuario local e timestamp persistidos.
- Tentativas sem sucesso nao sao registradas como manifestacao concluida.

### Gate M4 - Reprocessamento

- Consulta, importacao e envio sao idempotentes.
- Cancelamento nao gera estado confirmado.

## Epicos

| Epico | Escopo | Arquivo |
|---|---|---|
| 00 | Contratos, regras fiscais e ambientes | [EPIC-00](TASK-EPIC-002-00-CONTRATO.md) |
| 01 | Modelo de documentos e leitura dos XMLs | [EPIC-01](TASK-EPIC-002-01-DOCUMENTOS.md) |
| 02 | Consulta e envio de manifestacoes | [EPIC-02](TASK-EPIC-002-02-MANIFESTACAO.md) |
| 03 | Persistencia e auditoria | [EPIC-03](TASK-EPIC-002-03-PERSISTENCIA-AUDITORIA.md) |
| 04 | Cruzamento de valores | [EPIC-04](TASK-EPIC-002-04-CRUZAMENTO.md) |
| 05 | Interface WPF | [EPIC-05](TASK-EPIC-002-05-WPF.md) |
| 06 | Relatorios, testes e homologacao | [EPIC-06](TASK-EPIC-002-06-QUALIDADE.md) |

## Ordem sugerida do MVP

1. `002-API-001` a `002-API-006`
2. `002-DOC-001` a `002-DOC-005`
3. `002-CORE-001` a `002-CORE-004`
4. `002-MAN-001` a `002-MAN-006`
5. `002-AUD-001` a `002-AUD-004`
6. `002-UI-001` a `002-UI-006`
7. `002-XVAL-001` a `002-XVAL-007`
8. `002-REL-001` a `002-REL-005`

## Validacoes obrigatorias por fase

| Fase | Validacao |
|---|---|
| Contrato | Fixtures e comparacao com schema oficial |
| Somente leitura | Indexacao de XMLs anonimizados e filtros |
| Manifestacao | Homologacao com evento aceito e rejeitado |
| Cruzamento | Casos OK, divergente, sem referencia e multiplo |
| Auditoria | Inspecao de logs, banco e exportacoes |
| Release | Instalacao limpa, backup, retomada e bloqueio de producao por padrao |

## Fora do MVP

- Emissao ou cancelamento de notas.
- CT-e.
- Integracao bidirecional com ERP.
- Manifestacao automatica sem aprovacao humana.
- Execucao paralela antes da validacao de limites e regras das autoridades.
- Suporte multiusuario remoto.

## Riscos que bloqueiam o envio

- Endpoint ou schema de evento nao confirmado.
- Certificado sem vinculo comprovado com o destinatario.
- Prazo fiscal sem fonte normativa registrada.
- Evento anterior incompatível com a transicao solicitada.
- Match de valor ambiguo tratado como unico.
- Resposta de sucesso sem protocolo ou identificador persistivel.
