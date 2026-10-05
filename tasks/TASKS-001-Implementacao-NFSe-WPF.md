# TASKS-001: Implementacao do Download em Massa de NFS-e

Este backlog quebra a RFC-001 em tarefas pequenas e verificaveis para uma aplicacao desktop **C# / .NET 8 / WPF**, com foco na integracao tecnica com a API Nacional de NFS-e (ADN).

## Como usar

- Executar os itens na ordem das dependencias.
- Cada task deve gerar um commit pequeno e testavel.
- Nenhum endpoint, DTO ou regra da API deve ser considerado definitivo antes da validacao contra o Swagger/manual vigente.
- Tasks de integracao devem usar ambiente de producao restrita e certificados de homologacao.
- A aplicacao nao deve chamar producao por padrao.

## Status das tasks

Usar somente estes estados: `Backlog`, `Ready`, `In Progress`, `Blocked`, `In Review` e `Done`.

Uma task so pode entrar em `Ready` quando suas dependencias estiverem em `Done`, o contrato tecnico estiver identificado e a validacao esperada estiver definida. Uma task de integracao externa fica `Blocked` quando depender de credencial, ambiente ou contrato ainda nao confirmado.

## Formato obrigatorio de implementacao

Cada task deve registrar no issue ou pull request:

- Objetivo e escopo negativo.
- Dependencias e risco.
- Arquivos/projetos esperados.
- Criterios de aceite verificaveis.
- Comando ou evidencia de validacao.
- Impacto em seguranca, dados e compatibilidade.

Nao marcar uma task como concluida apenas porque o codigo compila.

## Definition of Done global

- Codigo compilando com warnings tratados no escopo da task.
- Teste automatizado ou evidencia manual reproduzivel, conforme o tipo da task.
- Logs sem senha, chave privada, token ou XML completo.
- CancellationToken respeitado em operacoes de rede e lote.
- Erros tecnicos e erros de negocio preservam status, codigo e mensagem original.
- Documentacao tecnica atualizada quando houver uma decisao de contrato.
- Sem emojis, comentarios de codigo ou dados fiscais reais adicionados.
- Sem mudanca silenciosa de contrato, schema, endpoint ou regra de negocio.
- Mudancas de persistencia possuem migration, estrategia de rollback e fixture de compatibilidade.
- Operacoes destrutivas ou com efeito fiscal possuem confirmacao explicita e teste de bloqueio.

## Gates de qualidade

### Gate G1 - Contrato

- Manual, Swagger, schema ou nota tecnica identificados por versao e data.
- Request, response e erros sanitizados registrados em `docs/api/`.
- Ambiente de homologacao separado de producao.

### Gate G2 - Dados

- CNPJ, chave, NSU, data e valor possuem validacao de entrada.
- Valores monetarios usam `decimal`.
- Instantes usam `DateTimeOffset`.
- Escrita de arquivo e estado e atomica ou transacional.

### Gate G3 - Seguranca

- Certificado e empresa foram vinculados antes da chamada.
- Senhas, chaves privadas, tokens e XML completo nao aparecem em logs.
- Cancelamento e timeout sao respeitados.

### Gate G4 - Operacao

- Reexecucao e idempotente.
- Falha parcial nao confirma estado inexistente.
- Evidencia de teste unitario, integracao ou homologacao anexada.

---

## EPIC 0 - Validacao do contrato da API ADN

Objetivo: eliminar suposicoes da RFC antes de construir o cliente HTTP.

### API-001 - Fixar a fonte oficial do contrato

**Descricao:** Registrar a versao/data do manual, Swagger e ambiente de homologacao usados pela implementacao.

**Aceite:**
- URLs oficiais registradas em `docs/api-adn.md`.
- Arquivo OpenAPI baixado e versionado ou identificacao de hash/data registrada.
- Producao restrita e producao possuem configuracoes separadas.

**Dependencias:** nenhuma.

### API-002 - Confirmar endpoint de distribuicao DFe

