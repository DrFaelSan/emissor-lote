# EPIC 00 - Contratos, regras fiscais e ambientes

## 002-API-001 - Separar contratos por documento

Definir os limites entre NF-e, NFS-e Nacional e RFC-001. Criar configuracoes, DTOs, clientes e eventos separados quando os contratos forem diferentes.

**Aceite:** nenhum endpoint de NF-e e usado para NFS-e por heranca implícita; escopo aprovado para o MVP.

## 002-API-002 - Validar consulta de notas recebidas NF-e

Confirmar endpoint, autorizacao, distribuicao para destinatario, parametros de NSU, schemas, certificados e ambiente de homologacao da SEFAZ.

**Aceite:** request/response reais sanitizados, codigos de retorno documentados e sem dependencia de portal web.

## 002-API-003 - Validar eventos de manifestacao NF-e

Confirmar SOAP/REST, schema XML, assinatura, eventos 210210, 210200, 210220 e 210240, regras de sequencia e rejeicoes.

**Aceite:** cada evento possui payload, certificado, endpoint, retorno e regra de retry documentados.

## 002-API-004 - Validar consulta e eventos NFS-e

Confirmar no ADN/SEFIN quais eventos de tomador existem, como consultar eventos e como registrar confirmacao.

**Aceite:** eventos NFS-e nao sao tratados como equivalentes automaticos aos eventos NF-e; feature flag se o contrato ainda nao estiver estavel.

## 002-API-005 - Confirmar prazos e regras fiscais

Validar prazos, inicio da contagem, eventos conclusivos, justificativa obrigatoria e efeitos da ausencia de manifestacao conforme NT vigente.

**Aceite:** prazos sao configuraveis por tipo de documento/evento e fonte normativa fica registrada.

## 002-API-006 - Criar fixtures e simuladores

Criar fixtures de sucesso, duplicidade, prazo expirado, rejeicoes, timeout, 401/403/429/5xx e respostas invalidas.

**Aceite:** testes de contrato nao dependem de internet, certificado real ou dados pessoais.

## Dependencias

Nenhuma. Este epico bloqueia o envio de eventos em producao.
