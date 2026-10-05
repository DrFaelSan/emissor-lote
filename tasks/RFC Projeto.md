# RFC-001: Download em Massa de NFS-e (Notas Fiscais de Serviço)

| Campo | Valor |
|---|---|
| **Status** | Draft |
| **Autor** | [Seu nome] |
| **Data** | [Data] |
| **Revisores** | [Nomes] |
| **Versão** | 0.1 |

---

## 1. Resumo Executivo

Este documento propõe a especificação técnica de um aplicativo desktop, desenvolvido em **.NET 8**, capaz de realizar **download em massa de XMLs de NFS-e** (emitidas, recebidas e eventos) através da **API pública nacional**, autenticando-se por **certificado digital A1** de cada empresa da carteira.

O aplicativo substitui uma solução anterior baseada em extensão de navegador que operava uma empresa por vez e exigia reinicialização de sessão para trocar de certificado — limitação que inviabilizava o processo em carteiras com dezenas de empresas.

---

## 2. Contexto e Motivação

### 2.1 Cenário Atual (As-Is)

- O processo de captura de NFS-e era realizado por uma **extensão de navegador**.
- A extensão suportava **uma única empresa por sessão**.
- Trocar de empresa exigia **fechar e reabrir o navegador** para que um novo certificado A1 fosse carregado.
- Para carteiras com **dezenas de empresas**, o tempo operacional crescia linearmente com o número de CNPJs.

### 2.2 Problemas Identificados

| # | Problema | Impacto |
|---|---|---|
| P1 | Uma empresa por sessão de navegador | Não escala |
| P2 | Reinício manual do navegador entre empresas | Erro humano, tempo perdido |
| P3 | Sem controle de progresso/retomada | Reproc essamento e perda de XMLs |
| P4 | Armazenamento desorganizado | Retrabalho da equipe fiscal |
| P5 | Sem suporte a filiais | Consultas manuais por CNPJ |

### 2.3 Objetivos

- **O1** — Percorrer toda a carteira de empresas em uma única execução.
- **O2** — Utilizar o certificado A1 correto por empresa, sem intervenção manual.
- **O3** — Retomar sincronização por **NSU** individualizado por CNPJ.
- **O4** — Persistir XMLs na **mesma estrutura de pastas já usada pela equipe fiscal**.
- **O5** — Suportar navegação **matriz → filiais** a partir do CNPJ raiz.

### 2.4 Não-Objetivos (Out of Scope)

- Emissão de NFS-e.
- Cancelamento ou substituição de notas.
- Geração de relatórios fiscais ou contábeis.
- Interface web / SaaS multiusuário.
- Suporte a certificados A3 (token físico) na v1.

---

## 3. Proposta Técnica

### 3.1 Arquitetura Macro

```
┌─────────────────────────────────────────────────────────────┐
│                    Aplicação Desktop (.NET 8)               │
│                                                             │
│  ┌──────────────┐   ┌──────────────┐   ┌────────────────┐   │
│  │  UI / CLI    │──▶│  Orquestrador│──▶│  Sincronizador │   │
│  │  (WPF/CLI)   │   │  de Carteira │   │  (por CNPJ)    │   │
│  └──────────────┘   └──────────────┘   └───────┬────────┘   │
│                                                │            │
│  ┌──────────────┐   ┌──────────────┐   ┌───────▼────────┐   │
│  │  Repositório │   │  Certificados│   │  Cliente HTTP  │   │
│  │  de NSU      │◀──│  (A1 / Win)  │──▶│  mTLS NFS-e    │   │
│  │  (state)     │   └──────────────┘   └───────┬────────┘   │
│  └──────────────┘                              │            │
│                                                │            │
│  ┌──────────────┐                              │            │
│  │  Escritor de │◀───── XMLs descompactados ───┘            │
│  │  Árvore FS   │                                            │
│  └──────────────┘                                            │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
                    ┌──────────────────┐
                    │  API NFS-e       │
                    │  Nacional (mTLS) │
                    └──────────────────┘
```

### 3.2 Stack Tecnológico

