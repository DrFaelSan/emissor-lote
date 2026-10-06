# RFC-003: Arquitetura Unificada NEO-e — Resolução de Gargalos Fiscais, Motor Multidocumento, Pré-Auditoria e Emissão em Lote

| Campo | Valor |
| --- | --- |
| **Projeto** | NEO-e (Plataforma Desktop de Automação Fiscal)

 |
| **Status** | Proporsal / Draft |
| **Autor** | Arquitetura de Software / Time de Engenharia Fiscal |
| **Target Runtime** | .NET 8.0 (LTS) / WPF C# (Windows-First)

 |
| **Relacionada a** | RFC-001 (Sincronização e Download ADN) e RFC-002 (Manifestação e Cruzamento)

 |

---

## 1. Resumo Executivo

A consolidação de experiências do mercado fiscal revela que a operação diária de escritórios contábeis e departamentos fiscais é severamente limitada por processos manuais, navegação por extensões de browser com restrição de certificado por sessão, falta de conferência de retenções e lentidão na emissão individual de notas fiscais.

A presente **RFC-003** especifica a evolução da plataforma **NEO-e**, construída em **.NET 8** e **WPF C#** sob princípios de *Clean Architecture*. O objetivo é transformar o aplicativo desktop em um motor corporativo completo de governança fiscal, contemplando:

1. **Sincronização Massiva via mTLS e NSU** sem depender de navegadores ou RPAs sensíveis a *captchas*.


2. **Motor de Emissão Nacional em Lote** com pré-validações de alíquotas/retenções e assinatura XMLDSig em memória.


3. **Painel de Pré-Auditoria Tributária e Manifestação do Destinatário** (NFS-e / NF-e) com recálculo automático de impostos e cruzamento de valores (*matching*).


4. **Conectividade Extensível (ERP / iPaaS)** para injeção automática de documentos escriturados.



---

## 2. Mapeamento Sistemático de Gargalos vs. Soluções NEO-e

| # | Gargalo Identificado | Causa Raiz no Mercado | Solução Arquitetural no NEO-e |
| --- | --- | --- | --- |
| **G1** | **Troca manual de certificados A1 no browser** | Sessões de navegação retêm apenas um certificado por contexto, exigindo reabrir o browser por CNPJ.

 | Instanciação dinâmica de `SocketsHttpHandler` associado a `X509Certificate2` em memória para cada requisição da carteira.

 |
| **G2** | **Lentidão e erros em emissões unitárias** | Entre 30 e 40 cliques por nota em portais web; digitação manual de retenções (PIS/COFINS/IRRF/ISS).

 | **Motor de Emissão Assíncrono** com validação de 12 regras fiscais em memória e transmissão direta via API SEFIN.

 |
| **G3** | **Reprocessamento e duplicação de XMLs** | Falta de controle de cursor sequencial nos downloads manuais ou via scripts simples.

 | Controle de cursor monotônico **NSU por CNPJ** persistido em SQLite com gravação atômica em disco.

 |
| **G4** | **Inconsistências em retenções e notas canceladas** | Divergência entre destacado na nota e a legislação vigente; falta de visibilidade sobre notas substituídas.

 | **Engine de Pré-Auditoria TaxTech** que analisa a árvore DOM do XML, calcula retenções "on-the-fly" e sinaliza notas canceladas/substituídas.

 |
| **G5** | **Gargalo no lançamento dentro do ERP** | Download do XML resolve apenas o início do fluxo; o trabalho braçal desloca-se para a digitação no ERP.

 | Arquitetura baseada em eventos locais que expõe contratos JSON/CSV limpos para consumo direto por ERPs ou pipelines n8n.

 |

---

## 3. Especificação da Arquitetura do Sistema NEO-e (.NET 8 + Clean Architecture)

O sistema deve manter rigorosa separação de responsabilidades em 5 projetos principais:

```text
src/
 ├── NEO-e.Domain/           --> Entidades fiscais, Value Objects (CNPJ, NSU, ChaveAcesso), Regras de Validação[cite: 4, 11]
 ├── NEO-e.Application/      --> Casos de Uso (SyncWallet, MassEmit, PreAudit, Manifestation)[cite: 5, 11]
 ├── NEO-e.Contracts/        --> DTOs externos, Schemas de Request/Response da ADN e SEFIN[cite: 4, 11]
 ├── NEO-e.Infrastructure/   --> SocketsHttpHandler mTLS, SQLiteContext, X509Store, XMLDSig, Serilog[cite: 4, 5, 11]
 └── NEO-e.App/              --> Shell WPF (MVVM), ViewModels, Controls, Directives, Generic Host (IHost)[cite: 5, 10, 11]

```

### 3.1 Infraestrutura mTLS e Gerenciamento de Certificados (`NEO-e.Infrastructure`)

Para resolver o **Gargalo G1**, a infraestrutura de rede abandona qualquer dependência de `WebBrowser` ou `Selenium/Puppeteer`.

* **Pooling de Handlers HTTP**: Cada CNPJ processado recebe um `HttpClient` isolado com um `SocketsHttpHandler` configurado com seu respetivo `X509Certificate2`.


