# EPIC 05 - Interface WPF

## 002-UI-001 - Shell do modulo

Criar navegacao WPF/MVVM para Empresas, Notas Recebidas, Cruzamento, Manifestacao e Auditoria.

**Aceite:** modulo usa recursos visuais comuns do RFC-001 e nao mistura estado de ViewModel com controles.

## 002-UI-002 - Grid de notas recebidas

Exibir chave, emitente, data, valor, tipo, status, prazo, cruzamento e situacao.

**Aceite:** filtros por empresa, periodo, tipo, status, prazo e divergencia permanecem responsivos com 10.000 notas.

## 002-UI-003 - Detalhe da nota

Exibir XML/metadados, eventos, referencia encontrada, delta e historico de auditoria sem expor chave privada.

**Aceite:** XML e detalhes possuem acesso somente leitura e tratamento para arquivo ausente.

## 002-UI-004 - Fluxo de decisao

Permitir selecionar evento, visualizar impacto, informar justificativa e revisar resumo antes de enviar.

**Aceite:** Ciencia pode ter fluxo proprio; eventos conclusivos exigem confirmacao explicita em duas etapas.

## 002-UI-005 - Execucao em lote

Processar notas selecionadas com progresso individual e cancelamento cooperativo.

**Aceite:** sucesso, rejeicao, erro tecnico e ignorada aparecem separadamente.

## 002-UI-006 - Alertas de prazo e divergencia

Destacar prazo proximo/expirado e divergencias sem sugerir confirmacao automatica.

**Aceite:** regras de destaque sao configuraveis e acompanhadas da fonte do prazo.
