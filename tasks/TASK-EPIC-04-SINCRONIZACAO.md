# EPIC 4 - NSU, lote e idempotencia

Referencia: [TASKS-001-Implementacao-NFSe-WPF.md](../TASKS-001-Implementacao-NFSe-WPF.md)

## SYNC-001 - Modelar estado por CNPJ

Criar estado com CNPJ, ultimo NSU confirmado, ultima execucao e versao. NSU negativo deve ser rejeitado e cada CNPJ deve possuir estado independente.

## SYNC-002 - Persistir estado atomicamente

Implementar SQLite ou JSON atomico. Usar transacao ou temporario + replace. Detectar corrupcao, criar backup e produzir erro acionavel.

## SYNC-003 - Implementar cursor de pagina

Usar `ultimoNsu`, `ultNSU` e `maxNSU` conforme contrato real. Avancar somente apos gravar todos os documentos. Impedir loop quando a API nao progride.

## SYNC-004 - Descompactar e validar XML

Decodificar Base64/GZip, validar XML e extrair metadados. Falha de um documento deve ser classificavel e nao confirmar NSU indevidamente.

## SYNC-005 - Escritor idempotente

Gravar em arquivo temporario e renomear atomicamente. Usar chave/identificador validado, ignorar conteudo identico e bloquear sobrescrita silenciosa de conteudo diferente.

## SYNC-006 - Sincronizar uma empresa

Orquestrar certificado, cursor, cliente, parser e escritor. Emitir progresso por CNPJ/NSU e manter ultimo NSU confirmado ao cancelar.

## SYNC-007 - Sincronizar carteira

Processar empresas selecionadas serialmente no MVP. Uma falha nao deve apagar resultados anteriores. Resultado final deve separar sucesso, erro, ignoradas e canceladas.

## Pronto quando

- Falhas podem ser retomadas sem perda de documentos.
- NSU so avanca depois da persistencia.
- Repetir a execucao nao duplica arquivos.
