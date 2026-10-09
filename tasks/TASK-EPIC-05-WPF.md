# EPIC 5 - WPF moderno

Referencia: [TASKS-001-Implementacao-NFSe-WPF.md](../TASKS-001-Implementacao-NFSe-WPF.md)

## UI-001 - Shell e navegacao

Criar janela principal WPF com MVVM, tema em Resources, estados de carregamento/vazio/erro/sucesso e layout funcional em resolucoes menores.

## UI-002 - Configuracao de pastas

Permitir configurar pasta de certificados, destino, ambiente e estrutura Ano/Mes/Tipo. Validar antes de iniciar e nunca salvar senha junto da configuracao.

## UI-003 - Grid de empresas

Exibir selecao, busca, validade, certificado, NSU, situacao e detalhe. Busca por CNPJ/nome/arquivo e selecao em massa nao podem bloquear a UI.

## UI-004 - Fluxo de senha

Permitir senha por certificado, com campo mascarado e memoria apenas durante o lote. Erros devem aparecer por empresa.

## UI-005 - Comandos do lote

Implementar Baixar XML, Baixar desde o inicio e Parar. Usar cancelamento cooperativo, confirmacao para reset e atualizar contadores sem travar a thread da UI.

## UI-006 - Progresso e logs

Exibir CNPJ/NSU atual, contadores e erros acionaveis. O log visual nao pode mostrar XML completo, senha, token ou chave privada.

## UI-007 - Splash screen e Dynamic Island

Exibir splash screen durante o boot com tempo minimo de exibicao e extrair o painel de status para a `DynamicIslandControl` com transicoes entre oculto, ponto de repouso e expandido. Notificar fim de sincronizacao, simulacao, falha e troca de ambiente via `IIslandNotifier`, sem o ViewModel acessar controles. Referencia: [RFC-004-UX-UI-Splash-Dynamic-Island.md](RFC-004-UX-UI-Splash-Dynamic-Island.md).

## Pronto quando

- O fluxo principal pode ser executado sem acessar controles diretamente pelo ViewModel.
- O usuario entende o estado de cada empresa.
- Iniciar, cancelar e resetar possuem estados e confirmacoes corretos.
- A splash cobre o boot sem encerrar a aplicacao e a ilha anima hover e notificacoes sem corrida de transicoes.
