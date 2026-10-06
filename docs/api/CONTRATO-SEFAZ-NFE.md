# Contrato API SEFAZ NF-e (Distribuição + Eventos)

**Versão:** 1.0  
**Data:** 2026-10-05  
**Fonte:** Manual SEFAZ NF-e + Swagger homologação  
**Ambientes:** Homologação + Produção (por UF)

---

## 1. Visão Geral NF-e

| Item | Especificação |
|------|---------------|
| Protocolo | HTTPS com mTLS (certificado ICP-Brasil A1/A3) |
| Formato troca | SOAP 1.2 (eventos) / JSON (distribuição) |
| Formato documentos | XML 1.0 (assinado XMLDSIG) |
| Padrão assinatura | XMLDSIG (W3C) |
| Autoridade | SEFAZ estadual + Receita Federal |

---

## 2. Endpoints Principais

### 2.1 Distribuição DF-e por NSU (Destinatário)

```
POST https://nfe.sefaz.<UF>.gov.br/nfe/distribuicao/DFePorNSU
```

**SOAP Request:**
```xml
<soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope">
  <soap:Header/>
  <soap:Body>
    <distDFeInt xmlns="http://www.portalfiscal.inf.br/nfe/wsdl/DistribuicaoDFe">
      <nfeDadosMsg>
        <distDFeInt versao="1.01" xmlns="http://www.portalfiscal.inf.br/nfe">
          <tpAmb>2</tpAmb>
          <cUFAutor>35</cUFAutor>
          <CNPJ>12345678000199</CNPJ>
          <distNSU>
            <ultNSU>000000000000047</ultNSU>
          </distNSU>
        </distDFeInt>
      </nfeDadosMsg>
    </distDFeInt>
  </soap:Body>
</soap:Envelope>
```

### 2.2 Eventos de Manifestação do Destinatário

| Código | Evento | Descrição | Conclusivo |
|--------|--------|-----------|------------|
| 210210 | Ciência da Operação | Tomou conhecimento | Não |
| 210200 | Confirmação da Operação | Confirma operação | Sim |
| 210220 | Desconhecimento da Operação | Não reconhece | Sim |
| 210240 | Operação não Realizada | Não se efetivou | Sim |

**Endpoint Envio Evento:**
```
POST https://nfe.sefaz.<UF>.gov.br/nfe/autorizacao/RecepcaoEvento
```

**SOAP Request (Exemplo Confirmação):**
```xml
<soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope">
  <soap:Header/>
  <soap:Body>
    <recepcaoEvento xmlns="http://www.portalfiscal.inf.br/nfe/wsdl/RecepcaoEvento">
      <nfeDadosMsg>
        <evento xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.00">
          <infEvento Id="ID1101101234567890123456789012345678901234567890">
            <cOrgao>35</cOrgao>
            <tpAmb>2</tpAmb>
            <CNPJ>12345678000199</CNPJ>
            <chNFe>35260112345678000199550010000000011000000001</chNFe>
            <dhEvento>2026-01-15T10:00:00-03:00</dhEvento>
            <tpEvento>210200</tpEvento>
            <nSeqEvento>1</nSeqEvento>
            <detEvento versaoEvento="1.00">
              <descEvento>Confirmação da Operação</descEvento>
            </detEvento>
          </infEvento>
          <Signature xmlns="http://www.w3.org/2000/09/xmldsig#">...</Signature>
        </evento>
      </nfeDadosMsg>
    </recepcaoEvento>
  </soap:Body>
</soap:Envelope>
```

---

## 3. Códigos de Retorno SEFAZ (Exemplos)

| Código | Descrição |
|--------|-----------|
| 135 | Evento registrado e vinculado à NF-e |
| 573 | Duplicidade de evento (já manifestado) |
| 574 | Chave de acesso inexistente |
| 575 | NF-e não está autorizada |
| 576 | Prazo de manifestação expirado |
| 577 | Evento anterior incompatível (ex: já confirmado) |
| 578 | Justificativa obrigatória para Operação não Realizada |

---

## 4. Prazos (Conforme NT Vigente)

| Evento | Prazo | Início Contagem |
|--------|-------|-----------------|
| Ciência (210210) | ~10 dias | Autorização NF-e |
| Confirmação (210200) | 90 dias (NT atual) ou 180 (anterior) | Autorização NF-e |
| Desconhecimento (210220) | 90/180 dias | Autorização NF-e |
| Op. não Realizada (210240) | 90/180 dias | Autorização NF-e |

> **Nota:** Sistema deve exibir prazo restante e alertar. Parametrizar por NT.

---

## 5. Fixtures Sanitizadas

| Arquivo | Descrição |
|---------|-----------|
| `docs/fixtures/sefaz-distribuicao-sucesso.xml` | Resposta distribuição com NF-e |
| `docs/fixtures/sefaz-distribuicao-vazio.xml` | Resposta vazia |
| `docs/fixtures/sefaz-evento-210210-sucesso.xml` | Ciência aceita |
| `docs/fixtures/sefaz-evento-210200-sucesso.xml` | Confirmação aceita |
| `docs/fixtures/sefaz-evento-210220-sucesso.xml` | Desconhecimento aceito |
| `docs/fixtures/sefaz-evento-210240-sucesso.xml` | Op. não Realizada aceita |
| `docs/fixtures/sefaz-evento-duplicidade.xml` | Código 573 |
| `docs/fixtures/sefaz-evento-prazo-expirado.xml` | Código 576 |
| `docs/fixtures/sefaz-evento-incompatível.xml` | Código 577 |

---

## 6. Pendências de Validação

- [ ] Confirmar endpoint exato por UF (homologação vs produção)
- [ ] Validar schema SOAP/WSDL atual
- [ ] Confirmar sequência eventos permitida (Ciência → Conclusivo)
- [ ] Validar justificativa obrigatória Op. não Realizada
- [ ] Confirmar certificado A1 vs A3 para eventos
- [ ] Testar rejeições mais comuns (573, 576, 577)