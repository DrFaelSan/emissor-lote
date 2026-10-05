# EPIC 01 - Modelo de documentos e leitura dos XMLs

## 002-DOC-001 - Modelar documento recebido

Criar modelo comum com tipo, chave, CNPJ emitente/destinatario, numero, serie, data de emissao, autorizacao, valor, status e origem.

**Aceite:** propriedades obrigatorias e opcionais sao distintas; CNPJ, chave e valores sao validados.

## 002-DOC-002 - Implementar parser NF-e

Ler XML autorizado de NF-e, namespaces, emitente, destinatario, total e informacoes de autorizacao sem depender de posicao textual.

**Aceite:** parser possui fixtures de namespaces, campos ausentes e valores monetarios.

## 002-DOC-003 - Implementar parser NFS-e

Ler os schemas reais suportados pelo RFC-001 e extrair tomador, prestador, chave, competencia, valor e eventos disponiveis.

**Aceite:** schema/versionamento documentados e XML desconhecido classificado como nao suportado.

## 002-DOC-004 - Indexar XMLs do RFC-001

Varredura incremental da arvore de pastas, com hash, tamanho, data, caminho e status de parsing.

**Aceite:** reprocessamento e idempotente; XML invalido nao interrompe a indexacao inteira.

## 002-DOC-005 - Identificar notas recebidas

Classificar destinatario/tomador conforme o CNPJ da empresa e separar notas emitidas pela propria empresa.

**Aceite:** matriz/filial e CNPJ raiz obedecem a regra configurada e casos ambiguos ficam pendentes.
