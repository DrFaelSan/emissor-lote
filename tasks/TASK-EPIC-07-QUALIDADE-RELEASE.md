# EPIC 7 - Qualidade, seguranca e distribuicao

Referencia: [TASKS-001-Implementacao-NFSe-WPF.md](../TASKS-001-Implementacao-NFSe-WPF.md)

## QA-001 - Testes unitarios

Cobrir cursor NSU, idempotencia, cancelamento, validacoes e erros criticos.

## QA-002 - Testes de integracao

Usar servidor/handler fake para testar fluxo HTTP, mTLS simulado, retry, 429, 5xx, payload invalido e timeout.

## QA-003 - Retomada e resiliencia

Simular interrupcao em cada etapa e provar que o proximo ciclo nao perde nem confirma NSU indevidamente.

## QA-004 - Auditoria de logs e segredos

Executar busca automatizada e revisao manual para confirmar ausencia de senha, chave privada e XML completo.

## QA-005 - Performance controlada

Medir parsing, escrita, rede e memoria separadamente. Nao prometer throughput acima do limite observado da API.

## REL-001 - Publicacao Windows

Definir self-contained/framework-dependent, instalador e atualizacao. Testar instalacao limpa, upgrade, desinstalacao e permissoes no Windows 10/11.

## REL-002 - Homologacao

Criar checklist com certificado, ambiente, permissoes, lote pequeno, cancelamento, retomada e exportacao.

## Pronto quando

- O MVP possui testes de unidade, integracao e retomada.
- Nenhum segredo aparece nos logs.
- A distribuicao foi validada em ambiente Windows limpo.
