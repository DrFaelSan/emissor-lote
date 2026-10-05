# EPIC 3 - Cliente ADN e resiliencia HTTP

Referencia: [TASKS-001-Implementacao-NFSe-WPF.md](../TASKS-001-Implementacao-NFSe-WPF.md)

## HTTP-001 - Implementar cliente tipado

Criar `IAdnClient` e metodo tipado para buscar pagina por NSU. Separar DTOs da API do dominio. Configurar timeout, `Accept`, correlation id e descarte correto do response body.

## HTTP-002 - Mapear status e erros de negocio

Mapear 400, 401, 403, 404, 409, 429 e 5xx. Preservar codigo e mensagem sanitizada. 401/403 nao devem sofrer retry automatico.

## HTTP-003 - Implementar retry e backoff

Aplicar retry somente a 429 e falhas transitorias/5xx, com no maximo 3 tentativas, jitter e suporte a `Retry-After`. Cancelamento deve interromper o backoff.

## HTTP-004 - Implementar rate limiting local

Limitar chamadas por empresa e globalmente. Registrar chamadas, retries e 429. Nenhuma chamada pode ocorrer apos cancelamento.

## HTTP-005 - Smoke test em producao restrita

Executar chamada autenticada com certificado de homologacao, comparar response real com fixtures e registrar latencia, headers e limite de lote em evidencia sanitizada.

## Pronto quando

- O cliente tipado chama o endpoint confirmado.
- Erros transitorios sao repetidos de forma limitada.
- A politica de rate limit e observavel e testada.