| Camada | Tecnologia | Justificativa |
|---|---|---|
| Runtime | **.NET 8** | LTS, performance, bibliotecas nativas de criptografia |
| UI | WPF (ou CLI + log estruturado) | Desktop Windows-first |
| HTTP | `HttpClient` + `SocketsHttpHandler` | Suporte nativo a mTLS com `ClientCertificate` |
| Certificados | `X509Store` / `X509Certificate2` | Leitura do **Windows Certificate Store** e arquivos `.pfx` |
| Persistência de NSU | SQLite (ou JSON por CNPJ) | Simples, transacional, portátil |
| Descompactação | `System.IO.Compression` | Nativo, sem dependências |
| Logging | `Serilog` | Log estruturado + rotação |
| Configuração | `appsettings.json` + `Microsoft.Extensions.Configuration` | Padrão moderno .NET |

> **Por que C#/.NET?**
> - Manipulação de certificados A1 (import, export, `X509Store`) é **nativa e estável** em .NET.
> - Acesso ao **Registro do Windows** (`Microsoft.Win32.Registry`) é trivial para configurações corporativas e GPO.
> - mTLS com `HttpClient` é suportado sem camadas adicionais.
> - Toolchain madura para **instaladores MSI**, **click-once** ou distribuição interna.

### 3.3 Fluxo de Execução

```
INÍCIO
  │
  ├─▶ 1. Carregar carteira (lista de CNPJs matriz)
  │
  ├─▶ 2. Para cada CNPJ matriz:
  │       ├─▶ 2.1 Resolver certificado A1 (store ou .pfx)
  │       ├─▶ 2.2 Carregar NSU persistido (default 0)
  │       ├─▶ 2.3 Criar HttpClient com mTLS
  │       │
  │       ├─▶ 3. LOOP de paginação:
  │       │       ├─▶ 3.1 GET /DFe?NSU={nsu}&cnpj={cnpj}
  │       │       ├─▶ 3.2 Se vazio → fim da caixa, break
  │       │       ├─▶ 3.3 Descompactar XMLs
  │       │       ├─▶ 3.4 Gravar na árvore de pastas
  │       │       ├─▶ 3.5 Atualizar NSU (transacional)
  │       │       └─▶ 3.6 Repetir até status "sem mais documentos"
  │       │
  │       └─▶ 4. Expandir para filiais (se configurado)
  │               └─▶ Repetir passos 2.1–3 para cada filial
  │
FIM
```

### 3.4 Autenticação (mTLS)

```csharp
var handler = new SocketsHttpHandler();
handler.SslOptions.ClientCertificates = new X509CertificateCollection
{
    certificadoA1  // X509Certificate2 carregado do store ou .pfx
};

var client = new HttpClient(handler)
{
    BaseAddress = new Uri("https://api.nfse.gov.br/...")
};
```

**Requisitos:**
- Certificado A1 em formato `.pfx` com senha **ou** presente no `X509Store(StoreName.My, StoreLocation.CurrentUser/LocalMachine)`.
- Validação de **data de expiração** antes de iniciar o lote.
- Seleção por **thumbprint** vinculada ao CNPJ no arquivo de configuração.

### 3.5 Estratégia de Sincronização por NSU

O **NSU (Número Sequencial Único)** funciona como cursor monotônico por CNPJ. Regras:

| Regra | Descrição |
|---|---|
| R1 | Cada CNPJ possui seu **próprio NSU**, independente dos demais. |
| R2 | NSU é persistido **após** a gravação bem-sucedida dos XMLs (atômico). |
| R3 | Se falha ocorrer entre download e persistência, o próximo ciclo **reprocessa** o lote (idempotência garantida pela sobrescrita de arquivo). |
| R4 | Último NSU consultado quando não há novos documentos é **mantido** (não avança). |
| R5 | Reset manual deve ser possível via UI para reprocessamento completo. |

**Formato do estado (exemplo JSON):**

```json
{
  "cnpj": "12345678000199",
  "ultimoNsu": 4711,
  "ultimaSincronizacao": "2025-01-15T09:32:11Z",
  "versao": 1
}
```

### 3.6 Estrutura de Pastas (compatível com a equipe fiscal)

A árvore **deve ser idêntica à já utilizada** para que lotes antigos e novos coexistam. Exemplo:

```
/notas-fiscais/{CNPJ}/
   ├── emitidas/{AAAA}/{MM}/*.xml
   ├── recebidas/{AAAA}/{MM}/*.xml
   └── eventos/{AAAA}/{MM}/*.xml
```

- Escrita **idempotente**: arquivo já existente com mesmo hash é ignorado.
- Nome do arquivo derivado da **chave de acesso** da NFS-e.

