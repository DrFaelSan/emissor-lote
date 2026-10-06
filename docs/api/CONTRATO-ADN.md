# Contrato API ADN (Ambiente de Dados Nacional) - NFS-e

**Versão:** 1.0  
**Data:** 2026-10-05  
**Fonte:** Swagger Oficial ADN - `https://www.nfse.gov.br/swagger/contribuintesissqn/`  
**Ambientes:** Produção Restrita (Homologação) + Produção

---

## 1. Visão Geral

| Item | Especificação |
|------|---------------|
| Protocolo | HTTPS com mTLS (certificado ICP-Brasil A1/A3) |
| Formato troca | JSON (request/response) |
| Formato documentos | XML 1.0 compactado (GZip + Base64) |
| Codificação | UTF-8 |
| Autenticação | Certificado digital do contribuinte (mTLS) |
| Rate Limit | Não publicado oficialmente (recomendado 200-500ms entre calls) |

---

## 2. Endpoints Principais

### 2.1 Distribuição de DFe por NSU

```
GET https://adn.nfse.gov.br/contribuinte/DFe/{NSU}
```

**Parâmetros:**

| Parâmetro | Local | Tipo | Obrigatório | Descrição |
|-----------|-------|------|-------------|-----------|
| NSU | Path | integer | Sim | Número Sequencial Único (cursor) |
| cnpjConsulta | Query | string | Não | CNPJ de consulta (valida raiz com certificado) |

**Headers:**
```
Accept: application/json
```

**Resposta Sucesso (200):**
```json
{
  "ultNSU": 4760,
  "maxNSU": 8920,
  "lote": [
    {
      "NSU": 4711,
      "chaveAcesso": "12345678901234567890123456789012345678901234567890",
      "tipoDocumento": "NFS-e",
      "dataHora": "2026-01-15T09:32:11-03:00",
      "XML": "H4sIAAAAAAAAA+1da3PbthL9..."
    }
  ]
}
```

**Campos do Lote:**
| Campo | Tipo | Descrição |
|-------|------|-----------|
| NSU | long | NSU do documento |
| chaveAcesso | string | Chave de acesso (50 dígitos) |
| tipoDocumento | string | "NFS-e", "Evento", etc. |
| dataHora | string | ISO 8601 com timezone |
| XML | string | Base64(GZip(XML)) |

**Comportamento NSU:**
- Retorna até **50 documentos** a partir do NSU informado (inclusive)
- `ultNSU`: último NSU retornado no lote
- `maxNSU`: maior NSU existente na caixa
- Caixa vazia: `lote: []` (não avança NSU)

### 2.2 Consulta de Eventos por Chave de Acesso

```
GET https://adn.nfse.gov.br/contribuinte/NFSe/{ChaveAcesso}/Eventos
```

**Resposta Sucesso (200):**
```json
{
  "eventos": [
    {
      "tipoEvento": "Canc",
      "dataHora": "2026-01-15T10:00:00-03:00",
      "xmlEvento": "H4sIAAAAAAAAA..."
    }
  ]
}
```

**Not Found (404):** Chave não encontrada ou sem eventos.

---

## 3. Códigos de Erro HTTP

| HTTP | Significado | Ação |
|------|-------------|------|
| 400 | Requisição inválida (NSU malformado, parâmetros) | Corrigir request |
| 401 | Não autenticado (certificado inválido/ausente) | Verificar mTLS + certificado |
| 403 | Sem permissão (certificado não autorizado para CNPJ) | Verificar vínculo certificado-CNPJ |
| 404 | Não encontrado (NSU inexistente, chave não existe) | Verificar parâmetros |
| 429 | Rate limit excedido | Backoff + `Retry-After` |
| 500 | Erro interno servidor | Retry com backoff |
| 503 | Serviço indisponível | Retry com backoff |

---

## 4. Compactação e Decodificação

**Sequência obrigatória:**
1. Receber JSON response
2. Extrair campo `XML` (string Base64)
3. Decodificar Base64 → bytes
4. Descompactar GZip → bytes XML
5. Decodificar UTF-8 → string XML

```csharp
var xmlBytes = Convert.FromBase64String(base64Gzip);
using var gzip = new GZipStream(new MemoryStream(xmlBytes), CompressionMode.Decompress);
using var output = new MemoryStream();
gzip.CopyTo(output);
var xml = Encoding.UTF8.GetString(output.ToArray());
```

---

## 5. URLs por Ambiente

| Ambiente | Base URL | Swagger |
|----------|----------|---------|
| Produção Restrita | `https://adn.producaorestrita.nfse.gov.br/` | `https://adn.producaorestrita.nfse.gov.br/contribuintes/docs/index.html` |
| Produção | `https://adn.nfse.gov.br/` | `https://www.nfse.gov.br/swagger/contribuintesissqn/` |

---

## 6. Fixtures Sanitizadas (Referência)

| Arquivo | Descrição |
|---------|-----------|
| `docs/fixtures/adn-lote-sucesso.json` | Lote com 3 docs variados |
| `docs/fixtures/adn-lote-vazio.json` | Caixa vazia |
| `docs/fixtures/adn-erro-400.json` | Bad Request |
| `docs/fixtures/adn-erro-401.json` | Unauthorized |
| `docs/fixtures/adn-erro-403.json` | Forbidden |
| `docs/fixtures/adn-erro-429.json` | Rate Limited (com Retry-After) |
| `docs/fixtures/adn-erro-500.json` | Server Error |
| `docs/fixtures/adn-payload-invalido.json` | JSON malformado |

---

## 7. Pendências de Validação (Pós-Homologação)

- [ ] Confirmar se NSU informado é **inclusivo** (retorna a partir dele) ou **exclusivo** (próximo)
- [ ] Confirmar semântica exata de `ultNSU` vs `maxNSU` em bordas
- [ ] Validar comportamento com certificado matriz consultando filial
- [ ] Confirmar tipos de documento possíveis em `tipoDocumento`
- [ ] Validar schema XML dos eventos (confirmação tomador, cancelamento, etc.)
- [ ] Medir latência real e limite prático de lote (50 docs)