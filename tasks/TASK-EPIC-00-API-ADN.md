# EPIC 0 - Validacao do contrato da API ADN

Referencia: [TASKS-001-Implementacao-NFSe-WPF.md](../TASKS-001-Implementacao-NFSe-WPF.md)

Objetivo: validar o contrato real da API antes de implementar DTOs e o cliente HTTP. Usar producao restrita e dados sanitizados.

## API-001 - Fixar a fonte oficial do contrato

- Registrar versao/data do manual, Swagger e ambiente usados.
- Registrar URLs oficiais em `docs/api-adn.md`.
- Registrar hash/data do OpenAPI consultado.
- Separar configuracoes de producao restrita e producao.

**Dependencias:** nenhuma.

## API-002 - Confirmar endpoint de distribuicao DFe

- Validar metodo, path, headers, autenticacao e parametros.
- Documentar request e response reais sanitizados.
- Confirmar semantica de `NSU`, `ultNSU` e `maxNSU`.
- Testar caixa vazia, NSU invalido, certificado sem permissao e CNPJ divergente.

**Dependencias:** API-001.

## API-003 - Confirmar schema e compactacao

- Mapear o JSON real e o campo do XML compactado.
- Confirmar a sequencia JSON, Base64, GZip e XML.
- Criar fixtures de sucesso e payload invalido.
- Classificar XML malformado e compactacao invalida.

**Dependencias:** API-002.

## API-004 - Confirmar consulta de eventos

- Verificar se eventos vem na distribuicao DFe ou em consulta adicional.
- Validar endpoint, autenticacao e resposta.
- Registrar decisao de escopo para v1.

**Dependencias:** API-001, API-002.

## API-005 - Criar fixtures e simulador HTTP

- Criar fixtures de lote, lote vazio, 401, 403, 429, 5xx e payload invalido.
- Criar `HttpMessageHandler` fake.
- Garantir que testes nao dependam de internet ou certificado real.

**Dependencias:** API-002, API-003.

## Pronto quando

- O contrato usado pelo codigo esta documentado.
- Existe pelo menos um payload real sanitizado.
- As hipoteses sobre NSU e compactacao foram confirmadas.