### 3.7 Suporte a Matriz e Filiais

- Entrada: **CNPJ da matriz** (raiz de 8 dígitos).
- O aplicativo consulta a API e **descobre filiais** vinculadas.
- Comportamento configurável:
  - **(a)** Navegar e consultar todas as filiais automaticamente, **ou**
  - **(b)** Permitir **adicionar filial específica** por CNPJ completo na carteira.
- Cada filial mantém **NSU, certificado e pasta próprios**.

```json
{
  "matriz": "12345678",
  "filiais": [
    { "cnpj": "12345678000199", "certThumbprint": "ABC..." },
    { "cnpj": "12345678000280", "certThumbprint": "DEF..." }
  ]
}
```

### 3.8 Configuração e Registro do Windows

- Configurações corporativas (paths, URLs, política de retry) lidas de:
  1. `appsettings.json` (padrão)
  2. **Registro do Windows** — `HKEY_LOCAL_MACHINE\SOFTWARE\{Empresa}\NfseDownloader` (override)
  3. Variáveis de ambiente
- Precedência: **env > registry > json**.
- Permite deploy via **GPO** sem reinstalar o aplicativo.

---

## 4. Requisitos

### 4.1 Funcionais

| ID | Requisito |
|---|---|
| RF-01 | Percorrer a carteira inteira em uma execução. |
| RF-02 | Autenticar via certificado A1 por CNPJ (mTLS). |
| RF-03 | Persistir NSU individual por CNPJ. |
| RF-04 | Retomar de onde parou após interrupção. |
| RF-05 | Descompactar e gravar XMLs na árvore fiscal existente. |
| RF-06 | Suportar emitidas, recebidas e eventos. |
| RF-07 | Expandir consultas a filiais a partir do CNPJ matriz. |
| RF-08 | Permitir cadastro manual de filial. |
| RF-09 | Emitir log estruturado por CNPJ/NSU. |

### 4.2 Não-Funcionais

| ID | Requisito | Meta |
|---|---|---|
| RNF-01 | Throughput | ≥ 1.000 documentos/min por CNPJ |
| RNF-02 | Tamanho de carteira | ≥ 100 empresas |
| RNF-03 | Idempotência | Repetir execução não duplica arquivos |
| RNF-04 | Resiliência | Retry com backoff exponencial (3 tentativas) |
| RNF-05 | Segurança | Certificado nunca em log; senha via DPAPI/Windows Credential Manager |
| RNF-06 | Compatibilidade | Windows 10+, .NET 8 Runtime |

---

## 5. Considerações de Segurança

- Senha do `.pfx` armazenada via **DPAPI** (`ProtectedData`) ou **Windows Credential Manager**.
- Nenhuma chave privada é exportada ou gravada em disco em texto claro.
- Comunicação **exclusivamente mTLS** com a API nacional.
- Logs **não** contêm senha, chave privada ou conteúdo de XML além do necessário.
- Assinatura do binário (Authenticode) recomendada para evitar AV falso-positivo.

---

## 6. Alternativas Consideradas

| Alternativa | Prós | Contras | Decisão |
|---|---|---|---|
| Extensão de navegador (atual) | Sem instalação | 1 empresa/sessão, não escala | ❌ Descartada |
| Serviço Windows headless | Sem UI, agendável | Dificulta operação assistida | 🔜 v2 |
| Aplicação Web + backend | Multiusuário | Certificado no servidor (LGPD/risco) | ❌ Descartada |
| **Desktop .NET 8** | Manipulação de certificado local, Registry, MSI | Requer deploy em cada máquina | ✅ **Escolhida** |

---

## 7. Plano de Entrega (Sugestão)

| Fase | Escopo | Duração |
|---|---|---|
| F1 | Protótipo: 1 CNPJ, download + gravação | 1 semana |
| F2 | Carteira + NSU persistido + retomada | 2 semanas |
| F3 | Filiais + Registry + logging | 1 semana |
| F4 | Instalador MSI + testes de carga | 1 semana |
| F5 | Homologação com equipe fiscal | 1 semana |

---

## 8. Riscos

| ID | Risco | Mitigação |
|---|---|---|
| R1 | Mudança de contrato da API nacional | Camada de abstração + versionamento |
| R2 | Certificado expirado durante o lote | Validação pré-execução + alerta |
| R3 | Antivírus bloqueia execução | Assinatura de código + whitelist |
| R4 | Corrupção do estado do NSU | Backup do arquivo + escrita atômica (temp + rename) |
| R5 | Rate limit da API | Backoff + fila serial por CNPJ |

