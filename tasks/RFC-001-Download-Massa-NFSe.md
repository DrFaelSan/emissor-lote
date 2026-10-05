# RFC-001: Download em Massa de NFS-e (Notas Fiscais de Serviço)

| Campo | Valor |
|---|---|
| **Status** | Implemented (v1.11.0) |
| **Autor** | [Seu nome] |
| **Data** | 2025-01 |
| **Revisores** | [Nomes] |
| **Versão do documento** | 1.0 |
| **Versão do aplicativo** | 1.11.0 |

---

## 1. Resumo Executivo

Este documento descreve a especificação técnica e o estado atual de um **aplicativo desktop Windows** (WinForms / .NET 8) capaz de realizar **download em massa de XMLs de NFS-e** — emitidas, recebidas e eventos — através da **API pública nacional (ADN)**, autenticando-se por **certificado digital A1** de cada empresa da carteira.

O aplicativo substitui uma solução anterior baseada em extensão de navegador que operava **uma empresa por sessão** e exigia reinicialização do navegador para trocar de certificado. Essa limitação inviabilizava o processo em carteiras com dezenas de empresas e gerava alto custo operacional para escritórios contábeis e departamentos jurídicos/fiscais.

**Resultado entregue:** percorrer toda a carteira em uma única execução, com retomada por NSU, organização automática na árvore de pastas já usada pela equipe fiscal, análise de lacunas, geração de DANFSe e exportação para conferência.

---

## 2. Contexto e Motivação

### 2.1 Cenário Anterior (As-Is)

- Captura de NFS-e realizada por **extensão de navegador**.
- A extensão suportava **uma única empresa por sessão**.
- Trocar de empresa exigia **fechar e reabrir o navegador** para carregar um novo certificado A1.
- Para carteiras com **dezenas de empresas**, o tempo operacional crescia linearmente com o número de CNPJs.
- Não havia controle de progresso, retomada ou organização consistente dos arquivos.

### 2.2 Problemas Identificados

| # | Problema | Impacto |
|---|---|---|
| P1 | Uma empresa por sessão de navegador | Não escala |
| P2 | Reinício manual do navegador entre empresas | Erro humano, tempo perdido |
| P3 | Sem controle de progresso / retomada | Reprocessamento e perda de XMLs |
| P4 | Armazenamento desorganizado | Retrabalho da equipe fiscal |
| P5 | Sem suporte a múltiplos certificados em lote | Consultas manuais por CNPJ |
| P6 | Ausência de análise de lacunas (NSU faltantes) | Inconsistência na caixa fiscal |
| P7 | Falta de relatórios e exportação para conferência | Trabalho manual adicional |

### 2.3 Objetivos Alcançados

| ID | Objetivo | Status |
|---|---|---|
| O1 | Percorrer toda a carteira de empresas em uma única execução | ✅ |
| O2 | Utilizar o certificado A1 correto por empresa, sem intervenção manual | ✅ |
| O3 | Retomar sincronização por **NSU** individualizado por CNPJ | ✅ |
| O4 | Persistir XMLs na **mesma estrutura de pastas** já usada pela equipe fiscal | ✅ |
| O5 | Carregar múltiplos certificados `.pfx` a partir de uma pasta | ✅ |
| O6 | Analisar e recuperar lacunas de NSU | ✅ |
| O7 | Gerar DANFSe e exportar dados para Excel | ✅ |
| O8 | Emitir logs e relatórios por execução | ✅ |

### 2.4 Não-Objetivos (Out of Scope na versão atual)

- Emissão de NFS-e.
- Cancelamento ou substituição de notas.
- Interface web / SaaS multiusuário.
- Suporte a certificados A3 (token físico) — roadmap.
- Serviço Windows headless (agendável) — roadmap v2.

---

## 3. Proposta Técnica e Estado Atual

### 3.1 Arquitetura Macro