**Descricao:** Validar metodo, path, headers, autenticacao, query/path parameters e semantica de NSU do endpoint de distribuicao.

**Aceite:**
- Request real documentado com exemplo sanitizado.
- Response real documentado com `ultNSU`, `maxNSU` e lote.
- Confirmado se a consulta inicia no NSU informado ou no proximo NSU.
- Confirmado comportamento para caixa vazia, NSU invalido, certificado sem permissao e CNPJ divergente.

**Dependencias:** API-001.

### API-003 - Confirmar schema e compactacao dos documentos

**Descricao:** Mapear o JSON real do lote e o campo que contem o XML compactado.

**Aceite:**
- DTOs possuem nomes/tipos baseados no payload real.
- Confirmada ordem de decodificacao: JSON, Base64, GZip e XML.
- Existe fixture sanitizada de sucesso e de payload invalido.
- XML malformado ou compactacao invalida gera erro classificavel.

**Dependencias:** API-002.

### API-004 - Confirmar consulta de eventos

**Descricao:** Validar se eventos fazem parte da distribuicao DFe ou exigem consulta adicional por chave.

**Aceite:**
- Fluxo de eventos documentado.
- Endpoint, headers, autenticacao e resposta validados.
- Decisao registrada sobre incluir eventos na v1 ou deixar adaptador preparado.

**Dependencias:** API-001, API-002.

### API-005 - Criar fixtures e simulador HTTP

**Descricao:** Criar respostas JSON/XML sanitizadas e um `HttpMessageHandler` fake para testes sem rede.

**Aceite:**
- Fixtures cobrem lote com documentos, lote vazio, erro 401/403/429/5xx e payload invalido.
- Testes nao dependem de certificado real nem de internet.

**Dependencias:** API-002, API-003.

**Validacao minima:** executar testes com lote vazio, lote sem progresso, payload invalido, 401, 403, 429 e 5xx. Nenhum teste pode depender de internet.

---

## EPIC 1 - Solucao, configuracao e seguranca

### CORE-001 - Criar solution e projetos

**Descricao:** Criar a estrutura inicial da solution .NET 8.

**Estrutura sugerida:**

```text
src/
  NfseDownloader.App/             # WPF, Views, ViewModels, DI
  NfseDownloader.Application/     # Casos de uso e contratos
  NfseDownloader.Domain/          # Entidades, value objects e regras
  NfseDownloader.Infrastructure/ # HTTP, certificados, SQLite, arquivos
  NfseDownloader.Contracts/      # DTOs da API e serializacao
 tests/
  NfseDownloader.UnitTests/
  NfseDownloader.IntegrationTests/
 docs/
```

**Aceite:** `dotnet build` e `dotnet test` executam em uma maquina limpa com .NET 8.

**Dependencias:** nenhuma.

### CORE-002 - Configurar DI, Options e ambientes

**Descricao:** Configurar `Microsoft.Extensions.DependencyInjection`, `IOptions`, logging e ambientes `Restrita`/`Producao`.

**Aceite:**
- URL base nunca fica hardcoded no ViewModel.
- Ambiente ativo aparece na UI.
- Producao exige confirmacao explicita antes de qualquer execucao.
- Configuracao invalida impede o inicio com mensagem acionavel.

**Dependencias:** CORE-001.

**Validacao minima:** testar serializacao, classificacao de retry e sanitizacao de mensagens com senha, certificado e XML.

### CORE-003 - Definir politica de segredos

**Descricao:** Definir armazenamento da senha do PFX e politica de memoria/log.

**Aceite:**
- Senha nao e salva em JSON nem log.
- DPAPI ou Windows Credential Manager escolhido e documentado.
- Senha e descartada quando a operacao termina ou e cancelada.
- Teste verifica que logs nao contem a senha.

**Dependencias:** CORE-001.

### CORE-004 - Definir modelo de erro e resultado

**Descricao:** Padronizar erros de validacao, certificado, rede, API, parsing e filesystem.