---

## 9. Métricas de Sucesso

- Tempo total para sincronizar 100 CNPJs < 30 min (baseline: horas no modelo atual).
- Zero intervenção manual durante a execução.
- 100% dos XMLs em pastas idênticas ao padrão da equipe fiscal.
- Redução de erros operacionais > 90%.

---

## 10. Questões em Aberto

1. Formato de distribuição: **MSI**, **ClickOnce** ou **auto-update**?
2. Deve haver **agendamento interno** (timer) ou apenas execução manual?
3. Limite de paralelismo por CNPJ — quantos `HttpClient` simultâneos são seguros?
4. Política de retenção dos XMLs em caso de falha parcial?
5. Suporte futuro a **A3 (token)** — roadmap?

---

## 11. Referências

- API NFS-e Nacional — documentação oficial.
- Padrão DFe / NSU — Nota Técnica vigente.
- .NET 8 — `HttpClient`, `X509Certificate2`, `ProtectedData`.
- LGPD — tratamento de dados fiscais.

## 12. APIs Públicas e Endpoints Detalhados

### 12.1 Visão Geral dos Ambientes

O Sistema Nacional NFS-e disponibiliza **dois ambientes** com infraestrutura e endpoints distintos:

| Ambiente | Finalidade | Portal do Emissor | Swagger / Documentação |
|---|---|---|---|
| **Produção Restrita** | Homologação e testes | `https://www.producaorestrita.nfse.gov.br/EmissorNacional/Login` | `https://www.producaorestrita.nfse.gov.br/swagger/contribuintesissqn/` |
| **Produção** | Emissão e consulta real | `https://www.nfse.gov.br/EmissorNacional/` | `https://www.nfse.gov.br/swagger/contribuintesissqn/` |

O **Swagger UI oficial** para contribuintes está disponível em `https://www.nfse.gov.br/swagger/contribuintesissqn/`, com especificação OpenAPI 3.0 acessível em `https://www.nfse.gov.br/Swagger/contribuintesissqn/docs/v1/swagger.json`.

### 12.2 Endpoints Principais (ADN — Ambiente de Dados Nacional)

#### a) Distribuição de DFe por NSU

```
GET https://adn.nfse.gov.br/contribuinte/DFe/{NSU}
```

**Descrição:** Retorna os documentos fiscais de serviço correspondentes ao NSU informado. O solicitante informa um NSU e o sistema nacional retorna o DF-e associado.

**Parâmetros:**
| Parâmetro | Local | Tipo | Obrigatório | Descrição |
|---|---|---|---|---|
| `NSU` | Path | integer | Sim | Número Sequencial Único |
| `cnpjConsulta` | Query | string | Não | CNPJ de consulta (validação de CNPJ Raiz com o certificado) |

**Retorno:** Até **50 documentos** por chamada a partir do NSU informado.

#### b) Consulta de Eventos por Chave de Acesso

```
GET https://adn.nfse.gov.br/contribuinte/NFSe/{ChaveAcesso}/Eventos
```

**Descrição:** Retorna os documentos fiscais do tipo Evento vinculados à chave de acesso informada.

#### c) Download de Evento Específico (via NSTecnologia)

```
POST https://api.nstecnologia.com.br/... (endpoint do parceiro)
```

**Campos de entrada:**
| Campo | Descrição | Obrigatório |
|---|---|---|
| `X-AUTH-TOKEN` | Token de acesso do usuário | Sim |
| `chNFSe` | Chave de acesso da NFS-e (50 dígitos) | Sim |
| `tpDown` | Tipo: `X` (XML), `P` (PDF), `XP` (ambos) | Sim |
| `tpEvento` | Tipo de evento (ex: `canc`) | Sim |
| `tpAmb` | Ambiente: `1` (Produção) / `2` (Homologação) | Sim |



#### d) Consulta de DPS por Identificador

```
GET https://sefin.nfse.gov.br/sefinnacional/dps/{id}
HEAD https://sefin.nfse.gov.br/sefinnacional/dps/{id}
```

**Descrição:** Retorna a chave de acesso da NFS-e a partir do identificador do DPS. O `HEAD` verifica se uma NFS-e foi emitida a partir do Id do DPS.