```
┌─────────────────────────────────────────────────────────────────────┐
│                 Aplicação Desktop (WinForms / .NET 8)               │
│                                                                     │
│  ┌──────────────┐   ┌────────────────┐   ┌────────────────────┐    │
│  │  UI WinForms │──▶│  Orquestrador  │──▶│  Sincronizador     │    │
│  │  (v1.11.0)   │   │  de Carteira   │   │  (por CNPJ/NSU)    │    │
│  └──────────────┘   └────────────────┘   └─────────┬──────────┘    │
│                                                     │               │
│  ┌──────────────┐   ┌────────────────┐   ┌─────────▼──────────┐    │
│  │  Repositório │   │  Certificados  │   │  Cliente HTTP      │    │
│  │  de NSU      │◀──│  (.pfx / Store)│──▶│  mTLS (ADN)        │    │
│  │  (state)     │   └────────────────┘   └─────────┬──────────┘    │
│  └──────────────┘                                   │               │
│                                                     │               │
│  ┌──────────────┐   ┌────────────────┐              │               │
│  │  Escritor de │◀──│  Descompactação│◀── XMLs ─────┘               │
│  │  Árvore FS   │   │  GZip + Base64 │                               │
│  └──────────────┘   └────────────────┘                               │
│                                                                     │
│  ┌──────────────┐   ┌────────────────┐                               │
│  │  Análise de  │   │  Geração       │                               │
│  │  Lacunas     │   │  DANFSe / Excel│                               │
│  └──────────────┘   └────────────────┘                               │
└─────────────────────────────────────────────────────────────────────┘
                              │
                              ▼
                    ┌──────────────────┐
                    │  API NFS-e       │
                    │  Nacional (mTLS) │
                    │  adn.nfse.gov.br │
                    └──────────────────┘
```

### 3.2 Stack Tecnológico

| Camada | Tecnologia | Justificativa |
|---|---|---|
| Runtime | **.NET 8** | LTS, performance, criptografia nativa |
| UI | **WinForms** | Desktop Windows-first, produtividade alta, distribuição simples |
| HTTP | `HttpClient` + `SocketsHttpHandler` | Suporte nativo a mTLS com `ClientCertificate` |
| Certificados | `X509Certificate2` + pasta de `.pfx` | Leitura de múltiplos certificados por pasta + store Windows |
| Persistência de NSU | SQLite / arquivo de estado por CNPJ | Simples, transacional, portátil |
| Descompactação | `System.IO.Compression` (GZip) | Nativo, sem dependências externas |
| Logging | `Serilog` (ou equivalente) | Log estruturado + rotação |
| Configuração | `appsettings.json` + preferências locais | Paths de certificado, destino e opções de subpastas |

> **Por que C# / .NET 8 + WinForms?**
> - Manipulação de certificados A1 (`.pfx`, senha, thumbprint, validade) é **nativa e estável**.
> - mTLS com `HttpClient` funciona sem camadas adicionais.
> - Toolchain madura para instaladores e distribuição interna.
> - WinForms permite interface rica (grid de empresas, status, progresso) com baixo overhead.

### 3.3 Fluxo de Execução

```
INÍCIO
  │
  ├─▶ 1. Usuário seleciona pasta de certificados (.pfx)
  │       └─▶ Aplicativo varre a pasta e carrega empresas automaticamente
  │
  ├─▶ 2. Usuário informa senhas (quando necessário) e marca empresas
  │
  ├─▶ 3. Usuário define pasta destino dos XMLs
  │       └─▶ Opção de subpastas: Ano / Mês / Tipo
  │
  ├─▶ 4. Para cada empresa marcada:
  │       ├─▶ 4.1 Validar certificado (validade, senha)
  │       ├─▶ 4.2 Carregar último NSU persistido (default 0)
  │       ├─▶ 4.3 Criar HttpClient com mTLS (certificado da empresa)
  │       │
  │       ├─▶ 5. LOOP de paginação (até 50 documentos por chamada):
  │       │       ├─▶ 5.1 GET /contribuinte/DFe/{NSU}
  │       │       ├─▶ 5.2 Se lote vazio → fim da caixa
  │       │       ├─▶ 5.3 Descompactar XMLs (GZip + Base64)
  │       │       ├─▶ 5.4 Gravar na árvore de pastas (idempotente)
  │       │       ├─▶ 5.5 Atualizar NSU (após gravação bem-sucedida)
  │       │       └─▶ 5.6 Repetir até status "sem mais documentos"
  │       │
  │       └─▶ 6. Atualizar status na grid (sucesso / erro / lacunas)
  │
  ├─▶ 7. (Opcional) Análise de lacunas → Recuperação de lacunas
  │
  ├─▶ 8. (Opcional) Gerar DANFSe / Exportar Excel / Ver relatório
  │
FIM
```