**Aceite:**
- Cada erro informa categoria, CNPJ, NSU quando aplicavel e acao sugerida.
- Excecoes de dominio nao carregam dados sensiveis.
- Retry so ocorre para erros explicitamente retryable.

**Dependencias:** CORE-001.

---

## EPIC 2 - Certificados A1 e mTLS

### CERT-001 - Implementar descoberta de arquivos PFX

**Descricao:** Ler uma pasta configurada, filtrar `.pfx`/`.p12` e criar registros de certificado sem carregar senhas automaticamente.

**Aceite:**
- Arquivos ausentes, duplicados e extensoes invalidas sao tratados.
- A UI mostra arquivo, subject, issuer, validade e thumbprint quando disponivel.
- Chave privada nunca e exportada.

**Dependencias:** CORE-001.

### CERT-002 - Implementar carregamento seguro do PFX

**Descricao:** Carregar certificado com senha fornecida pelo usuario e flags adequadas ao processo.

**Aceite:**
- Senha incorreta produz status claro sem encerrar o lote inteiro.
- Certificado sem chave privada e rejeitado.
- Certificado expirado ou ainda nao valido e sinalizado antes do lote.

**Dependencias:** CERT-001, CORE-003.

### CERT-003 - Validar vinculo certificado-CNPJ

**Descricao:** Extrair e validar o CNPJ esperado conforme o contrato do certificado/API.

**Aceite:**
- Regra de extracao documentada e coberta por testes.
- Divergencia impede a consulta ou exige confirmacao explicita, conforme decisao de negocio.

**Dependencias:** CERT-002, API-002.

**Validacao minima:** verificar que um certificado de outra empresa nao pode ser selecionado para o CNPJ da operacao e que a validacao TLS permanece ativa.

### CERT-004 - Criar handler mTLS por empresa

**Descricao:** Criar `HttpClient`/handler associado ao certificado da empresa.

**Aceite:**
- Cada empresa usa seu proprio certificado.
- Handler e `HttpClient` sao descartados ao final do processamento.
- Teste verifica que o certificado e anexado ao request.
- Revogacao/validacao TLS nao e desabilitada globalmente.

**Dependencias:** CERT-002, API-002.

---

## EPIC 3 - Cliente ADN e resiliencia HTTP

### HTTP-001 - Implementar cliente tipado da distribuicao

**Descricao:** Criar interface `IAdnClient` e metodo tipado para buscar uma pagina por NSU.

**Aceite:**
- DTOs de request/response sao separados da camada de dominio.
- Timeout, headers `Accept` e correlation id sao configuraveis.
- Response body e liberado corretamente.

**Dependencias:** API-003, CERT-004.

### HTTP-002 - Mapear status HTTP e erros de negocio

**Descricao:** Transformar respostas HTTP e payloads de erro em excecoes/resultados tipados.

**Aceite:**
- 400, 401, 403, 404, 409, 429 e 5xx possuem comportamento documentado.
- Corpo de erro sanitizado pode ser gravado no log tecnico.
- 401/403 nao entram em retry automatico.

**Dependencias:** HTTP-001, CORE-004.

### HTTP-003 - Implementar retry e backoff

**Descricao:** Aplicar retry apenas para 429 e erros transitorios de rede/5xx, com limite e jitter configuraveis.

**Aceite:**
- Maximo padrao de 3 tentativas.
- `Retry-After` e respeitado quando presente.
- Cancelamento interrompe o backoff.
- Teste verifica que 400 e 401 nao sao repetidos.

**Dependencias:** HTTP-002.

### HTTP-004 - Implementar rate limiting local

**Descricao:** Limitar chamadas por empresa e globalmente, com intervalo configuravel.

**Aceite:**
- Limite padrao conservador documentado.
- Nenhuma chamada ocorre apos cancelamento.
- Metricas registram quantidade de chamadas, retries e 429.

**Dependencias:** HTTP-003.

### HTTP-005 - Teste de contrato em producao restrita

**Descricao:** Executar smoke test autenticado com certificado de homologacao.

