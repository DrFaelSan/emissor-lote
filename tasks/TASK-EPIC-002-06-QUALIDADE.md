# EPIC 06 - Relatorios, testes e homologacao

## 002-REL-001 - Relatorio de pendencias

Exportar notas pendentes, status, prazo, empresa, emitente, valor e ultimo erro.

**Aceite:** CSV/Excel nao inclui senha, chave privada ou dados alem do necessario.

## 002-REL-002 - Relatorio de manifestacoes

Exportar eventos enviados, resultado, protocolo/codigo de retorno, timestamp e usuario local.

**Aceite:** resposta rejeitada nao aparece como manifestacao concluida.

## 002-REL-003 - Relatorio de divergencias

Exportar valor da nota, valor esperado, delta, tolerancia, estrategia de match e revisao.

**Aceite:** totais batem com a grid e podem ser reproduzidos.

## 002-REL-004 - Testes automatizados

Cobrir parsers, estados, prazos, idempotencia, importacao, matching, retry, cancelamento e auditoria.

**Aceite:** testes nao usam dados fiscais reais.

## 002-REL-005 - Homologacao fiscal e tecnica

Executar cenarios com NF-e e NFS-e em ambientes oficiais, incluindo rejeicoes, prazo, duplicidade, certificado errado e cancelamento.

**Aceite:** checklist assinado pela area tecnica e fiscal; producao bloqueada sem aprovacao.

## 002-REL-006 - Performance

Medir carga de 10.000 notas, filtros, importacao, cruzamento e memoria.

**Aceite:** UI permanece responsiva e gargalos sao documentados; nao confundir benchmark local com limite da API.

## 002-REL-007 - Seguranca e compliance

Revisar logs, armazenamento, acesso local, exportacoes e retenção conforme LGPD.

**Aceite:** evidencia de que segredos nao aparecem em arquivos, excecoes ou relatorios.
