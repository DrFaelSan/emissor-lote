# Estado Atual e Proximo Epic

| Campo | Valor |
|---|---|
| Data de referencia | 2026-10-05 |
| Projeto | NEO-e |
| Estado | **EPIC 5 - WPF Moderno CONCLUÍDO** (UI-001 a UI-006 implementados) |
| Proximo EPIC | **EPIC 6 - Lacunas, Documentos e Relatórios** ou **RFC-002 Manifestação + Cruzamento** |
| Documento de referencia | [TASK-EPIC-05-WPF.md](../tasks/TASK-EPIC-05-WPF.md) |

## 1. Resumo

O projeto possui a solution .NET 8 completa com as camadas Domain, Application, Contracts, Infrastructure e App (WPF), além dos projetos de testes.

O motor de sincronização por CNPJ/NSU está implementado e testado. A interface WPF (MVVM) está **completa e funcional** com:
- Shell, tema, navegação, estados visuais
- Configuração de pastas, ambiente, estrutura
- Grid de empresas com busca, seleção, validação
- **Coluna de senha mascarada (PasswordBox) - só em memória**
- **Comandos: Baixar XML, Baixar desde o início (2 etapas), Parar (cancelamento cooperativo)**
- **Progresso com barra, CNPJ/NSU atual, contadores, log colorido (sem dados sensíveis)**
- Carregamento de NSU persistido do SQLite ao descobrir certificados

## 2. Estado por Epic

### EPIC 1 - Solução, configuração e segurança

**Estado: Base implementada.**

- Solution e projetos .NET 8 criados.
- Configurações de armazenamento, ADN, ambiente, certificados e logging definidas.
- Ambiente Restrita e Produção com URLs separadas.
- Produção exige confirmação explícita para troca.
- Modelo de erros de domínio definido (certificado, rede, API, parsing, estado).

**Pendências para encerrar:**
- Formalizar política de armazenamento de senhas com DPAPI ou Windows Credential Manager (hoje só memória).
- Adicionar validação automatizada de ausência de segredos em logs/arquivos.

### EPIC 2 - Certificados A1 e mTLS

**Estado: Base implementada.**

- Descoberta de `.pfx`/`.p12` em pasta configurada.
- Leitura de subject, issuer, validade, thumbprint, chave privada.
- Carregamento com senha fornecida (em memória).
- Validação de validade, chave privada e CNPJ do certificado.
- Cliente mTLS separado por certificado, ciclo de vida reutilizável (HttpClient por thumbprint).

**Pendências para encerrar:**
- Confirmar regra de extração do CNPJ no contrato oficial do certificado.
- Criar testes com certificados sanitizados/fixtures controlados.

### EPIC 3 - Cliente ADN e resiliência HTTP

**Estado: Implementação inicial concluída; homologação pendente.**

- `IAdnClient` tipado com `GetDfeAsync` (NSU) e `GetEventosAsync` (chave).
- Ambiente ativo define BaseUrl (Restrita/Produção).
- Retry com backoff exponencial + jitter para 429/5xx/timeout.
- Respeita `Retry-After`. Cancelamento propagado.
- `HttpClient` reutilizado por certificado (ConcurrentDictionary).
- Payload fiscal completo removido dos logs.

**Pendências para encerrar:**
- Registrar contrato real da API em `docs/api/` com fixture sanitizada.
- Adicionar rate limiting local por empresa e global (SemaphoreSlim).
- Executar smoke test autorizado em Produção Restrita.
- Cobrir 400, 401, 403, 429, 5xx, timeout, resposta inválida com fake handler.

### EPIC 4 - NSU, lote e idempotência

**Estado: Fluxo principal implementado e testado.**

