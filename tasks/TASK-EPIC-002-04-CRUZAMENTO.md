# EPIC 04 - Cruzamento de valores

## 002-XVAL-001 - Definir contrato de importacao

Definir CSV e Excel, encoding, separador, cabecalhos, colunas obrigatorias e mapeamento de moeda/data.

**Aceite:** arquivo invalido mostra linha/coluna; valores usam `decimal`, nunca `double`.

## 002-XVAL-002 - Importar CSV

Implementar importacao de CNPJ, valor, data, chave opcional, numero, serie e observacao.

**Aceite:** preview, validacao e relatorio de linhas rejeitadas.

## 002-XVAL-003 - Importar Excel

Usar biblioteca aprovada, com limite de tamanho, selecao de planilha e preview.

**Aceite:** planilha nao executa macros nem altera o arquivo original.

## 002-XVAL-004 - Match por chave

Comparar chave exata com prioridade maxima e detectar duplicidade na referencia.

**Aceite:** match unico, ausente e multiplo possuem resultados distintos.

## 002-XVAL-005 - Match por emitente/numero/serie

Aplicar segunda estrategia quando nao houver chave, com regras configuraveis.

**Aceite:** conflito entre estrategias e marcado como ambiguo, nao como OK.

## 002-XVAL-006 - Match por data/valor e totais

Implementar tolerancia absoluta e percentual, alem de comparacao por periodo e soma.

**Aceite:** arredondamento documentado; exibir esperado, encontrado e delta.

## 002-XVAL-007 - Classificacao e revisao

Classificar OK, Divergente, Sem referencia e Multiplos matches. Exigir revisao humana para divergencias antes de qualquer confirmacao.

**Aceite:** resultado e reproduzivel, filtravel e auditavel.