#### e) Emissão de NFS-e (DPS)

```
POST https://adn.producaorestrita.nfse.gov.br/nfse  (homologação)
POST https://adn.nfse.gov.br/nfse                    (produção)
```

**Descrição:** Envia o XML da DPS para gerar a NFS-e.

#### f) Parâmetros Municipais

```
GET https://adn.producaorestrita.nfse.gov.br/parametros_municipais/{codigoMunicipio}/convenio
```

**Descrição:** Obtém as parametrizações do município antes de emitir a nota.

### 12.3 Protocolo de Comunicação

| Item | Especificação |
|---|---|
| **Protocolo** | TLS 1.0, 1.1 e 1.2 com autenticação mútua (mTLS) |
| **Formato de troca** | JSON |
| **Formato dos documentos** | XML 1.0 |
| **Codificação** | UTF-8 |
| **Certificado digital** | ICP-Brasil, tipo A1 ou A3, CNPJ ou CPF, com "Autenticação do Cliente" |
| **Padrão de assinatura XML** | XMLDSIG |
| **Compactação** | GZip com representação base64binary |



---

## 13. Exemplos de Requisições e Respostas

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

### 13.3 Exemplo de Requisição de Download de Evento

```json
{
  "chNFSe": "00000000000000000000000000000000000000000000000000",
  "tpDown": "X",
  "tpEvento": "canc",
  "tpAmb": "2"
}
```



---

## 14. Repositórios de Implementações Similares

### 14.1 .NET / C#

| Repositório | Descrição | Stack | Link |
|---|---|---|---|
| **Gestor-NFS-e** | Aplicativo desktop Windows para consultar, sincronizar, organizar e baixar XML, PDF DANFSe e XLSX da NFS-e Nacional. Suporta múltiplas empresas, certificado A1 por `.pfx` ou store do Windows, NSU persistido, SQLite local. | C# / .NET | `https://github.com/joaovmgs/Gestor-NFS-e` |
| **OpenAC.Net.NFSe** | Biblioteca .NET multiplataforma para geração, assinatura, transmissão, consulta e impressão de NFS-e para centenas de municípios brasileiros. | C# / .NET | `https://github.com/OpenAC-Net/OpenAC.Net.NFSe` |
| **OpenAC.Net.NFSe.Nacional** | Extensão específica para o padrão nacional da NFS-e, com integração via DI, logging e serviços ASP.NET Core. | C# / .NET | NuGet: `OpenAC.Net.NFSe.Nacional` |
| **NfseNacional (NuGet)** | Biblioteca .NET para emissão, consulta e cancelamento com certificado A1. Suporta net8.0, net9.0, netstandard2.1. | C# / .NET | NuGet: `NfseNacional` |

### 14.2 Python

| Repositório | Descrição | Link |
|---|---|---|
| **xml-nfse-download** | Download automático de NFS-e RECEBIDAS diretamente no portal da NFSe usando Selenium. | `https://github.com/devcaiada/xml-nfse-download` |
| **baixar_nfse_portal_nacional** | Ferramenta automatizada para baixar NFS-e do Portal Nacional, com salvamento de XML/PDF, certificado PFX/PEM, GUI tkinter e release em EXE. | `https://github.com/renan20553/baixar_nfse_portal_nacional` |
| **brans-nfe** | Cliente Python para a NFS-e Nacional do Brasil (SEFIN) — assinatura, transmissão, consulta, cancelamento, DFe e DANFSe. | `https://github.com/badbrans/brans-nfe` |

### 14.3 Node.js / TypeScript

| Repositório | Descrição | Link |
|---|---|---|
| **open-nfse** | Cliente TypeScript/Node.js para o Padrão Nacional de NFS-e (nfse.gov.br). API unificada da Receita Federal. | `https://github.com/Fm-s/open-nfse` |

### 14.4 PHP

| Repositório | Descrição | Link |
|---|---|---|
| **sefin-sdk** | SDK PHP para integração com a API NFS-e da SEFIN Nacional, seguindo contrato `swagger.json`. Suporte a mTLS, DTOs tipados, GZip+base64. | `https://github.com/ItargetLabs/sefin-sdk` |
| **nfse-porto-alegre-rs** | Biblioteca PHP para integração completa com a API REST de NFS-e do município de Porto Alegre (padrão nacional ADN). | `https://github.com/ilab4/nfse-porto-alegre-rs` |