- Estado de sincronização independente por CNPJ (`EstadoSincronizacao`).
- NSU negativo rejeitado (value object `Nsu`).
- Persistência SQLite do NSU e documentos (transacional).
- Parser XML: Base64 → GZip → XML, extrai chave, CNPJs, datas, número, série, valor.
- Escrita atômica (temp + rename) em pasta CNPJ/Tipo/Ano/Mês.
- Idempotência: hash SHA-256; conteúdo igual ignora; diferente não sobrescreve.
- NSU confirmado **após** gravação bem-sucedida dos documentos.
- Falha de documento impede confirmação do lote (rollback NSU).
- Cancelamento e progresso por empresa no caso de uso.
- Carteira processada serialmente (MVP).

**Pendências para encerrar:**
- Detecção e backup de banco SQLite corrompido.
- Confirmar semântica final de `ultimoNsu`, `ultNSU`, `maxNSU` com contrato oficial.
- Testes de interrupção em cada etapa do lote.
- Separar explicitamente resultados: sucesso, erro, ignorado, cancelado na evidência.

### EPIC 5 - WPF Moderno ✅ **CONCLUÍDO**

| Task | Status | Detalhes |
|------|--------|----------|
| **UI-001** Shell/navegação | ✅ | Janela MVVM, tema em `App.xaml`, estados visual, responsivo |
| **UI-002** Config pastas | ✅ | Certificados, destino, ambiente, estrutura Ano/Mês/Tipo; validação prévia |
| **UI-003** Grid empresas | ✅ | Seleção, busca (CNPJ/nome/arquivo/thumbprint), validação, NSU, situação |
| **UI-004** Fluxo senha | ✅ | `PasswordBox` na grid, mascarada, só certificados com chave privada, só memória |
| **UI-005** Comandos lote | ✅ | `StartSyncCommand` (valida + bloqueia Prod), `ResetNsuCommand` (2 etapas), `CancelSyncCommand` (cooperativo) |
| **UI-006** Progresso/logs | ✅ | Barra progresso, `SyncProgressText`, contadores públicos, `LogMessages` colorido (Info/Warning/Error/Success), máx 100 msgs |

**Critérios de aceite atendidos:**
- Fluxo principal executa sem ViewModel acessar controles WPF diretamente.
- Usuário entende estado de cada empresa (grid + log + progresso).
- Iniciar/cancelar/reset têm estados e confirmações corretas.
- Produção bloqueada por padrão (requer confirmação explícita).
- Senhas nunca persistidas em config/log/ViewModel (apenas memória durante lote).
- Ambiente ativo visível no topo da grid.

### EPIC 6 - Lacunas, Documentos e Relatórios (Próximo - RFC-001)

| Task | Status | Detalhes |
|------|--------|----------|
| DOC-001 Inventariar NSUs | 🔄 Parcial | SQLite já persiste metadados; falta indexador dedicado + detecção lacunas |
| DOC-002 Análise de lacunas | ❌ | Identificar NSUs faltantes entre primeiro e último conhecido |
| DOC-003 Recuperação assistida | ❌ | Reconsultar NSUs selecionados com limite/confirmação |
| DOC-004 Exportar Excel/CSV | ❌ | Resultado execução + inventário (sem dados sensíveis) |
| DOC-005 Gerar DANFSe | ❌ | PDF associado ao XML (depende de biblioteca/licença) |

### RFC-002 - Manifestação + Cruzamento de Valores (Alternativa Próxima)

| Epic | Escopo | Status |
|------|--------|--------|
| EPIC 00 Contratos | Validar endpoints NF-e (SEFAZ) e NFS-e (ADN) p/ manifestação | ❌ Bloqueia envio |
| EPIC 01 Documentos | Parser NF-e, parser NFS-e, indexador XMLs RFC-001, classificar recebidas | ❌ |
| EPIC 02 Manifestação | Consulta eventos, envio Ciência/Confirmação/Desconhecimento/Op.NãoRealizada, 2 etapas | ❌ |
| EPIC 03 Auditoria | Request/response sanitizado, protocolo, thumbprint, usuário, timestamp append-only | ❌ |
| EPIC 04 Cruzamento | Importar Excel/CSV, motor match 4 níveis, classificar OK/Divergente/SemRef/Ambiguidade | ❌ |
| EPIC 05 WPF Manifestação | Grid consolidada, filtros, seleção lote, exportação, alertas prazo | ❌ |
| EPIC 06 Qualidade | Testes contrato, homologação evento aceito/rejeitado, casos cruzamento, instalação | ❌ |