**Aceite:**
- Request/response real comparado com fixtures.
- Latencia, headers e limite de lote registrados.
- Evidencia sanitizada salva em `docs/evidencias/`.

**Dependencias:** API-005, HTTP-004, CERT-004.

**Bloqueio:** nao executar sem certificado de homologacao, autorizacao de ambiente e evidencia sanitizada. Este teste nao pertence ao `dotnet test` padrao.

---

## EPIC 4 - NSU, lote e idempotencia

### SYNC-001 - Modelar estado por CNPJ

**Descricao:** Criar entidade de estado com CNPJ, ultimo NSU confirmado, ultima execucao e versao.

**Aceite:**
- Estado e independente por CNPJ.
- NSU nao aceita valor negativo.
- Schema possui versao para migracao futura.

**Dependencias:** CORE-001.

### SYNC-002 - Persistir estado atomicamente

**Descricao:** Implementar repositorio SQLite ou JSON atomicamente, conforme decisao registrada.

**Aceite:**
- Escrita usa transacao ou arquivo temporario + replace atomico.
- Corrupcao detectada gera backup e erro acionavel.
- Teste simula interrupcao antes do commit.

**Dependencias:** SYNC-001.

### SYNC-003 - Implementar cursor de pagina

**Descricao:** Definir algoritmo usando `ultimoNsu`, `ultNSU` e `maxNSU` reais da API.

**Aceite:**
- NSU so avanca depois de todos os documentos da pagina gravados.
- Lote vazio encerra sem avancar indevidamente.
- Resposta sem progresso gera falha de protecao contra loop infinito.
- Reinicio apos falha reprocessa somente o que ainda nao foi confirmado.

**Dependencias:** API-002, SYNC-002.

### SYNC-004 - Implementar descompactacao e validacao XML

**Descricao:** Decodificar documento, validar XML bem formado e extrair metadados necessarios.

**Aceite:**
- Falha em um documento e classificada sem perder o estado anterior.
- XML original e preservado quando o processamento puder continuar.
- Testes cobrem Base64/GZip invalido e encoding inesperado.

**Dependencias:** API-003.

### SYNC-005 - Implementar escritor idempotente

**Descricao:** Gravar XML em arquivo temporario e renomear atomicamente.

**Aceite:**
- Nome usa chave de acesso ou identificador validado.
- Mesmo conteudo nao gera duplicata.
- Conteudo diferente para o mesmo nome nao sobrescreve silenciosamente.
- Diretorios sao criados com seguranca.

**Dependencias:** SYNC-004.

### SYNC-006 - Implementar orquestracao de uma empresa

**Descricao:** Criar caso de uso `SincronizarEmpresa` com certificado, cursor, cliente, parser e escritor.

**Aceite:**
- Fluxo completo executa com fake HTTP e filesystem temporario.
- Progresso emite CNPJ, NSU, documentos gravados e erro.
- Cancelamento deixa o estado no ultimo NSU confirmado.

**Dependencias:** HTTP-004, SYNC-003, SYNC-005.

### SYNC-007 - Implementar orquestracao da carteira

**Descricao:** Processar empresas selecionadas de forma serial inicialmente.

**Aceite:**
- Falha de uma empresa nao apaga o resultado das anteriores.
- Resultado final separa sucesso, erro, ignoradas e canceladas.
- Paralelismo fica desabilitado ate task propria de validacao de rate limit.

**Dependencias:** SYNC-006.

**Validacao minima:** simular sucesso, erro de certificado, timeout, cancelamento e falha de filesystem em empresas diferentes. O resultado deve ser individual por empresa.

---

## EPIC 5 - WPF moderno

### UI-001 - Criar shell visual e navegacao

**Descricao:** Criar janela principal WPF com layout moderno, acessivel e responsivo.

**Aceite:**
- MVVM; ViewModel nao conhece controles WPF diretamente.
- Tema, cores e densidade definidos em Resources.
- Estados de carregando, vazio, erro e sucesso existem.
- Interface funciona em resolucao menor sem cortar controles.