* **Segurança de Chaves**: A navegação no repositório do Windows (`X509Store`) ou arquivos `.pfx` não exporta a chave privada para disco. As senhas de arquivos `.pfx` são criptografadas localmente via Windows **DPAPI** (`ProtectedData`).



```csharp
// Exemplo conceitual de construção do handler mTLS isolado por empresa em NEO-e.Infrastructure
public HttpClient CreateMtlsClient(X509Certificate2 certificate, string baseUrl)
{
    var handler = new SocketsHttpHandler
    {
        SslOptions = new SslClientAuthenticationOptions
        {
            ClientCertificates = new X509Certificate2Collection(certificate),
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
        },
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        EnableMultipleHttp2Connections = true
    };

    var client = new HttpClient(handler)
    {
        BaseAddress = new Uri(baseUrl),
        Timeout = TimeSpan.FromSeconds(60)
    };

    return client;
}

```

---

## 4. Detalhamento dos Módulos Técnicos

### 4.1 Módulo 1: Motor de Sincronização Incremental por NSU (Evolução RFC-001)

A sincronização de documentos fiscais eletrônicos é guiada por **Cursores Monotônicos (NSU)** persistidos na base local `SQLite` por empresa.

* **Algoritmo de Paginação Incremental**:
1. Carrega a lista de empresas ativas e recupera o `ultNSU` gravado no SQLite.


2. Executa a chamada REST ao endpoint do Ambiente de Dados Nacional (ADN) enviando o cursor `ultNSU`.


3. Recebe o pacote de documentos compactado (`GZip` + `Base64`).


4. Descompacta o payload em memória usando `System.IO.Compression`.


5. Salva os arquivos XMLs atomicamente na árvore fiscal configurada (ex: `Destino/CNPJ/Ano/Mes/Tipo/`).


6. **Apenas após** a confirmação do I/O de todos os arquivos do lote, o `ultNSU` é atualizado transacionalmente no SQLite.





```
[Início Sync] ──> (Ler ultNSU no SQLite) ──> (GET ADN mTLS via SocketsHttpHandler)
                                                      │
                                                      ▼
[Atualizar ultNSU no SQLite] <── (Gravou XMLs) <── (Descompacta GZip em memória)

```

### 4.2 Módulo 2: Motor de Emissão de NFS-e em Lote (*Nota Rápida Engine*)

Para atender à necessidade de emissão acelerada, o NEO-e inclui uma engine de validação e transmissão de DPS (Declaração de Prestação de Serviço) para o portal nacional SEFIN.

1. **Pré-Validação Fiscal em Memória**: Antes de assinar ou emitir chamadas de rede, cada rascunho de nota passa por 12 validações de domínio no `NEO-e.Domain` (Alíquota ISS vigente no município, preenchimento do tomador, códigos IBGE, cálculo de PIS/COFINS/IRRF/CSLL e regras de retenção na fonte).


2. **Conferência de Numeração Preventiva**: Consulta rápida à SEFIN para assegurar que a numeração da DPS não sofrerá duplicidade.


3. **Assinatura Digital XMLDSig**: Assinatura em memória do lote utilizando a chave privada contida no repositório do Windows, sem expor chaves.


4. **Envio e Distribuição**: Transmissão do lote via API oficial, gravação do XML autorizado e envio assíncrono dos e-mails aos tomadores com XML e DANFSe anexados.



### 4.3 Módulo 3: Painel de Pré-Auditoria Tributária e Manifestação (Evolução RFC-002)

Para suprir os **Gargalos G4 e G5**, o módulo de entrada de documentos não limita sua atuação ao mero download.

* **Recálculo de Impostos On-The-Fly**: Leitura em memória do XML parseado (`XDocument`/`XmlReader`). O sistema extrai os valores brutos do serviço e reaplica as alíquotas legais de retenção (ex: 4,65% para PIS/COFINS/CSLL, tabela de ISS do município). Qualquer divergência entre o valor retido e o devido dispara uma *flag* de alerta no grid do WPF.


* **Tratamento de Status Fiscais**: Identificação precisa de notas autorizadas, canceladas e substituídas. Notas canceladas são destacadas na UI para prevenir lançamentos indevidos na contabilidade.


* **Manifestação do Destinatário (NF-e/NFS-e)**: Permite o registro direto dos eventos de *Ciência*, *Confirmação*, *Desconhecimento* ou *Operação não Realizada* via mTLS.


* **Algoritmo de Matching de Valores (Cruzamento)**:
Compara notas recebidas com planilhas de referência ou pedidos de compra através da seguinte precedência estrita:


1. `Chave de Acesso Exata`

2. `CNPJ Emitente + Número do Documento + Série`

3. `CNPJ Emitente + Data Emissão + Valor Líquido (± Tolerância)`




---

## 5. Especificação da Interface do Usuario (`NEO-e.App`)

A interface WPF implementa padrões modernos utilizando `Microsoft.Extensions.Hosting` (`IHost`) integrando Dependency Injection (DI), MVVM rigoroso e renderização responsiva.

### 5.1 Componentes e Recursos do WPF Shell

