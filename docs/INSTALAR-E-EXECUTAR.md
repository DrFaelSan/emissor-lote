# Instalar e executar o NEO-e

Guia de instalacao, execucao em Debug, execucao em Release e distribuicao da aplicacao desktop Windows.

Conteudos relacionados:

- [README.md](../README.md): visao geral do projeto.
- [codebase/STACK.md](codebase/STACK.md): stack e dependencias.
- [codebase/TESTING.md](codebase/TESTING.md): estrutura de testes.
- [ESTADO-ATUAL-E-PROXIMO-EPIC.md](ESTADO-ATUAL-E-PROXIMO-EPIC.md): status e evidencia de validacao.

## 1. Pre-requisitos

| Item | Versao / detalhe |
|------|------------------|
| Sistema operacional | Windows 10 ou 11 x64 (o projeto usa WPF, `net8.0-windows`) |
| .NET SDK | 8.0 ou superior (SDK 9/10 tambem compila `net8.0-windows`) |
| Git | Qualquer versao atual |
| Disco | Permissao de escrita em `C:\Certificados` e `C:\NotasFiscais` (padrao do `appsettings.json`) |
| Certificado A1 | Arquivo `.pfx`/`.p12` com chave privada, fora do repositorio |

Observacoes:

- `*.pfx`, `*.p12` e `*.cer` estao no `.gitignore`. O certificado nunca deve entrar no repositorio.
- Verificar o SDK instalado:

```powershell
dotnet --version
dotnet --list-sdks
```

- Se o SDK 8 nao estiver instalado, instale em https://dotnet.microsoft.com/download/dotnet/8.0 ou use um SDK mais recente.

## 2. Primeira execucao

```powershell
git clone <url-do-repositorio>
cd emissor-lote

dotnet restore NEO-e.slnx
dotnet build NEO-e.slnx
dotnet test NEO-e.slnx
```

Evidencia de referencia (validado em 2026-10-07 nesta maquina):

- `dotnet restore NEO-e.slnx`: sucesso.
- `dotnet build NEO-e.slnx --no-restore`: sucesso, 0 warnings, 0 erros.
- `dotnet test NEO-e.slnx`: 16 testes aprovados (14 unit + 1 integration + 1 homologacao), 0 falhas.

## 3. Configuracao

O arquivo de configuracao e `src/NEO-e.App/appsettings.json`. Ele e copiado para a saida do build com `PreserveNewest` e carregado de `AppContext.BaseDirectory` com `optional: false`: se o arquivo faltar, a aplicacao falha na inicializacao.

Edite sempre o arquivo-fonte em `src/NEO-e.App/appsettings.json`. Depois do build, o arquivo atualizado ja aparece na pasta de saida.

Chave principais:

| Chave | Padrao | Descricao |
|-------|--------|-----------|
| `App.Certificates.FolderPath` | `C:\Certificados` | Pasta descoberta em busca de `.pfx`/`.p12` |
| `App.Storage.DestinationPath` | `C:\NotasFiscais` | Destino dos XMLs gravados |
| `App.Adn.BaseUrlRestrita` | `https://adn.producaorestrita.nfse.gov.br/` | Endpoint de homologacao |
| `App.Adn.BaseUrlProducao` | `https://adn.nfse.gov.br/` | Endpoint de producao |
| `App.Environment.Active` | `Restrita` | Ambiente ativo. Producao nao pode ser o padrao |
| `App.Environment.RequireExplicitConfirmationForProducao` | `true` | Exige confirmacao explicita antes de operacoes em producao |
| `App.Logging.LogFilePath` | `logs/neo-e-.log` | Arquivo de log, relativo a pasta da aplicacao |

A UI exibe o ambiente ativo e diferencia homologacao de producao. A troca para Producao exige confirmacao em duas etapas.

Logs:

- Debug: `src/NEO-e.App/bin/Debug/net8.0-windows/logs/neo-e-.log`
- Release: `src/NEO-e.App/bin/Release/net8.0-windows/logs/neo-e-.log`
- Publish: `src/NEO-e.App/bin/Release/net8.0-windows/win-x64/publish/logs/neo-e-.log`

Erros de inicializacao aparecem em uma caixa de mensagem (`App.xaml.cs`, `ShowStartupError`) com mensagem, inner exception e stack trace.

## 4. Executar em Debug

### Linha de comando

```powershell
dotnet run --project src/NEO-e.App/NEO-e.App.csproj
```

`Debug` e a configuracao padrao. Equivalente explicito:

```powershell
dotnet run --project src/NEO-e.App/NEO-e.App.csproj -c Debug
```

### Visual Studio / VS Code

1. Abrir `NEO-e.slnx` na raiz do repositorio.
2. Definir `NEO-e.App` como projeto de inicializacao.
3. Pressionar F5 (Debug) ou Ctrl+F5 (executar sem debug).

### Onde ficam os artefatos

