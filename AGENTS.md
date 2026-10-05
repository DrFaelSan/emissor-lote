# AGENTS.md

## Escopo

Estas regras valem para todo o repositorio NEO-e e para qualquer agente ou pessoa que altere o projeto.

## Regras obrigatorias

- Nao usar emojis em codigo, nomes de arquivos, mensagens de log, textos de interface, commits ou documentacao tecnica.
- Nao adicionar comentarios dentro do codigo. O codigo deve ser autoexplicativo por nomes, tipos e funcoes pequenas.
- Nao adicionar cabecalhos de copyright ou licenca sem solicitacao explicita.
- Usar ASCII por padrao em arquivos novos e alterados.
- Nao gravar senha, chave privada, token, XML completo ou segredo em log, excecao, fixture ou relatorio.
- Nao desabilitar validacao TLS, revogacao ou verificacao de certificado para fazer uma integracao funcionar.
- Nao chamar ambiente de producao durante desenvolvimento sem aprovacao explicita e evidencia registrada.
- Nao usar dados fiscais reais em testes, fixtures ou exemplos versionados.
- Nao alterar RFCs por conveniencia de implementacao; registrar divergencias em uma decisao tecnica.
- Nao fazer manifestacao conclusiva automaticamente.

## C# e .NET

- Target framework: .NET 8.
- Habilitar nullable reference types e implicit usings quando a solution for criada.
- Tratar warnings como erros no CI depois que o baseline inicial estiver limpo.
- Preferir tipos fortes a strings livres para status, ambiente, tipo de documento e evento.
- Usar `DateTimeOffset` para instantes e `decimal` para valores monetarios.
- Propagar `CancellationToken` em toda operacao assincrona de rede, lote, filesystem e importacao.
- Nao usar `async void`, exceto handlers de eventos WPF quando inevitavel.
- Nao criar `HttpClient` por request. O ciclo de vida deve ser controlado por empresa/certificado.
- Nao usar `double` ou `float` para valores fiscais.
- Validar entrada na fronteira: arquivo, XML, JSON, CNPJ, chave, data e valor.
- Manter DTOs externos separados das entidades de dominio.

## WPF

- Usar MVVM.
- ViewModel nao deve acessar controle visual diretamente.
- Operacoes longas nao podem bloquear a thread da interface.
- Estados de carregamento, vazio, erro, cancelado e sucesso devem ser explicitos.
- Acoes conclusivas devem ter confirmacao em duas etapas.
- A UI deve exibir o ambiente ativo e diferenciar homologacao de producao.
- Nao usar texto decorativo ou emojis para substituir estado tecnico.

## API, certificados e seguranca

- Validar o contrato atual da API antes de criar ou alterar DTOs.
- Versionar fixtures sanitizadas e registrar a fonte do contrato.
- Retry somente para erros transitorios explicitamente classificados.
- Respeitar `Retry-After`, timeout e cancelamento.
- Cada CNPJ deve usar o certificado correspondente.
- Certificado sem chave privada, expirado ou com vinculo divergente deve bloquear a operacao daquela empresa.
- Persistir NSU somente depois da gravacao bem-sucedida dos documentos.
- Eventos rejeitados nao podem ser exibidos como sucesso.

## Testes

- Toda regra de dominio nova deve possuir teste unitario.
- Toda integracao externa deve possuir fixture ou handler falso.
- Casos de falha sao obrigatorios: timeout, cancelamento, resposta invalida, duplicidade, arquivo sem permissao e certificado incorreto.
- Testes de producao restrita sao explicitamente marcados e nunca rodam como parte do teste padrao.
- Nao reduzir cobertura ou remover teste para fazer o build passar.

## Tasks e commits

- Uma task deve ter objetivo, dependencias, criterio de aceite e validacao.
- Manter o escopo de um commit pequeno e coerente.
- Nao misturar refatoracao ampla com mudanca funcional.
- Atualizar backlog, README ou decisao tecnica quando o comportamento implementado mudar.
- Nao marcar uma task como concluida sem evidencia de validacao.

## Revisao antes de concluir

- Verificar diff e arquivos alterados.
- Executar build e testes disponiveis.
- Procurar segredos, emojis e comentarios novos no codigo.
- Confirmar que logs e mensagens de erro estao sanitizados.
- Confirmar que a mudanca nao habilita producao por padrao.