### 3.4 Autenticação (mTLS)

```csharp
var handler = new SocketsHttpHandler();
handler.SslOptions.ClientCertificates = new X509CertificateCollection
{
    certificadoA1  // X509Certificate2 carregado do .pfx ou store
};

var client = new HttpClient(handler)
{
    BaseAddress = new Uri("https://adn.nfse.gov.br/")
};
```

**Requisitos:**
- Certificado A1 em formato `.pfx` com senha **ou** presente no Windows Certificate Store.
- Validação de **data de expiração** antes de iniciar o lote.
- Senha armazenada de forma segura (não em texto claro nos logs).
- Cada empresa usa seu próprio certificado — sem troca manual de sessão.

### 3.5 Estratégia de Sincronização por NSU

O **NSU (Número Sequencial Único)** funciona como cursor monotônico por CNPJ.

| Regra | Descrição |
|---|---|
| R1 | Cada CNPJ possui seu **próprio NSU**, independente dos demais. |
| R2 | NSU é persistido **após** a gravação bem-sucedida dos XMLs (atômico). |
| R3 | Se falha ocorrer entre download e persistência, o próximo ciclo **reprocessa** o lote (idempotência pela sobrescrita / hash). |
| R4 | Último NSU consultado quando não há novos documentos é **mantido** (não avança). |
| R5 | É possível forçar “Baixar desde o início” (reset de NSU). |
| R6 | Análise de lacunas identifica NSUs faltantes entre o primeiro e o último conhecido. |

**Formato do estado (exemplo):**

```json
{
  "cnpj": "12345678000199",
  "ultimoNsu": 4711,
  "ultimaSincronizacao": "2025-01-15T09:32:11Z",
  "versao": 1
}
```

### 3.6 Estrutura de Pastas (compatível com a equipe fiscal)

A árvore **é configurável** e mantém compatibilidade com o padrão já utilizado:

```
{PastaDestino}/
└── {CNPJ}/
    ├── emitidas/{AAAA}/{MM}/*.xml
    ├── recebidas/{AAAA}/{MM}/*.xml
    └── eventos/{AAAA}/{MM}/*.xml
```

- Escrita **idempotente**: arquivo já existente com mesmo conteúdo/hash é ignorado.
- Nome do arquivo preferencialmente derivado da **chave de acesso** da NFS-e.
- Opção de subpastas: `Ano / Mês / Tipo` (visível na UI).

### 3.7 Interface do Usuário (v1.11.0)

A interface principal (WinForms) contém:

| Área | Funcionalidade |
|---|---|
| **Barra superior** | Status de configuração (pasta de certificados, pasta destino, empresas marcadas) + botões “Abrir pasta destino” e “Configurações” |
| **Certificados** | Campo de pasta + “Escolher pasta” + “Reler pasta” |
| **Destino** | Campo de pasta de gravação + “Escolher pasta” + seletor de subpastas (Ano/Mês/Tipo) |
| **Grid de empresas** | Colunas: Empresa, CNPJ/CPF, Arquivo (.pfx), Senha, Último NSU, Validade, Situação, Detalhe |
| **Busca / Filtro** | Busca por empresa, CNPJ ou arquivo + filtro de validade |
| **Seleção** | Marcar todas / Desmarcar / Marcar só estas / Informar senha |
| **Conferência** | Analisar lacunas / Recuperar lacunas |
| **Documentos** | Gerar DANFSe / Exportar Excel / Ver relatório |
| **Ações principais** | Contadores (Empresas, XML gravados, Com erro) + botão **Baixar (XML)** + “Baixar desde o início” + “Parar” |
| **Status bar** | Mensagem de prontidão / progresso |

