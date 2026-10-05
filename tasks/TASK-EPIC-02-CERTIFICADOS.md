# EPIC 2 - Certificados A1 e mTLS

Referencia: [TASKS-001-Implementacao-NFSe-WPF.md](../TASKS-001-Implementacao-NFSe-WPF.md)

## CERT-001 - Descobrir arquivos PFX

Ler pasta configurada, aceitar `.pfx`/`.p12`, detectar duplicados e expor subject, issuer, validade e thumbprint quando disponivel. Nao carregar senhas automaticamente nem exportar chave privada.

## CERT-002 - Carregar PFX com seguranca

Carregar com senha informada e flags adequadas. Tratar senha incorreta, ausencia de chave privada, certificado expirado e certificado ainda nao valido sem interromper as demais empresas.

## CERT-003 - Validar vinculo certificado-CNPJ

Extrair o CNPJ segundo o contrato confirmado da API/certificado. Documentar a regra, testar divergencias e impedir consulta quando o vinculo nao for confiavel.

## CERT-004 - Criar handler mTLS por empresa

Criar `HttpClient`/handler proprio por empresa, anexar o certificado ao request e descartar recursos ao final. Nunca desabilitar validacao TLS globalmente. Testar a selecao do certificado.

## Pronto quando

- Certificados podem ser listados e validados sem senha persistida.
- O CNPJ do certificado e conferido antes da chamada.
- O cliente HTTP usa o certificado correto por empresa.