* **Desacoplamento de Threads**: Nenhuma operação pesada de I/O, criptografia ou requisição HTTP roda na UI Thread. O progresso é reportado via `IProgress<T>` e atualizado assincronamente no ViewModel.


* **Resources e Temas Globais**: Definição centralizada de estilos e paleta de cores no `App.xaml`.


* **Gestão do Ciclo de Vida da Janela**: Configuração explícita do `ShutdownMode` no `App.xaml.cs` para evitar que o processo persista oculto em segundo plano:



```csharp
// App.xaml.cs - Garantia de ciclo de vida no WPF + IHost
protected override async void OnStartup(StartupEventArgs e)
{
    await _host.StartAsync();

    var mainWindow = _host.Services.GetRequiredService<MainWindow>();
    this.MainWindow = mainWindow;
    this.ShutdownMode = ShutdownMode.OnMainWindowClose; // Previne processo fantasma
    mainWindow.Show();

    base.OnStartup(e);
}

```

---

## 6. Requisitos Funcionais (RF) e Não-Funcionais (RNF)

### 6.1 Requisitos Funcionais

| ID | Requisito Funcional | Módulo Relacionado |
| --- | --- | --- |
| **RF-01** | Sincronizar documentos fiscais (NFS-e/NF-e) via API pública por NSU sem intervenção manual.

 | Sync Core

 |
| **RF-02** | Suportar carregamento em lote de múltiplos certificados A1 (`.pfx` ou Windows Store) por carteira de CNPJs.

 | Security / Certs

 |
| **RF-03** | Validar regras de negócio, calcular impostos e emitir lotes de NFS-e diretamente no portal SEFIN.

 | Emissão em Lote

 |
| **RF-04** | Exibir painel de pré-auditoria tributária com destaque visual para discrepâncias de retenção e notas canceladas.

 | Audit / Matching

 |
| **RF-05** | Permitir o envio de eventos de Manifestação do Destinatário (Ciência, Confirmação, Rejeição) assinados com mTLS.

 | Manifestação

 |
| **RF-06** | Exportar relatórios gerenciais consolidados e cruzamentos em formatos XLSX, CSV e estrutura de pastas em disco.

 | Reporting

 |

### 6.2 Requisitos Não-Funcionais

| ID | Requisito Não-Funcional | Meta / Métrica |
| --- | --- | --- |
| **RNF-01** | **Performance de Processamento** | Sincronizar no mínimo 1.000 documentos/min por CNPJ. Suportar carteiras com $\ge 100$ empresas ativas.

 |
| **RNF-02** | **Responsividade da Interface** | Rendimento visual sustentado em 60 FPS no WPF. Grade responsiva capaz de filtrar $\ge 10.000$ registros sem *freezing* da UI.

 |
| **RNF-03** | **Idempotência de Arquivos** | Repetir a execução de download ou emissão não duplica arquivos nem causa inconsistência no banco local.

 |
| **RNF-04** | **Concorrência e Rate Limiting** | Execução paralela controlada via `SemaphoreSlim` (limite de 2 a 4 conexões simultâneas) respeitando as respostas `429 Too Many Requests` e cabeçalhos `Retry-After` da API.

 |

---

## 7. Seguridade, Compliance e Governança

1. **Proteção de Dados Sensíveis (LGPD)**: Todas as chaves privadas e senhas de certificados são mantidas protegidas pelo subsistema de segurança do Windows (DPAPI ou Windows Credential Manager). Nenhuma chave privada é gravada em texto claro ou exportada para logs.


2. **Trilha de Auditoria (Audit Log)**: Todas as ações conclusivas (emissões de notas, envios de eventos de manifestação ou alterações de configuração) geram registros estruturados no SQLite com *timestamp* (`DateTimeOffset`), CNPJ do certificado utilizado e identificador do operador.


3. **Resiliência e Re tentativas (Polly / Custom Handler)**: Falhas transitórias de rede (HTTP `500`, `502`, `503`, `504`, `429`) utilizam política de *Exponential Backoff* com *Jitter*, abortando imediatamente caso o erro seja de certificado expirado ou falta de permissão.



---

## 8. Considerações Finais e Roadmap de Execução

Com a aprovação desta RFC, o desenvolvimento da plataforma **NEO-e** seguirá a seguinte esteira de marcos:

* **Fase 1 (Sincronização & Core Base)**: Finalização e estabilização da arquitetura *Clean*, DI/Generic Host no WPF e motor de download mTLS com NSU para NFS-e (Evolução RFC-001).


* **Fase 2 (Emissão em Lote & Regras Fiscais)**: Implementação do módulo *Nota Rápida Engine* em .NET 8, integração com a API da SEFIN e assinador XMLDSig nativo.


* **Fase 3 (Pré-Auditoria & Manifestação)**: Implementação do painel de recálculo de retenções, identificação de cancelamentos, evento de manifestação e algoritmo de matching de valores (Evolução RFC-002).


* **Fase 4 (Extensibilidade ERP)**: Construção dos adaptadores de integração via API local ou arquivos JSON para ERPs (Sankhya, NetSuite, etc.).