**Fluxo típico do usuário:**
1. Escolher pasta de certificados → empresas aparecem automaticamente.
2. Informar senhas dos `.pfx` (quando necessário).
3. Escolher pasta destino.
4. Marcar as empresas desejadas.
5. Clicar em **Baixar (XML)**.
6. (Opcional) Analisar/recuperar lacunas e gerar DANFSe ou Excel.

---

## 4. Requisitos

### 4.1 Funcionais

| ID | Requisito | Status |
|---|---|---|
| RF-01 | Percorrer a carteira inteira em uma execução | ✅ |
| RF-02 | Autenticar via certificado A1 por CNPJ (mTLS) | ✅ |
| RF-03 | Persistir NSU individual por CNPJ | ✅ |
| RF-04 | Retomar de onde parou após interrupção | ✅ |
| RF-05 | Descompactar e gravar XMLs na árvore fiscal | ✅ |
| RF-06 | Suportar emitidas, recebidas e eventos | ✅ |
| RF-07 | Carregar múltiplos `.pfx` a partir de uma pasta | ✅ |
| RF-08 | Permitir informar senha por certificado | ✅ |
| RF-09 | Analisar e recuperar lacunas de NSU | ✅ |
| RF-10 | Gerar DANFSe | ✅ |
| RF-11 | Exportar dados para Excel | ✅ |
| RF-12 | Emitir relatório da execução | ✅ |
| RF-13 | Permitir “Baixar desde o início” (reset) | ✅ |
| RF-14 | Parar execução em andamento | ✅ |
| RF-15 | Filtrar empresas por validade do certificado | ✅ |

### 4.2 Não-Funcionais

| ID | Requisito | Meta |
|---|---|---|
| RNF-01 | Throughput | ≥ 1.000 documentos/min por CNPJ (quando a API permitir) |
| RNF-02 | Tamanho de carteira | ≥ 100 empresas |
| RNF-03 | Idempotência | Repetir execução não duplica arquivos |
| RNF-04 | Resiliência | Retry com backoff exponencial (3 tentativas) para 429/5xx |
| RNF-05 | Segurança | Certificado/senha nunca em log; senha via mecanismo seguro |
| RNF-06 | Compatibilidade | Windows 10+, .NET 8 Runtime |
| RNF-07 | Usabilidade | Feedback visual de progresso, status por empresa e contadores |

---

## 5. Considerações de Segurança

- Senha do `.pfx` **não** é gravada em texto claro nos logs.
- Preferência por armazenamento seguro (DPAPI / Windows Credential Manager) quando persistida.
- Nenhuma chave privada é exportada ou gravada em disco em texto claro.
- Comunicação **exclusivamente mTLS** com a API nacional (`adn.nfse.gov.br`).
- Logs não contêm conteúdo completo de XML além do necessário para diagnóstico.
- Validação de validade do certificado antes de iniciar o lote.
- Assinatura do binário (Authenticode) recomendada para reduzir falsos positivos de antivírus.

---

## 6. Alternativas Consideradas

| Alternativa | Prós | Contras | Decisão |
|---|---|---|---|
| Extensão de navegador (anterior) | Sem instalação | 1 empresa/sessão, não escala | ❌ Descartada |
| Serviço Windows headless | Sem UI, agendável | Dificulta operação assistida e diagnóstico | 🔜 Roadmap v2 |
| Aplicação Web + backend | Multiusuário | Certificado no servidor (risco LGPD) | ❌ Descartada |
| **Desktop WinForms / .NET 8** | Certificado local, UI rica, fácil distribuição | Requer deploy em cada máquina | ✅ **Escolhida** |

---

## 7. Plano de Entrega (histórico)

| Fase | Escopo | Resultado |
|---|---|---|
| F1 | Protótipo: 1 CNPJ, download + gravação | ✅ |
| F2 | Carteira + NSU persistido + retomada | ✅ |
| F3 | Pasta de certificados + grid + senhas | ✅ |
| F4 | Análise de lacunas + recuperação | ✅ |
| F5 | DANFSe + Excel + relatório | ✅ |
| F6 | Polimento de UI e estabilidade (v1.11.0) | ✅ |

