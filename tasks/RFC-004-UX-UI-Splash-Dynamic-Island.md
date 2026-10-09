# RFC-004 - UX/UI: Splash Screen e Dynamic Island

## Status
Proposta de implementacao

## Data
2026-10-09

## Objetivo
Definir a experiencia de inicializacao e o painel de status flutuante do aplicativo NEO-e, com uma splash screen exibida durante o boot e uma Dynamic Island animada no estilo iOS que concentra ambiente, contadores, progresso, acoes e notificacoes no topo da janela.

## Escopo
- Exibir splash screen durante a criacao do host e validacao de DI, com tempo minimo de exibicao
- Extrair a barra de status flotante existente da MainWindow para um controle reutilizavel
- Animar a transicao entre estado oculto, ponto de repouso (40x40) e estado expandido
- Expandir a ilha no hover do mouse e ao receber notificacoes
- Notificar fim de sincronizacao, resultado de simulacao, falha de operacao e troca de ambiente
- Manter o MVVM: animacao vive no code-behind do controle, ViewModel emite apenas eventos de notificacao

## Contexto
A janela principal ja possuia um painel de status embutido em `MainWindow.xaml` com ambiente, contadores, progresso e botoes, porem sem animacao, sem estados de repouso e sem mecanismo de notificacao. A inicializacao abria direto a MainWindow apos o build do host. O objetivo e transformar esse painel em uma Dynamic Island com transicoes animadas e adicionar uma splash screen para o boot, sem que ViewModel acesse controles visuais.

## Requisitos
1. A splash screen deve ser exibida antes do build do host e permanecer por no minimo 800 ms.
2. O fechamento da splash nao pode encerrar a aplicacao (a MainWindow deve assumir como janela principal antes do fechamento).
3. Falhas de startup devem fechar a splash antes de exibir a mensagem de erro.
4. A ilha deve iniciar como ponto de 40x40 acima da regiao de titulo e deslizar para o lugar apos o carregamento.
5. A ilha deve expandir no hover e recolher ao remover o mouse, com transicoes seriais (sem corrida de animacoes).
6. Notificacoes devem expandir a ilha, exibir chip com icone e mensagem por 5 s e recolher depois, salvo hover ativo.
7. O estado colapsado nao pode receber cliques; o conteudo expandido deve ser acessivel a mouse e teclado.
8. O ViewModel deve emitir notificacoes apenas via `IIslandNotifier`, nunca acessando o controle.
9. Nao gravar segredos, XML completo ou dados fiscais reais em mensagens de notificacao.

## Decisoes de Implementacao
### 1. Splash screen
- `SplashScreenWindow` (sem borda, 420x260) exibida em `App.OnStartup` com `ShutdownMode.OnExplicitShutdown`.
- Host criado em `Task.Run` para nao travar a UI da splash; tempo restante ate 800 ms aplicado apos `StartAsync`.
- Resolucao da MainWindow, atribuicao de `MainWindow`, troca para `OnMainWindowClose` e fechamento da splash ocorrem nessa ordem.

### 2. Dynamic Island
- Controle `DynamicIslandControl` (UserControl) extraido da `MainWindow`, com estilos de chip de ambiente movidos para os recursos do proprio controle.
- Estados: oculto (Y=-80, sem hit test), colapsado (ponto 40x40, CornerRadius 20) e expandido (largura/altura auto, CornerRadius 32, conteudo visivel).
- Transicoes serializadas por uma tarefa encadeada (`_activeTransition`); cada storyboard chama `Stop()` no `Completed` para liberar o clock e grava o valor final antes de concluir.
- Largura expandida calculada por medicao direta do conteudo; ao recolher, `Width`/`Height` voltam a valores fixos de 40.

### 3. Notificacoes
- `IIslandNotifier` + `IslandNotificationService` registrados como singleton no DI.
- Chip de notificacao com icones Segoe Fluent Icons por tipo (Info, Success, Error).
- Hold de 5 s cancelavel: nova notificacao substitui a anterior; hover ativo adia o recolhimento ate o `MouseLeave`.
- Gatilhos: conclusao de sincronizacao (sucesso/falha), resultado de simulacao, excecao de operacao e troca de ambiente.

## Criterios de Aceitacao
- O aplicativo exibe a splash no boot e abre a MainWindow sem piscar ou encerrar.
- A ilha comeca oculta, desliza para o topo e aparece como ponto de 40x40.
- Hover expande a ilha; saida do mouse recolhe para o ponto.
- Notificacoes expandem a ilha, mostram o chip por 5 s e recolhem.
- Troca de ambiente e fim de sincronizacao geram notificacao na ilha.
- ViewModel nao referencia tipos de `System.Windows` da animacao.

## Validacao
- dotnet build "NEO-e.slnx" -nologo
- dotnet test tests/NEO-e.UnitTests/NEO-e.UnitTests.csproj -nologo --no-restore
- dotnet test tests/NEO-e.IntegrationTests/NEO-e.IntegrationTests.csproj -nologo --no-restore

## Entregaveis
- `src/NEO-e.App/SplashScreenWindow.xaml` e `SplashScreenWindow.xaml.cs`
- `src/NEO-e.App/Controls/DynamicIslandControl.xaml` e `DynamicIslandControl.xaml.cs`
- `src/NEO-e.App/Services/IslandNotification.cs`, `IIslandNotifier.cs`, `IslandNotificationService.cs`
- Ajustes em `App.xaml.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs` e `MainWindowViewModel.cs`
- Documento de referencia RFC em `tasks/RFC-004-UX-UI-Splash-Dynamic-Island.md`

## Observacoes
A splash e puramente visual: nao possui logica de negocio. O tempo minimo de 800 ms evita flash em boots rapidos; boots lentos mantem a splash ate a MainWindow estar pronta. As animacoes rodam no code-behind do controle por dependerem de medicao de tamanho em tempo de execucao, o que e aceitavel dentro do escopo de UI do MVVM.