**Dependencias:** CORE-002.

### UI-002 - Implementar configuracao de pastas

**Descricao:** Tela para pasta de certificados, destino, ambiente e estrutura Ano/Mes/Tipo.

**Aceite:**
- Validacao ocorre antes do inicio.
- Caminhos invalidos exibem causa e correcao.
- Ultima configuracao nao salva senha.

**Dependencias:** UI-001, CERT-001, CORE-002.

### UI-003 - Implementar grid de empresas

**Descricao:** Grid com selecao, busca, validade, certificado, NSU, situacao e detalhe.

**Aceite:**
- Busca por CNPJ, nome e arquivo.
- Selecao em massa nao bloqueia a UI.
- Erros por empresa ficam acessiveis sem expor segredo.

**Dependencias:** CERT-001, SYNC-001, UI-001.

### UI-004 - Implementar fluxo de senha

**Descricao:** Permitir senha por certificado, com armazenamento apenas em memoria durante o lote.

**Aceite:**
- Campo mascarado e nunca logado.
- Acao "informar senha" atualiza apenas certificados selecionados.
- Erro de senha aparece por empresa.

**Dependencias:** CERT-002, UI-003, CORE-003.

### UI-005 - Implementar comandos do lote

**Descricao:** Comandos Baixar XML, Baixar desde o inicio e Parar.

**Aceite:**
- Botao iniciar bloqueia configuracoes conflitantes.
- Parar usa cancelamento cooperativo.
- Reset de NSU pede confirmacao e registra auditoria local.
- Contadores atualizam sem travar a thread da UI.

**Dependencias:** SYNC-007, UI-003, UI-004.

### UI-006 - Implementar progresso e logs de execucao

**Descricao:** Exibir progresso por empresa e status resumido da execucao.

**Aceite:**
- Usuario sabe qual CNPJ/NSU esta sendo processado.
- Erro oferece detalhe suficiente para suporte.
- Log visual nao exibe XML completo, senha ou chave privada.

**Dependencias:** UI-005, CORE-004.

**Validacao minima:** revisar estados carregando, vazio, erro, cancelado e sucesso em resolucao menor. Verificar que a UI nao exibe segredo nem XML completo.

---

## EPIC 6 - Lacunas, documentos e relatorios

### DOC-001 - Inventariar NSUs gravados

**Descricao:** Persistir metadados necessarios para identificar lacunas sem inferir apenas do nome dos arquivos.

**Aceite:**
- Inventario relaciona CNPJ, NSU, chave, tipo e caminho.
- Duplicatas sao detectadas.
- Regra de lacuna e documentada como heuristica quando a API nao garante continuidade.

**Dependencias:** SYNC-005.

### DOC-002 - Implementar analise de lacunas

**Descricao:** Calcular intervalos candidatos a partir do inventario e estado da API.

**Aceite:**
- Analise diferencia lacuna real de NSU que nao contem documento.
- Resultado e somente leitura.
- UI exibe quantidade e intervalos.

**Dependencias:** DOC-001, SYNC-003.

### DOC-003 - Implementar recuperacao assistida

**Descricao:** Reconsultar NSUs selecionados com limite e confirmacao do usuario.

**Aceite:**
- Recuperacao respeita retry, rate limit e idempotencia.
- Estado so avanca com as mesmas regras do fluxo normal.
- Operacao e cancelavel.

**Dependencias:** DOC-002, HTTP-004, SYNC-006.

### DOC-004 - Exportar Excel/CSV e relatorio

**Descricao:** Exportar resultado da execucao e inventario sem dados sensiveis desnecessarios.

**Aceite:**
- Arquivo inclui filtros, status, CNPJ, NSU, chave e timestamps.
- Exportacao nao bloqueia a UI.
- Falha de permissao de escrita e tratada.

**Dependencias:** SYNC-007, DOC-001.

### DOC-005 - Gerar DANFSe

**Descricao:** Implementar somente depois de confirmar contrato e biblioteca de renderizacao.

