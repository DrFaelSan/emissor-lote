# Plano de Implementação Paralelo - NEO-e

**Data:** 2026-10-05  
**Objetivo:** Executar em paralelo três frentes:
- **Trilha A**: Completar RFC-001 (EPIC 6 - Lacunas, Documentos, Relatórios)
- **Trilha B**: Iniciar RFC-002 (Manifestação + Cruzamento de Valores)
- **Trilha C**: Homologação Produção Restrita (Pré-requisito para A e B)

---

## Visão Geral de Dependências

```
Trilha C (Homologação) ──┬──► Trilha A (EPIC 6) ──► DANFSe, Excel, Lacunas
                         │
                         └──► Trilha B (RFC-002) ──► Manifestação, Cruzamento
                              │
                              └── Requer: Contratos validados (C), Fixtures (C), Rate limiting (C)
```

**Estratégia:** Iniciar Trilha C imediatamente (bloqueia validação real). Trilha A e B avançam com mocks/fixtures até homologação liberar contratos reais.

---

## Trilha C: Homologação Produção Restrita (Prioridade Máxima - Bloqueia Validação Real)

### C-001: Contrato API ADN/SEFAZ Documentado
| Task | Descrição | Entregável | Prazo |
|------|-----------|------------|-------|
| C-001.1 | Baixar OpenAPI/Swagger oficial ADN (Restrita + Produção) | `docs/api/adn-openapi-restrita.json`, `docs/api/adn-openapi-producao.json` | 1 dia |
| C-001.2 | Baixar OpenAPI/Swagger oficial SEFAZ NF-e (Distribuição + Eventos) | `docs/api/sefaz-nfe-openapi.json` | 1 dia |
| C-001.3 | Documentar endpoints, headers, autenticação, parâmetros, códigos de erro | `docs/api/CONTRATO-ADN.md`, `docs/api/CONTRATO-SEFAZ-NFE.md` | 2 dias |
| C-001.4 | Registrar semântica `NSU`, `ultNSU`, `maxNSU`, paginação, compactação | Seção nos docs acima | 1 dia |

### C-002: Fixtures Sanitizadas
| Task | Descrição | Entregável | Prazo |
|------|-----------|------------|-------|
| C-002.1 | Fixture: Lote sucesso (50 docs variados: emitidas, recebidas, eventos) | `docs/fixtures/adn-lote-sucesso.json` | 1 dia |
| C-002.2 | Fixture: Lote vazio (caixa vazia) | `docs/fixtures/adn-lote-vazio.json` | 0.5 dia |
| C-002.3 | Fixture: Erros HTTP 400, 401, 403, 429, 500, 503 | `docs/fixtures/adn-erros-*.json` | 1 dia |
| C-002.4 | Fixture: Payload inválido (JSON malformado, Base64 inválido, GZip corrompido) | `docs/fixtures/adn-payload-invalido.json` | 1 dia |
| C-002.5 | Fixture: Eventos NF-e (210210, 210200, 210220, 210240) | `docs/fixtures/sefaz-eventos-*.json` | 1 dia |
| C-002.6 | Fixture: Eventos NFS-e (confirmação tomador ADN) | `docs/fixtures/adn-eventos-nfse.json` | 1 dia |
| C-002.7 | XMLs NFS-e/NF-e sanitizados (emitida, recebida, cancelamento, carta correção) | `docs/fixtures/xmls/` | 2 dias |

### C-003: Rate Limiting Local
| Task | Descrição | Entregável | Prazo |
|------|-----------|------------|-------|
| C-003.1 | `SemaphoreSlim` global (ex: 4 concurrent) + por empresa (ex: 1 concurrent) | `Infrastructure/RateLimiter/AdnRateLimiter.cs` | 1 dia |
| C-003.2 | Delay configurável entre requests (default 300ms) | Config `AdnSettings.RateLimitDelayMs` | 0.5 dia |
| C-003.3 | Métricas: chamadas, retries, 429, tempo médio | `IAdnClient` expõe `AdnClientMetrics` | 0.5 dia |
| C-003.4 | Integração no `AdnHttpClient` antes de `ExecuteWithRetryAsync` | Atualizar `AdnHttpClient.cs` | 0.5 dia |

