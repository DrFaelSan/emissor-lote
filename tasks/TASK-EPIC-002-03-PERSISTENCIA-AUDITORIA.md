# EPIC 03 - Persistencia e auditoria

## 002-AUD-001 - Schema SQLite

Criar tabelas para documentos, eventos, estados, referencias de valores, matches, execucoes e erros.

**Aceite:** indices por CNPJ, chave, tipo, data e status; migrations versionadas.

## 002-AUD-002 - Persistir eventos e respostas

Guardar evento solicitado, hash do payload, timestamp, certificado identificado por thumbprint, status HTTP/codigo e resposta sanitizada.

**Aceite:** nunca persistir senha, chave privada ou XML completo desnecessariamente.

## 002-AUD-003 - Trilha de auditoria local

Registrar usuario local, acao, motivo, documento, antes/depois, origem e resultado.

**Aceite:** auditoria e append-only para o usuario da aplicacao; alteracoes manuais ficam identificadas.

## 002-AUD-004 - Retomada e concorrencia

Garantir que duas execucoes nao manifestem a mesma nota simultaneamente e que cancelamento preserve o ultimo estado confirmado.

**Aceite:** lock por empresa/documento e testes de interrupcao.