**Aceite:**
- Dependencia/licenca documentada.
- XML invalido nao derruba o lote.
- PDF fica associado ao XML e nao substitui o original.

**Dependencias:** SYNC-004, DOC-001.

---

## EPIC 7 - Qualidade, seguranca e distribuicao

### QA-001 - Testes unitarios de dominio e sincronizacao

**Aceite:** Cobertura de cursor NSU, idempotencia, cancelamento, erros e validacoes criticas.

**Dependencias:** SYNC-006.

### QA-002 - Testes de integracao com servidor fake

**Aceite:** Fluxo HTTP completo testa mTLS simulado, retry, 429, 5xx, payload invalido e timeout.

**Dependencias:** API-005, HTTP-004.

### QA-003 - Teste de resiliencia e retomada

**Aceite:** Interrupcao em cada etapa prova que o proximo ciclo nao perde nem confirma NSU indevidamente.

**Dependencias:** SYNC-006, SYNC-007.

### QA-004 - Auditoria de logs e segredos

**Aceite:** Busca automatizada e revisao manual confirmam ausencia de senha, chave privada e XML completo.

**Dependencias:** CORE-003, UI-006.

### QA-005 - Teste de performance controlado

**Aceite:** Mede parsing, escrita, chamadas e memoria separadamente; nao promete throughput acima do limite real da API.

**Dependencias:** HTTP-005, SYNC-007.

### REL-001 - Publicacao Windows

**Descricao:** Definir self-contained/framework-dependent, instalador e atualizacao.

**Aceite:** Instalacao limpa, upgrade, desinstalacao e permissao de pastas testados em Windows 10/11.

**Dependencias:** QA-001, QA-004.

### REL-002 - Checklist de homologacao

**Aceite:** Checklist inclui certificado, ambiente, permissao, pasta destino, lote pequeno, cancelamento, retomada e exportacao.

**Dependencias:** QA-003, QA-005, REL-001.

---

## Ordem sugerida do MVP

1. `CORE-001`, `CORE-002`, `CORE-004`
2. `API-001` a `API-005`
3. `CERT-001` a `CERT-004`
4. `HTTP-001` a `HTTP-005`
5. `SYNC-001` a `SYNC-007`
6. `UI-001` a `UI-006`
7. `QA-001` a `QA-004`
8. `DOC-001` a `DOC-004`
9. `DOC-005`, `QA-005`, `REL-001`, `REL-002`

## Fora do MVP

- Paralelismo entre CNPJs: somente apos medir limites da API e concluir `QA-005`.
- Certificado A3.
- Windows Service/agendamento.
- Emissao, cancelamento ou substituicao de NFS-e.
- API REST local.

## Decisoes que precisam ser fechadas antes do codigo de producao

1. Contrato exato da distribuicao DFe e semantica de `NSU`, `ultNSU` e `maxNSU`.
2. Formato oficial do XML compactado e tratamento de cada tipo de documento.
3. SQLite ou JSON atomico para o estado; SQLite e preferivel se houver inventario de documentos/lacunas.
4. Regra de associacao de certificado com CNPJ.
5. Politica de senha: somente durante a execucao ou persistencia via DPAPI/Credential Manager.
6. Estrutura final de pastas e regra para nome/conflito de arquivo.
7. Biblioteca e escopo do DANFSe.
8. Estrategia de distribuicao: instalador MSI, MSIX ou alternativa corporativa.

## Riscos tecnicos adicionais

| Risco | Controle obrigatorio |
|---|---|
| API retorna lote sem progresso | Circuit breaker de paginacao e erro explicito |
| Arquivo existe com conteudo diferente | Nao sobrescrever; gerar conflito auditavel |
| Certificado correto para CNPJ incorreto | Validacao de vinculo antes do request |
| Estado corrompido | Backup, schema versionado e recuperacao manual |
| Cancelamento durante escrita | Temporario descartavel e NSU nao confirmado |
| Resposta contem dados sensiveis | Sanitizacao centralizada antes do log |
