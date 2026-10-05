# EPIC 1 - Solucao, configuracao e seguranca

Referencia: [TASKS-001-Implementacao-NFSe-WPF.md](../TASKS-001-Implementacao-NFSe-WPF.md)

## CORE-001 - Criar solution e projetos

Criar solution .NET 8 com `App`, `Application`, `Domain`, `Infrastructure`, `Contracts`, testes unitarios e testes de integracao. `dotnet build` e `dotnet test` devem funcionar em uma maquina limpa.

## CORE-002 - Configurar DI, Options e ambientes

Configurar DI, Options, logging e ambientes `Restrita`/`Producao`. A URL nao pode ficar no ViewModel. Producao exige confirmacao explicita e configuracao invalida impede o inicio.

## CORE-003 - Definir politica de segredos

Escolher e documentar DPAPI ou Windows Credential Manager. Senha nunca vai para JSON ou log e deve ser descartada ao final/cancelamento. Adicionar teste de ausencia de segredo nos logs.

## CORE-004 - Definir modelo de erro e resultado

Padronizar erros de validacao, certificado, rede, API, parsing e filesystem. Todo erro deve preservar categoria, CNPJ, NSU quando aplicavel e acao sugerida. Retry apenas para erros retryable.

## Pronto quando

- A solution compila e testa.
- O ambiente ativo e a politica de segredos estao definidos.
- O restante do codigo pode depender de contratos comuns de configuracao e erro.
