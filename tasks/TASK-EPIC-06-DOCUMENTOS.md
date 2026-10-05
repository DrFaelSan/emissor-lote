# EPIC 6 - Lacunas, documentos e relatorios

Referencia: [TASKS-001-Implementacao-NFSe-WPF.md](../TASKS-001-Implementacao-NFSe-WPF.md)

## DOC-001 - Inventario de NSUs

Persistir CNPJ, NSU, chave, tipo e caminho. Detectar duplicatas. Documentar que uma lacuna pode representar NSU sem documento quando a API nao garante continuidade documental.

## DOC-002 - Analise de lacunas

Calcular intervalos candidatos com base no inventario e estado da API. Resultado somente leitura e exibido com quantidade e intervalos.

## DOC-003 - Recuperacao assistida

Reconsultar NSUs selecionados com confirmacao, rate limit, retry, idempotencia e cancelamento. Aplicar as mesmas regras de confirmacao do fluxo normal.

## DOC-004 - Excel/CSV e relatorio

Exportar resultado, status, CNPJ, NSU, chave e timestamps sem dados sensiveis desnecessarios. Operacao deve ser assincrona e tratar falta de permissao.

## DOC-005 - DANFSe

Implementar depois de confirmar biblioteca, contrato e licenca. PDF deve ser associado ao XML, sem substituir o original; XML invalido nao derruba o lote.

## Pronto quando

- O inventario permite auditoria da caixa por CNPJ.
- Lacunas sao identificadas como candidatas, sem falsa garantia.
- Exportacao e recuperacao sao cancelaveis e idempotentes.