### C-004: Fake HttpMessageHandler para Testes Offline
| Task | Descrição | Entregável | Prazo |
|------|-----------|------------|-------|
| C-004.1 | `FakeAdnHandler : HttpMessageHandler` que serve fixtures por URL/NSU | `tests/NEO-e.IntegrationTests/Fakes/FakeAdnHandler.cs` | 2 dias |
| C-004.2 | Simula: sucesso, vazio, 400, 401, 403, 429 (com Retry-After), 5xx, timeout, payload inválido | Métodos `SetupSuccess()`, `SetupError(statusCode)`, etc. | 1 dia |
| C-004.3 | Injeta via DI nos testes de integração | Atualizar `ServiceCollectionExtensions` p/ testes | 0.5 dia |
| C-004.4 | Testes cobrindo todos cenários de erro + retry + cancelamento | `AdnClientContractTests.cs` | 2 dias |

### C-005: Smoke Test Produção Restrita
| Task | Descrição | Entregável | Prazo |
|------|-----------|------------|-------|
| C-005.1 | Obter certificado de homologação + autorização ambiente | Certificado `.pfx` teste + e-mail autorização | Externo |
| C-005.2 | Executar sync de 1 CNPJ (poucos docs) validando request/response real vs fixtures | Log comparativo sanitizado | 1 dia |
| C-005.3 | Medir latência, headers, limite lote (50), comportamento NSU | `docs/evidencias/smoke-test-YYYYMMDD.md` | 0.5 dia |
| C-005.4 | Validar parser com XMLs reais (namespaces, campos opcionais) | Ajustes no `NfseXmlParser` se necessário | 1 dia |

**Total Trilha C: ~15-18 dias úteis** (pode rodar em paralelo com início de A/B)

---

## Trilha A: EPIC 6 - Lacunas, Documentos e Relatórios (RFC-001 Completo)

### A-001: Inventário e Análise de Lacunas (DOC-001/002)
| Task | Descrição | Entregável | Prazo |
|------|-----------|------------|-------|
| A-001.1 | `IGapAnalyzer` interface + `SqliteGapAnalyzer` implementation | `Application/Contracts/IGapAnalyzer.cs`, `Infrastructure/Persistence/SqliteGapAnalyzer.cs` | 2 dias |
| A-001.2 | Indexador: varrer SQLite `documentos` + FS, construir mapa NSU→Chave por CNPJ | `GapAnalysisResult` com `GapInterval[]` | 2 dias |
| A-001.3 | Heurística lacuna: NSU sequencial sem documento vs. NSU que API diz não ter doc | Diferenciar "lacuna real" de "NSU sem documento" | 1 dia |
| A-001.4 | UI: Botão "Analisar Lacunas" → exibe intervalos + qtde | `MainWindowViewModel.AnalyzeGapsAsync()`, botão na UI | 1 dia |

### A-002: Recuperação Assistida (DOC-003)
| Task | Descrição | Entregável | Prazo |
|------|-----------|------------|-------|
| A-002.1 | `GapRecoveryResult` + `RecoverAsync(Cnpj, IReadOnlyList<Nsu>)` | Reutiliza `SincronizarEmpresaUseCase` com NSUs específicos | 2 dias |
| A-002.2 | UI: Selecionar intervalos/lacunas → "Recuperar" → confirmação → progresso | Grid com checkbox por intervalo, botão recuperar | 1 dia |
| A-002.3 | Respeita rate limit, retry, idempotência, cancelamento | Reusa infraestrutura existente | 0.5 dia |

