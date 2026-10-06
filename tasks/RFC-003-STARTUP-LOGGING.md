# RFC-003 - Inicializacao e Logging do Sistema

## Status
Proposta de implementacao

## Data
2026-10-05

## Objetivo
Definir a padronizacao de boot, diagnostico e logs de inicializacao e encerramento do aplicativo NEO-e, garantindo observabilidade em ambiente desktop WPF e validacao do container de dependencia antes da exibicao da MainWindow.

## Escopo
- Registrar logs estruturados de inicializacao do sistema
- Registrar logs estruturados de encerramento do sistema
- Validar configuracao e infraestrutura antes da UI ficar disponivel
- Padronizar a captura de excecoes de startup para que falhas nao passem silenciosas
- Integrar os padroes uteis observados nos exemplos externos sem migrar codigo experimental para o produto

## Contexto
O projeto possui uma camada de infraestrutura com DI e Serilog configurada, mas a aplicacao WPF ainda precisava deixar a inicializacao mais observavel. Os exemplos externos forneciam padroes uteis de validacao de certificados e infraestrutura, mas nao devem ser copiados como implementacao final sem adaptacao ao produto.

## Requisitos
1. O aplicativo deve registrar o inicio do processo com contexto de ambiente e arquivo de configuracao.
2. O aplicativo deve registrar o momento em que a MainWindow e exibida.
3. O aplicativo deve registrar o encerramento do sistema ao fechar.
4. Falhas de inicializacao devem ser exibidas via MessageBox em vez de encerrar silenciosamente.
5. O processo de validacao de configuracao deve ocorrer antes da exibicao da interface para reduzir erros no runtime.
6. O projeto deve manter as referencias externas como material de consulta e nao como dependencia direta de producao.

## Decisoes de Implementacao
### 1. Boot de aplicacao
- O host e criado no OnStartup da aplicacao WPF.
- A configuracao e DI sao montadas antes da exibicao da janela principal.
- O logger da aplicacao registra eventos de startup e encerramento.

### 2. Logging
- Reaproveitar a interface `NEO_e.Application.Contracts.ILogger`.
- Resolver o logger do DI apos a criacao do host.
- Registrar mensagens de:
  - inicializacao
  - configuracao carregada
  - exibicao da MainWindow
  - encerramento do sistema

### 3. Tratamento de falhas
- Excecoes do AppDomain, Dispatcher e TaskScheduler sao capturadas e apresentadas em UI.
- Falhas de bootstrap nao permanecem silenciosas para o usuario.

## Critérios de Aceitacao
- O processo de inicializacao registra logs em arquivo.
- O fechamento do sistema registra logs em arquivo.
- A aplicacao continua compilando sem erros.
- A validacao de configuracao do host permanece ativa.
- O usuario recebe feedback visual em caso de falha de startup.

## Validacao
- dotnet build "NEO-e.slnx" -nologo
- dotnet test tests/NEO-e.UnitTests/NEO-e.UnitTests.csproj -nologo --no-restore

## Entregaveis
- Ajustes em `src/NEO-e.App/App.xaml.cs`
- Ajustes no registro do logger da infraestrutura
- Documento de referencia RFC em `tasks/RFC-003-STARTUP-LOGGING.md`

## Observacoes
Os exemplos externos em `exemplos/` devem continuar sendo referencia, mas sua copia para o codigo de producao deve ser evitada. O projeto atual deve consumir apenas os padroes adequados: validacao de certificados, DI e logs estruturados.
