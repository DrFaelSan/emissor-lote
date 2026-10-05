# EPIC 02 - Consulta e envio de manifestacoes

## 002-MAN-001 - Consultar status atual

Consultar eventos existentes, consolidar o ultimo evento aceito e calcular estado atual sem confundir tentativa com sucesso.

**Aceite:** status distingue Pendente, Ciencia, Confirmada, Desconhecida, Nao Realizada, Rejeitada e Prazo Expirado.

## 002-MAN-002 - Implementar politica de transicao

Criar maquina de estados por tipo de documento e evento, baseada no contrato validado.

**Aceite:** transicoes invalidas sao bloqueadas antes da chamada; estado aceito so muda apos resposta positiva.

## 002-MAN-003 - Montar evento NF-e

Implementar DTO/XML de cada evento permitido, incluindo justificativa quando obrigatoria e dados do destinatario.

**Aceite:** schema valida localmente antes do envio e assinatura usa o certificado correto.

## 002-MAN-004 - Montar evento NFS-e

Implementar somente os eventos confirmados no `002-API-004`, isolados do fluxo NF-e.

**Aceite:** feature flag impede envio quando o contrato nao estiver homologado.

## 002-MAN-005 - Enviar manifestacao com mTLS

Usar infraestrutura de certificados do RFC-001, correlation id, timeout, retry seletivo e `CancellationToken`.

**Aceite:** 400/401/403/rejeicoes de negocio nao sofrem retry cego; 429/5xx seguem politica aprovada.

## 002-MAN-006 - Idempotencia e lote seguro

Evitar reenviar evento ja aceito e processar lote com resultado individual por nota.

**Aceite:** falha em uma nota nao confirma as demais; confirmacao de evento conclusivo exige segunda etapa na UI.