### A-003: Exportação Excel/CSV (DOC-004)
| Task | Descrição | Entregável | Prazo |
|------|-----------|------------|-------|
| A-003.1 | `IExcelExporter` + `ClosedXmlExcelExporter` (ClosedXML) | `Infrastructure/Export/ClosedXmlExcelExporter.cs` | 2 dias |
| A-003.2 | Export Execução: CNPJ, Empresa, NSU Inicial/Final, Docs, Erros, Duração, Status | `ExportExecutionAsync()` | 1 dia |
| A-003.3 | Export Inventário: CNPJ, Chave, Tipo, Número, Série, Data, Valor, Caminho, NSU | `ExportInventoryAsync()` | 1 dia |
| A-003.4 | UI: Botões "Exportar Execução", "Exportar Inventário" + SaveFileDialog | `MainWindowViewModel.ExportExecutionAsync()`, `ExportInventoryAsync()` | 1 dia |
| A-003.5 | Sem bloquear UI (async), tratar permissão escrita | `Task.Run` + try/catch `UnauthorizedAccessException` | 0.5 dia |

### A-004: DANFSe (DOC-005) - Decisão de Biblioteca
| Task | Descrição | Entregável | Prazo |
|------|-----------|------------|-------|
| A-004.1 | Avaliar: `DANFSe` (NuGet), `OpenAC.Net.DANFSe`, gerar próprio (QuestPDF) | Decisão técnica registrada em `docs/decisions/DANFSe-Library.md` | 1 dia |
| A-004.2 | Implementar `IDanfseGenerator` + gerador escolhido | `Infrastructure/Danfse/` | 3-5 dias |
| A-004.3 | UI: Botão "Gerar DANFSe" (seleção múltipla) → PDFs associados aos XMLs | `MainWindowViewModel.GenerateDanfseAsync()` | 1 dia |
| A-004.4 | XML inválido não derruba lote; PDF não substitui original | Try/catch por documento, log erro | 0.5 dia |

**Total Trilha A: ~18-22 dias úteis**

---

## Trilha B: RFC-002 - Manifestação + Cruzamento de Valores

### B-000: EPIC 00 - Contratos Fiscais (Bloqueado por Trilha C)
| Task | Descrição | Entregável | Prazo |
|------|-----------|------------|-------|
| B-000.1 | Validar endpoint NF-e Distribuição Destinatário (SEFAZ) - NSU, schemas, auth | `docs/api/CONTRATO-SEFAZ-DISTRIBUICAO.md` | Após C-001 |
| B-000.2 | Validar eventos NF-e: 210210 (Ciência), 210200 (Confirmação), 210220 (Desconhecimento), 210240 (Op.NãoRealizada) - SOAP/REST, assinatura, sequência, rejeições | `docs/api/CONTRATO-SEFAZ-EVENTOS.md` | Após C-001 |
| B-000.3 | Validar eventos NFS-e ADN: consulta (`GET /NFSe/{chave}/Eventos`), registro confirmação tomador | `docs/api/CONTRATO-ADN-EVENTOS.md` | Após C-001 |
| B-000.4 | Confirmar prazos (NT vigente): Ciência ~10 dias, Conclusivas 90/180 dias | `docs/api/PRAZOS-MANIFESTACAO.md` | 0.5 dia |
| B-000.5 | Feature flag: NFS-e eventos só se contrato estável | `ManifestationSettings.EnableNfseEvents` | 0.5 dia |

### B-001: EPIC 01 - Modelo Documentos e Leitura XMLs
| Task | Descrição | Entregável | Prazo |
|------|-----------|------------|-------|
| B-001.1 | `DocumentoRecebido` entity: tipo (NFSe/NFE), chave, CNPJ emitente/destinatário, nº, série, data emissão/autorização, valor, status manifestação, prazo, origem (RFC-001/XML/Evento) | `Domain/Entities/DocumentoRecebido.cs` | 1 dia |
| B-001.2 | Parser NF-e: namespaces `nfe`, `protNFe`, `infNFe`, emitente, destinatário, total, autorização | `Infrastructure/Parsers/NfeXmlParser.cs` | 3 dias |
| B-001.3 | Parser NFS-e: schemas ADN vigentes, tomador, prestador, chave, competência, valor, eventos disponíveis | `Infrastructure/Parsers/NfseRecebidaXmlParser.cs` | 2 dias |
| B-001.4 | Indexador incremental: varrer árvore pastas RFC-001, hash, tamanho, data, caminho, status parsing | `Application/UseCases/IndexReceivedDocumentsUseCase.cs` | 2 dias |
| B-001.5 | Classificar: destinatário = CNPJ empresa (matriz/filial/raiz configurável) vs emitidas pela própria | Regra configurável `MatrizFilialRule` | 1 dia |
| B-001.6 | Persistência SQLite: tabela `documentos_recebidos` + índices CNPJ+data, chave, status | `Infrastructure/Persistence/SqliteReceivedDocumentRepository.cs` | 2 dias |