```text
src/NEO-e.App/bin/Debug/net8.0-windows/
  NEO-e.App.exe            aplicacao
  appsettings.json         configuracao copiada do fonte
  logs/neo-e-.log          log estruturado (Serilog)
  *.dll, *.pdb             bibliotecas e simbolos
```

## 5. Executar em Release

### Build e execucao local

```powershell
dotnet build NEO-e.slnx -c Release
dotnet run --project src/NEO-e.App/NEO-e.App.csproj -c Release
```

Saida: `src/NEO-e.App/bin/Release/net8.0-windows/`.

### Publicacao self-contained (recomendado para distribuicao)

Gera uma pasta autocontida: a maquina de destino nao precisa ter .NET instalado.

```powershell
dotnet publish src/NEO-e.App/NEO-e.App.csproj -c Release -r win-x64 --self-contained true
```

Saida: `src/NEO-e.App/bin/Release/net8.0-windows/win-x64/publish/`.

### Publicacao framework-dependent (mais leve)

Requer o .NET 8 Runtime x64 na maquina de destino.

```powershell
dotnet publish src/NEO-e.App/NEO-e.App.csproj -c Release -r win-x64 --self-contained false
```

Comparacao:

| Modalidade | Tamanho | Requisito na maquina alvo |
|------------|---------|---------------------------|
| `--self-contained true` | Maior | Nenhum (runtime incluido) |
| `--self-contained false` | Menor | .NET 8 Runtime x64 |

### Copiar para outra maquina

1. Copiar o conteudo da pasta `publish/` inteira.
2. Levar junto o `appsettings.json` da pasta de publicacao (ele ja e gerado, mas confira os caminhos).
3. Ajustar `App.Certificates.FolderPath` e `App.Storage.DestinationPath` para as pastas existentes na maquina de destino.
4. Copiar o certificado `.pfx` para a pasta de certificados da maquina de destino (nunca junto do executavel em um compartilhamento).
5. Executar `NEO-e.App.exe`.

### Limpar artefatos anteriores

```powershell
dotnet clean NEO-e.slnx
```

## 6. Testes

```powershell
dotnet test NEO-e.slnx
dotnet test NEO-e.slnx --no-restore
dotnet test tests/NEO-e.UnitTests/NEO-e.UnitTests.csproj
```

Projetos de teste:

| Projeto | Escopo |
|---------|--------|
| `NEO-e.UnitTests` | Regras de dominio, value objects, use cases |
| `NEO-e.IntegrationTests` | Infraestrutura com `FakeAdnHandler`, SQLite em memoria, filesystem temporario |
| `NEO-e.HomologacaoTests` | Smoke test controlado em homologacao |

Regra (AGENTS.md): testes de producao restrita sao explicitamente marcados e nunca rodam como parte do teste padrao. Nao executar `NEO-e.HomologacaoTests` com certificado real ou ambiente de producao sem aprovacao explicita e evidencia registrada.

## 7. Solucao de problemas

| Sintoma | Causa provavel | Acao |
|---------|----------------|------|
| `NETSDK1045` ou erro de target framework | SDK nao suporta `net8.0-windows` | Instalar .NET 8 SDK ou SDK mais recente |
| Erro na inicializacao citando `appsettings.json` | Arquivo ausente na pasta do executavel | Rodar `dotnet build` ou copiar o `appsettings.json` junto do `.exe` |
| Erro de permissao ao gravar XML | Pasta de destino sem permissao | Conceder escrita em `App.Storage.DestinationPath` ou mudar a chave |
| Certificado nao encontrado | Pasta errada ou extensao fora de `.pfx`/`.p12` | Conferir `App.Certificates.FolderPath` e `AutoDiscover` |
| Certificado sem chave privada, expirado ou vinculo divergente | Bloqueio intencional | Trocar o certificado; a operacao daquela empresa fica bloqueada |
| Aplicacao nao abre fora do Windows | WPF e Windows-only | Executar em Windows 10/11 x64 |
| `dotnet run` falha apos editar `appsettings.json` | Erro de validacao no startup | Ler a caixa de mensagem de erro e corrigir a chave citada |
| Producao aparece como ativa | Configuracao alterada manualmente | Voltar para `Restrita`; producao nunca pode ser o padrao |

## 8. Referencia de comandos

```powershell
# Restaurar e compilar
dotnet restore NEO-e.slnx
dotnet build NEO-e.slnx

# Testar
dotnet test NEO-e.slnx

# Executar em Debug
dotnet run --project src/NEO-e.App/NEO-e.App.csproj

# Executar em Release
dotnet run --project src/NEO-e.App/NEO-e.App.csproj -c Release

# Publicar self-contained (win-x64)
dotnet publish src/NEO-e.App/NEO-e.App.csproj -c Release -r win-x64 --self-contained true

# Publicar framework-dependent (win-x64)
dotnet publish src/NEO-e.App/NEO-e.App.csproj -c Release -r win-x64 --self-contained false

# Limpar
dotnet clean NEO-e.slnx
```