---

## 8. Riscos e Mitigações

| ID | Risco | Mitigação |
|---|---|---|
| R1 | Mudança de contrato da API nacional | Camada de abstração + versionamento de cliente |
| R2 | Certificado expirado durante o lote | Validação pré-execução + coluna de validade na grid |
| R3 | Antivírus bloqueia execução | Assinatura de código + whitelist interna |
| R4 | Corrupção do estado do NSU | Escrita atômica (temp + rename) + backup |
| R5 | Rate limit da API (HTTP 429) | Backoff exponencial + processamento serial por CNPJ + delay configurável |
| R6 | Senha incorreta de certificado | Feedback imediato na coluna “Situação” |

---

## 9. Métricas de Sucesso

- Tempo total para sincronizar ~100 CNPJs reduzido de **horas** para **minutos**.
- Zero intervenção manual de troca de certificado durante a execução.
- 100% dos XMLs gravados na estrutura de pastas já utilizada pela equipe fiscal.
- Redução de erros operacionais (reprocessamento, arquivos perdidos) superior a 90%.
- Capacidade de identificar e recuperar lacunas de NSU de forma assistida.

---

## 10. Questões em Aberto / Roadmap

| Item | Status | Observação |
|---|---|---|
| Formato de distribuição (MSI / ClickOnce / auto-update) | Em aberto | Preferência por instalador simples |
| Agendamento interno (timer / Windows Service) | Roadmap v2 | Hoje a execução é manual |
| Paralelismo controlado (2–4 CNPJs simultâneos) | Em avaliação | Respeitar rate limit da API |
| Suporte a certificado A3 (token) | Roadmap | Complexidade de PIN e sessão |
| Política de retenção de XMLs em falha parcial | Definida | Idempotência + reprocessamento seguro |

---

## 11. Referências

- API NFS-e Nacional — documentação oficial (ADN / SEFIN).
- Padrão DFe / NSU — Nota Técnica vigente.
- .NET 8 — `HttpClient`, `X509Certificate2`, `System.IO.Compression`.
- LGPD — tratamento de dados fiscais.
- Manual dos Contribuintes — APIs ADN.

---

## 12. APIs Públicas e Endpoints Detalhados

### 12.1 Ambientes

| Ambiente | Finalidade | Base URL principal |
|---|---|---|
| **Produção Restrita** | Homologação e testes | `https://adn.producaorestrita.nfse.gov.br/` |
| **Produção** | Consulta e emissão real | `https://adn.nfse.gov.br/` |

Swagger (contribuintes):  
`https://www.nfse.gov.br/swagger/contribuintesissqn/`

### 12.2 Endpoints Principais Utilizados

#### a) Distribuição de DFe por NSU

```
GET https://adn.nfse.gov.br/contribuinte/DFe/{NSU}
```

- Retorna até **50 documentos** a partir do NSU informado.
- Parâmetro opcional de consulta por CNPJ (validação de raiz com o certificado).
- Resposta contém `ultNSU`, `maxNSU` e lote com XMLs compactados (GZip + Base64).

#### b) Consulta de Eventos por Chave de Acesso

```
GET https://adn.nfse.gov.br/contribuinte/NFSe/{ChaveAcesso}/Eventos
```

#### c) Outros endpoints de referência (não foco do download em lote)

- Consulta de DPS: `GET/HEAD https://sefin.nfse.gov.br/sefinnacional/dps/{id}`
- Emissão de NFS-e (fora de escopo): `POST /nfse`
- Parâmetros municipais: `GET /parametros_municipais/{codigoMunicipio}/convenio`

### 12.3 Protocolo de Comunicação

| Item | Especificação |
|---|---|
| Protocolo | TLS com autenticação mútua (mTLS) |
| Formato de troca | JSON |
| Formato dos documentos | XML 1.0 |
| Codificação | UTF-8 |
| Certificado digital | ICP-Brasil, tipo A1 (ou A3 no futuro), com “Autenticação do Cliente” |
| Compactação | GZip com representação Base64 |

---

## 13. Exemplos de Requisição e Resposta