### B-002: EPIC 02 - Consulta e Envio Manifestações
| Task | Descrição | Entregável | Prazo |
|------|-----------|------------|-------|
| B-002.1 | `IManifestationClient`: `GetEventsAsync(chave, cert)`, `SendEventAsync(evento, cert)` | `Application/Contracts/IManifestationClient.cs` | 1 dia |
| B-002.2 | Cliente NF-e (SEFAZ): SOAP/REST, assinatura XMLDSIG, certificado destinatário | `Infrastructure/Http/SefazManifestationClient.cs` | 4 dias |
| B-002.3 | Cliente NFS-e (ADN): REST, eventos tomador, certificado destinatário | `Infrastructure/Http/AdnManifestationClient.cs` | 3 dias |
| B-002.4 | UseCases: `SendCienciaAsync`, `SendConfirmacaoAsync`, `SendDesconhecimentoAsync`, `SendOperacaoNaoRealizadaAsync` (com justificativa) | `Application/UseCases/ManifestationUseCases.cs` | 3 dias |
| B-002.5 | Regras transição: Ciência → Confirmação/Desconhecimento/OpNãoRealizada; bloquear se evento anterior incompatível | Validação no UseCase | 1 dia |
| B-002.6 | Confirmação 2 etapas obrigatória para eventos conclusivos | UI + UseCase | 1 dia |
| B-002.7 | Tratamento: duplicidade (já manifestado), rejeição (códigos SEFAZ), timeout, cancelamento | Try/catch + mapeamento códigos erro | 1 dia |

### B-003: EPIC 03 - Persistência e Auditoria
| Task | Descrição | Entregável | Prazo |
|------|-----------|------------|-------|
| B-003.1 | `ManifestacaoRegistro` entity: empresa, documento, evento, usuário local, timestamp, request sanitizado, response, protocolo, resultado | `Domain/Entities/ManifestacaoRegistro.cs` | 1 dia |
| B-003.2 | Repositório SQLite append-only: `SaveAsync`, `GetByDocumentoAsync`, `GetByEmpresaPeriodoAsync` | `Infrastructure/Persistence/SqliteManifestationRepository.cs` | 1 dia |
| B-003.3 | Sanitizador request/response: remover chave privada, senha, XML completo (manter só metadados) | `Infrastructure/Security/ManifestationSanitizer.cs` | 1 dia |
| B-003.4 | Log estruturado: cada tentativa (sucesso/falha) com correlation ID | Serilog + `ManifestationAuditLogger` | 0.5 dia |