### 14.5 Aplicações Desktop (Electron)

| Repositório | Descrição | Link |
|---|---|---|
| **Nfs-e-Monitor** | Aplicação desktop Windows (Electron + SQLite) que centraliza gestão de NFS-e do portal nacional. Sincroniza automaticamente, exporta relatórios e organiza notas de múltiplas empresas. | `https://github.com/matheuscardosos/Nfs-e-Monitor` |

---

## 15. Bibliotecas e Pacotes .NET Recomendados

| Pacote | Finalidade | Fonte |
|---|---|---|
| `System.Security.Cryptography.X509Certificates` | Manipulação de certificados X.509 | Nativo .NET |
| `Microsoft.Win32.Registry` | Acesso ao Registro do Windows | Nativo .NET |
| `System.IO.Compression` | GZip para compactação/descompactação | Nativo .NET |
| `System.Net.Http` | HttpClient com suporte a mTLS | Nativo .NET |
| `Serilog` | Logging estruturado com rotação | NuGet |
| `Microsoft.Data.Sqlite` | Persistência do NSU em SQLite | NuGet |
| `NfseNacional` | Integração direta com ADN/SEFIN | NuGet |
| `OpenAC.Net.NFSe.Nacional` | Emissão e transmissão NFSe | NuGet |
| `DANFSe` | Geração de DANFSe em PDF | NuGet |
| `DFeSignature` | Assinatura digital XML (XMLDSIG) | OpenAC.Net.NFSe |

### 15.1 Exemplo de Código: Carregamento de Certificado A1

```csharp
using System.Security.Cryptography.X509Certificates;

// Opção 1: Do arquivo .pfx
var cert = new X509Certificate2("certificado.pfx", "senha",
    X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);

// Opção 2: Do Windows Certificate Store
using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
store.Open(OpenFlags.ReadOnly);
var certs = store.Certificates.Find(
    X509FindType.FindByThumbprint, "THUMBPRINT_AQUI", validOnly: true);
var certStore = certs[0];
```



---

## 16. Documentação Técnica Oficial (Links Diretos)

| Documento | URL |
|---|---|
| **Manual dos Contribuintes — APIs ADN** | `https://www.gov.br/nfse/pt-br/biblioteca/documentacao-tecnica/documentacao-atual/manual-contribuintes-apis-adn-sistema-nacional-nfse.pdf` |
| **Documentação Técnica (hub)** | `https://www.gov.br/nfse/pt-br/biblioteca/documentacao-tecnica` |
| **Swagger Contribuintes (Produção)** | `https://www.nfse.gov.br/swagger/contribuintesissqn/` |
| **Swagger Contribuintes (Produção Restrita)** | `https://adn.producaorestrita.nfse.gov.br/contribuintes/docs/index.html` |
| **Nota Técnica DANFSe nº 008/2026** | `https://www.gov.br/nfse/pt-br/biblioteca/documentacao-tecnica/rtc/nt-008-se-cgnfse-danfse-20260505.pdf` |
| **Guia APIs Municípios (NFS-e Via)** | `https://www.gov.br/nfse/pt-br/nfs-e-via/documentacao-tecnica/anexo-v-guia-para-utilizacao-das-apis_municipios_v1-0.pdf` |

---

## 17. Boas Práticas de Implementação

### 17.1 Rate Limiting e Intervalo entre Requisições

- O portal nacional **não publica um rate limit oficial fixo**, mas a comunidade reporta que o limite para consultas de eventos é de aproximadamente **360 chamadas/minuto**.
- Recomenda-se implementar um **delay de 200–500ms** entre downloads consecutivos para evitar HTTP 429 (Too Many Requests).
- Implementar **retry com backoff exponencial** (3 tentativas) para erros 429 e 5xx.

### 17.2 Códigos de Erro Comuns

| HTTP | Significado | Ação |
|---|---|---|
| `400` | Requisição inválida (payload ou regra de negócio) | Verificar XML/DPS, corrigir payload |
| `401` | Não autenticado | Verificar certificado A1 e mTLS |
| `403` | Sem permissão (ex: `NFSeDist` ausente) | Verificar permissões do certificado |
| `404` | Não encontrado | Verificar `companyId` / chave de acesso |
| `409` | Conflito | Verificar estado do documento |
| `429` | Rate limit excedido | Backoff exponencial + delay |

### 17.3 Códigos de Erro de Negócio (Exemplos)