### 13.1 Consulta de DFe por NSU (cURL)

```bash
curl -X GET "https://adn.nfse.gov.br/contribuinte/DFe/4711" \
  --cert /path/to/certificado.pem \
  --key /path/to/private-key.pem \
  -H "Accept: application/json"
```

### 13.2 Resposta Típica (JSON)

```json
{
  "ultNSU": 4760,
  "maxNSU": 8920,
  "lote": [
    {
      "NSU": 4711,
      "chaveAcesso": "12345678901234567890123456789012345678901234567890",
      "tipoDocumento": "NFS-e",
      "dataHora": "2025-01-15T09:32:11-03:00",
      "XML": "H4sIAAAAAAAAA+1da3PbthL9..."
    }
  ]
}
```

---

## 14. Repositórios e Bibliotecas de Referência

### 14.1 .NET / C#

| Nome | Descrição |
|---|---|
| Gestor-NFS-e | Aplicativo desktop similar (múltiplas empresas, NSU, SQLite) |
| OpenAC.Net.NFSe / OpenAC.Net.NFSe.Nacional | Biblioteca multiplataforma e extensão nacional |
| NfseNacional (NuGet) | Emissão, consulta e cancelamento com certificado A1 |

### 14.2 Outras linguagens (referência)

- Python: `brans-nfe`, `baixar_nfse_portal_nacional`
- Node.js: `open-nfse`
- PHP: `sefin-sdk`

---

## 15. Bibliotecas .NET Recomendadas / Utilizadas

| Pacote | Finalidade |
|---|---|
| `System.Security.Cryptography.X509Certificates` | Certificados X.509 |
| `System.Net.Http` | HttpClient + mTLS |
| `System.IO.Compression` | GZip |
| `Microsoft.Data.Sqlite` | Persistência de NSU |
| `Serilog` | Logging estruturado |
| Bibliotecas de DANFSe (quando aplicável) | Geração de PDF auxiliar |

### 15.1 Exemplo: Carregamento de Certificado A1

```csharp
// A partir de arquivo .pfx
var cert = new X509Certificate2(
    "certificado.pfx",
    "senha",
    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);

// A partir do Windows Certificate Store
using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
store.Open(OpenFlags.ReadOnly);
var certs = store.Certificates.Find(
    X509FindType.FindByThumbprint, "THUMBPRINT", validOnly: true);
var certStore = certs[0];
```

---

## 16. Documentação Técnica Oficial

| Documento | URL |
|---|---|
| Manual dos Contribuintes — APIs ADN | https://www.gov.br/nfse/pt-br/biblioteca/documentacao-tecnica/documentacao-atual/manual-contribuintes-apis-adn-sistema-nacional-nfse.pdf |
| Hub de documentação técnica | https://www.gov.br/nfse/pt-br/biblioteca/documentacao-tecnica |
| Swagger Contribuintes (Produção) | https://www.nfse.gov.br/swagger/contribuintesissqn/ |
| Swagger Produção Restrita | https://adn.producaorestrita.nfse.gov.br/contribuintes/docs/index.html |

---

## 17. Boas Práticas de Implementação

### 17.1 Rate Limiting

- A API nacional não publica um rate limit oficial fixo.
- Recomenda-se delay de **200–500 ms** entre chamadas consecutivas.
- Implementar **retry com backoff exponencial** (3 tentativas) para HTTP 429 e 5xx.

### 17.2 Códigos de Erro Comuns

| HTTP | Significado | Ação |
|---|---|---|
| 400 | Requisição inválida | Verificar parâmetros / payload |
| 401 | Não autenticado | Verificar certificado e mTLS |
| 403 | Sem permissão | Verificar permissões do certificado |
| 404 | Não encontrado | Verificar NSU / chave |
| 429 | Rate limit | Backoff + delay |
| 5xx | Erro de servidor | Retry com backoff |

### 17.3 Idempotência e Atomicidade

- Gravar XML em arquivo temporário e renomear atomicamente após sucesso.
- Persistir NSU **somente após** gravação bem-sucedida.
- Usar hash ou chave de acesso no nome do arquivo para evitar duplicatas.