### B-004: EPIC 04 - Cruzamento de Valores
| Task | Descrição | Entregável | Prazo |
|------|-----------|------------|-------|
| B-004.1 | `ValorReferencia` DTO: CNPJ Emitente, Valor, Data, Chave?, Número?, Série?, Observação? | `Application/Contracts/ValorReferencia.cs` | 0.5 dia |
| B-004.2 | Importador Excel/CSV (ClosedXML): validação colunas obrigatórias, parsing decimal pt-BR, datas | `Infrastructure/Import/ExcelValorReferenciaImporter.cs` | 2 dias |
| B-004.3 | Motor match 4 níveis: 1) Chave exata, 2) CNPJ+Num+Série, 3) CNPJ+Data+Valor (±tol%), 4) CNPJ+Período+Soma | `Domain/Services/ValorCrossMatchEngine.cs` | 3 dias |
| B-004.4 | Classificação: OK, Divergente (delta), Sem Referência, Ambiguidade (múltiplos match) | `CrossMatchResult` enum + detalhes | 1 dia |
| B-004.5 | Tolerância configurável por empresa (default 0,01 / 0,5%) | `Empresa.ValorTolerancePercent` | 0.5 dia |
| B-004.6 | UI: Botão "Importar Valores", grid com coluna "Cruzamento" (OK/Divergente/SemRef/Ambiguidade), filtro | `MainWindowViewModel.ImportValoresAsync()`, coluna na grid manifestação | 2 dias |
| B-004.7 | Relatório divergências: exportar Excel com valor nota × valor ref × delta | `ExportDivergenciasAsync()` | 1 dia |

### B-005: EPIC 05 - WPF Manifestação
| Task | Descrição | Entregável | Prazo |
|------|-----------|------------|-------|
| B-005.1 | Nova aba/janela "Manifestação" (ou integrar na principal) | `ManifestoWindow.xaml` + `ManifestoWindowViewModel.cs` | 2 dias |
| B-005.2 | Grid consolidada: Chave, Emitente, Data, Valor, Tipo, Status Manifestação, Prazo Restante, Cruzamento, Situação | `DocumentoRecebidoRowViewModel` | 2 dias |
| B-005.3 | Filtros: empresa, período, status (Pendente/Manifestada/Erro), tipo, cruzamento | `CollectionView.Filter` | 1 dia |
| B-005.4 | Ações em lote: Ciência, Confirmação, Desconhecimento, Op.NãoRealizada (com justificativa) | `SendManifestationBatchAsync()` | 2 dias |
| B-005.5 | Alertas visuais: prazo vencendo (amarelo), vencido (vermelho), confirmação automática iminente | Converter `DaysToDeadline` → cor/ícone | 1 dia |
| B-005.6 | Exportar pendências/divergências Excel | Reusa `IExcelExporter` | 0.5 dia |

### B-006: EPIC 06 - Qualidade e Homologação
| Task | Descrição | Entregável | Prazo |
|------|-----------|------------|-------|
| B-006.1 | Testes contrato: fixtures NF-e/NFS-e eventos (sucesso, duplicidade, prazo expirado, rejeições) | `tests/NEO-e.UnitTests/ManifestationContractTests.cs` | 2 dias |
| B-006.2 | Testes integração: fake handlers SEFAZ/ADN, mTLS simulado, retry, cancelamento | `tests/NEO-e.IntegrationTests/ManifestationIntegrationTests.cs` | 2 dias |
| B-006.3 | Homologação: evento aceito + evento rejeitado (SEFAZ + ADN) | Evidência sanitizada `docs/evidencias/` | Externo |
| B-006.4 | Casos cruzamento: OK, Divergente, Sem Referência, Ambiguidade | Testes unitários `ValorCrossMatchEngineTests.cs` | 1 dia |
| B-006.5 | Auditoria: inspeção logs, banco, exportações | Checklist `docs/evidencias/auditoria-manifestacao.md` | 1 dia |
| B-006.6 | Release: instalação limpa, backup, retomada, bloqueio Produção por padrão | Checklist `REL-001/002` | 1 dia |

**Total Trilha B: ~35-42 dias úteis** (muito depende de Trilha C para contratos reais)

---

## Cronograma Paralelo Sugerido (12 Semanas)

| Semana | Trilha C (Homologação) | Trilha A (EPIC 6) | Trilha B (RFC-002) |
|--------|------------------------|-------------------|-------------------|
| 1-2 | C-001, C-002 (contrato + fixtures) | A-001 (GapAnalyzer + Indexador) | B-001 (Modelo + Parsers + Indexador) |
| 3-4 | C-003 (Rate limiting), C-004 (Fake handler) | A-002 (Recuperação assistida) | B-001 cont., B-002.1-002.3 (Clients) |
| 5-6 | C-004 cont., C-005 (Smoke test) | A-003 (Excel Export) | B-002.4-002.7 (UseCases + Regras) |
| 7-8 | **Validação contratos reais** → Ajuste parsers/clients | A-004 (DANFSe - decisão + impl) | B-003 (Auditoria), B-004 (Cruzamento) |
| 9-10 | Fixes pós-homologação | A-004 cont. | B-005 (WPF Manifestação) |
| 11-12 | Documentação final, CI/CD | Integração A+B, testes ponta a ponta | B-006 (Qualidade + Homologação RFC-002) |