| Código | Descrição |
|---|---|
| `E0037` | Município já conveniado — erro de emissão |
| `E0116` | IM obrigatória para o prestador |
| `00381` | Valor do desconto condicionado maior que o valor do serviço |

### 17.4 Idempotência e Atomicidade

- Gravar XML em **arquivo temporário** e renomear atomicamente após sucesso.
- Persistir NSU **somente após** gravação bem-sucedida dos XMLs.
- Utilizar **hash SHA-256** da chave de acesso como nome do arquivo para evitar duplicatas.

### 17.5 Segurança do Certificado

- Senha do `.pfx` armazenada via **DPAPI** (`ProtectedData.Protect`) ou **Windows Credential Manager**.
- Nunca logar senha ou chave privada.
- Validar data de expiração antes de iniciar o lote.
- Preferir `X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable` para evitar problemas de permissão.

---

## 18. Roadmap e Evoluções Futuras

| Fase | Funcionalidade | Prioridade | Estimativa |
|---|---|---|---|
| **v1.1** | Suporte a certificados A3 (token físico) | Média | 2 semanas |
| **v1.2** | Agendamento interno (timer / Windows Service) | Alta | 1 semana |
| **v1.3** | Paralelismo controlado (múltiplas empresas simultâneas) | Média | 2 semanas |
| **v1.4** | Geração automática de DANFSe em PDF | Baixa | 2 semanas |
| **v1.5** | Exportação para XLSX / CSV para conferência fiscal | Média | 1 semana |
| **v2.0** | Serviço Windows headless (sem UI) para deploy em servidor | Alta | 4 semanas |
| **v2.1** | API REST local para integração com outros sistemas | Média | 3 semanas |
| **v2.2** | Dashboard web de monitoramento (opcional) | Baixa | 4 semanas |

### 18.1 Considerações sobre Paralelismo

- Cada CNPJ possui **NSU independente**, permitindo processamento paralelo.
- **Limitar a 2–4 CNPJs simultâneos** para respeitar rate limits da API.
- Utilizar `SemaphoreSlim` para controle de concorrência.
- Cada CNPJ deve ter seu próprio `HttpClient` com certificado distinto.

---

## 19. Glossário

| Termo | Definição |
|---|---|
| **ADN** | Ambiente de Dados Nacional — infraestrutura de compartilhamento de DF-e da NFS-e |
| **A1** | Certificado digital armazenado em arquivo (software), com validade de 1 ano |
| **A3** | Certificado digital armazenado em token/cartão (hardware) |
| **CNPJ Raiz** | Os 8 primeiros dígitos do CNPJ, identificando a matriz e suas filiais |
| **DANFSe** | Documento Auxiliar da NFS-e — representação gráfica simplificada |
| **DF-e** | Documento Fiscal Eletrônico (termo guarda-chuva para NF-e, CT-e, NFS-e) |
| **DPS** | Declaração de Prestação de Serviço — documento que gera a NFS-e |
| **mTLS** | Mutual TLS — autenticação mútua por certificado digital |
| **NFS-e** | Nota Fiscal de Serviço Eletrônica — modelo 56 no padrão nacional |
| **NSU** | Número Sequencial Único — cursor monotônico por CNPJ atribuído pelo ADN |
| **SEFIN** | Sistema Eletrônico de Fiscalização — servidor de emissão da NFS-e nacional |
| **XMLDSIG** | Padrão de assinatura digital XML (W3C) |



---

## 20. Aprovações

| Papel | Nome | Data | Assinatura |
|---|---|---|---|
| **Autor** | [Seu nome] | ___/___/______ | |
| **Revisor Técnico** | [Nome] | ___/___/______ | |
| **Revisor Fiscal** | [Nome] | ___/___/______ | |
| **Aprovação Final** | [Nome] | ___/___/______ | |

---

> **Nota:** Este documento é um artefato vivo. Alterações na API nacional, notas técnicas ou requisitos de negócio devem ser refletidas em novas versões desta RFC, com registro no histórico de revisões abaixo.

### Histórico de Revisões

| Versão | Data | Autor | Alterações |
|---|---|---|---|
| 0.1 | [Data] | [Nome] | Versão inicial (seções 1–11) |
| 0.2 | [Data] | [Nome] | Adição das seções 12–20: APIs, exemplos, repositórios, boas práticas, roadmap e glossário |