### 17.4 Segurança do Certificado

- Nunca logar senha ou chave privada.
- Validar data de expiração antes do lote.
- Preferir flags de armazenamento adequadas (`MachineKeySet` / `Exportable` quando necessário).

---

## 18. Roadmap e Evoluções Futuras

| Fase | Funcionalidade | Prioridade |
|---|---|---|
| v1.x | Polimento contínuo, estabilidade e feedback de usuários | Alta |
| v1.2+ | Agendamento interno / execução assistida | Alta |
| v1.3 | Paralelismo controlado (múltiplos CNPJs) | Média |
| v1.4 | Melhorias de relatório e conferência fiscal | Média |
| v2.0 | Serviço Windows headless (sem UI) | Alta |
| v2.x | Suporte a certificado A3 (token) | Média |
| Futuro | API REST local para integração com outros sistemas | Baixa |

### 18.1 Considerações sobre Paralelismo

- Cada CNPJ possui NSU independente → paralelismo é possível.
- Limitar a **2–4 CNPJs simultâneos** para respeitar rate limits.
- Usar `SemaphoreSlim` (ou equivalente) para controle de concorrência.
- Cada CNPJ deve ter seu próprio `HttpClient` com certificado distinto.

---

## 19. Glossário

| Termo | Definição |
|---|---|
| **ADN** | Ambiente de Dados Nacional — infraestrutura de compartilhamento de DF-e da NFS-e |
| **A1** | Certificado digital em arquivo (software), validade típica de 1 ano |
| **A3** | Certificado digital em token/cartão (hardware) |
| **CNPJ Raiz** | Os 8 primeiros dígitos do CNPJ (matriz + filiais) |
| **DANFSe** | Documento Auxiliar da NFS-e (representação gráfica) |
| **DF-e** | Documento Fiscal Eletrônico |
| **DPS** | Declaração de Prestação de Serviço |
| **mTLS** | Mutual TLS — autenticação mútua por certificado |
| **NFS-e** | Nota Fiscal de Serviço Eletrônica (padrão nacional) |
| **NSU** | Número Sequencial Único — cursor monotônico por CNPJ |
| **SEFIN** | Sistema Eletrônico de Fiscalização |

---

## 20. Aprovações

| Papel | Nome | Data | Assinatura |
|---|---|---|---|
| **Autor** | [Seu nome] | ___/___/______ | |
| **Revisor Técnico** | [Nome] | ___/___/______ | |
| **Revisor Fiscal / Contábil** | [Nome] | ___/___/______ | |
| **Aprovação Final** | [Nome] | ___/___/______ | |

---

> **Nota:** Este documento é um artefato vivo. Alterações na API nacional, notas técnicas ou requisitos de negócio devem ser refletidas em novas versões, com registro no histórico abaixo.

### Histórico de Revisões

| Versão | Data | Autor | Alterações |
|---|---|---|---|
| 0.1 | — | — | Versão inicial (proposta) |
| 0.2 | — | — | Adição de APIs, exemplos, repositórios e boas práticas |
| **1.0** | 2025-01 | — | Alinhamento ao produto real (WinForms v1.11.0): UI, análise de lacunas, DANFSe, Excel, fluxo de uso e status de requisitos |

---

## Anexo A — Valor para o Setor Contábil / Jurídico

O aplicativo elimina as principais fricções operacionais que escritórios e departamentos fiscais enfrentam no download em lote de NFS-e:

| Antes | Depois |
|---|---|
| Uma empresa por sessão de navegador | Toda a carteira em uma execução |
| Troca manual de certificado (fechar/abrir navegador) | Certificado correto por empresa, automático |
| Sem retomada | NSU persistido → continua de onde parou |
| Arquivos desorganizados | Mesma árvore de pastas já usada pela equipe |
| Sem visibilidade de falhas | Grid com status, erros e contadores |
| Lacunas de NSU não detectadas | Análise e recuperação assistida |
| Conferência manual trabalhosa | Exportação Excel + relatório + DANFSe |

O objetivo não foi apenas automatizar o download, mas **remover a limitação operacional que aparecia exatamente quando o volume de empresas aumentava**.