---

## Riscos e Mitigações

| Risco | Impacto | Mitigação |
|-------|---------|-----------|
| Certificado homologação não liberado | Bloqueia C-005, atrasa validação real | Iniciar A/B com mocks; homologar assim que possível |
| Contrato ADN/SEFAZ diverge das fixtures | Parsers/clients quebram em homologação | Fake handler flexível; ajustar rápido pós-smoke test |
| Escopo RFC-002 muito grande | Atraso entrega | MVP: só NF-e manifestação + cruzamento básico; NFS-e events fase 2 |
| DANFSe biblioteca com licença/bugs | A-004 atrasa | Decisão semana 1; fallback: gerar PDF simples com QuestPDF |
| Paralelismo causa conflitos merge | Rework | Branches por feature; PRs pequenos; sync diário main |

---

## Definição de Pronto (Definition of Done) por Trilha

### Trilha C
- [ ] Contratos ADN/SEFAZ documentados em `docs/api/`
- [ ] Fixtures sanitizadas em `docs/fixtures/` (sucesso, vazio, erros, XMLs)
- [ ] Rate limiting implementado e testado
- [ ] Fake handler cobre todos cenários de erro
- [ ] Smoke test executado e evidência sanitizada salva
- [ ] Parsers ajustados conforme XMLs reais

### Trilha A
- [ ] Análise lacunas identifica intervalos reais (não falsos positivos)
- [ ] Recuperação assistida respeita idempotência/rate limit/cancelamento
- [ ] Export Excel/CSV abre corretamente, sem dados sensíveis
- [ ] DANFSe gera PDF válido associado ao XML (não substitui)
- [ ] Todos testes unitários passam; build limpo

### Trilha B
- [ ] Contratos NF-e/NFS-e manifestação validados (Trilha C)
- [ ] Parsers NF-e/NFS-e processam XMLs reais (fixtures + homologação)
- [ ] Envio 4 eventos NF-e funciona (Ciência, Confirmação, Desconhecimento, Op.NãoRealizada)
- [ ] Auditoria append-only com request/response sanitizado + protocolo
- [ ] Cruzamento 4 níveis classifica corretamente (testes cobrem 4 casos)
- [ ] WPF: grid, filtros, lote, alertas prazo, exportação funcionam
- [ ] Homologação: evento aceito + rejeitado em SEFAZ e ADN

---

## Próximos Passos Imediatos (Esta Semana)

1. **Iniciar C-001**: Baixar OpenAPI ADN/SEFAZ, documentar em `docs/api/`
2. **Iniciar C-002**: Criar fixtures a partir dos exemplos RFC + especificação
3. **Iniciar A-001.1**: `IGapAnalyzer` + `SqliteGapAnalyzer` (baseado no SQLite existente)
4. **Iniciar B-001.1**: `DocumentoRecebido` entity + repositório SQLite
5. **Criar branches**: `feature/homologacao`, `feature/epic6-lacunas`, `feature/rfc002-manifestacao`

---

## Comandos Úteis

```bash
# Build + Testes rápido
dotnet build NEO-e.slnx --no-restore && dotnet test NEO-e.slnx --no-restore

# Criar branch feature
git checkout -b feature/homologacao
git checkout -b feature/epic6-lacunas
git checkout -b feature/rfc002-manifestacao

# Ver diff antes de commit
git diff --stat
git diff src/

# Log estruturado para auditoria
# Serilog já configurado - logs em ./logs/app-YYYYMMDD.log
```