## 3. Evidência de Validação Atual

Em 2026-10-05 foram executados:

- `dotnet build NEO-e.slnx --no-restore`: **sucesso** (0 warnings, 0 errors).
- `dotnet test NEO-e.slnx --no-restore`: **9 testes aprovados** (7 unit + 1 integration + 1 homologação).
- Diagnóstico de arquivos alterados: nenhum erro encontrado.

**Testes unitários cobrem:**
- Rejeição de NSU negativo.
- Monotonicidade do estado de sincronização.
- Decodificação GZip e parsing de XML com namespace.
- Escrita idempotente e limpeza do arquivo temporário.
- Persistência e leitura de documento no SQLite.
- Registro de logger Serilog via DI.

## 4. Próximos Passos Recomendados

### Opção A: Completar RFC-001 (EPIC 6) - Foco em lacunas/relatórios
1. Implementar indexador de lacunas NSU (DOC-001/002)
2. Recuperação assistida de lacunas (DOC-003)
3. Exportação Excel/CSV de execução e inventário (DOC-004)
4. DANFSe (DOC-005) - após definir biblioteca

### Opção B: Iniciar RFC-002 - Manifestação + Cruzamento (Maior valor fiscal)
1. **EPIC 00**: Validar contratos oficiais NF-e/NFS-e para manifestação (homologação)
2. **EPIC 01**: Modelar `DocumentoRecebido`, parsers NF-e/NFS-e, indexar XMLs existentes
3. **EPIC 02**: Consulta/envio eventos (Ciência, Confirmação, Desconhecimento, Op.NãoRealizada)
4. **EPIC 03**: Auditoria completa de cada manifestação
5. **EPIC 04**: Motor de cruzamento de valores (importação Excel/CSV + match 4 níveis)
6. **EPIC 05/06**: WPF + qualidade/homologação

### Opção C: Homologação Produção Restrita (Pré-requisito para ambas)
- Registrar contrato real API ADN em `docs/api/` com fixtures sanitizadas
- Rate limiting local + smoke test com certificado de homologação
- Cobrir cenários de erro com fake handler (400, 401, 403, 429, 5xx, timeout, payload inválido)

## 5. Riscos Atuais

| Risco | Mitigação |
|-------|-----------|
| API ADN sem contrato/fixture oficial | Registrar em `docs/api/` antes de homologação |
| UI habilita operações conclusivas/Produção por padrão | Já bloqueado: Produção requer confirmação; Reset NSU = 2 etapas |
| Senha do certificado em log/memória prolongada | Já só memória durante lote; falta DPAPI para persistir entre sessões |
| Parser diverge do schema oficial | Ajustar após validar contrato real (EPIC 3 / RFC-002 EPIC 00) |
| SQLite corrompido perde estado NSU | Implementar detecção/backup (EPIC 4 pendente) |

## 6. Decisão Necessária

**Qual direção priorizar?**

- [ ] **Opção A**: Completar RFC-001 (EPIC 6 - lacunas/relatórios/DANFSe)
- [ ] **Opção B**: Iniciar RFC-002 (Manifestação + Cruzamento - maior valor fiscal)
- [ ] **Opção C**: Homologação Produção Restrita primeiro (pré-requisito para A e B)
- [ ] **Paralelo**: C + (A ou B) - homologação enquanto planeja próximo epic

---

> **Nota:** O EPIC 5 (WPF) atende todos os critérios de "Pronto quando" do [TASK-EPIC-05-WPF.md](../tasks/TASK-EPIC-05-WPF.md). A aplicação está operacional para download em lote de NFS-e via ADN.