# RFC-002: Manifestação de Notas Recebidas + Cruzamento de Valores

| Campo | Valor |
|---|---|
| **Status** | Draft |
| **Autor** | [Seu nome] |
| **Data** | 2025-01 |
| **Revisores** | [Nomes] |
| **Versão do documento** | 0.1 |
| **Relacionada a** | RFC-001 (Download em Massa de NFS-e) |

---

## 1. Resumo Executivo

Este documento propõe a especificação técnica de um **módulo / aplicativo desktop** (WinForms / .NET 8) capaz de:

1. **Listar e gerenciar notas fiscais emitidas contra o CNPJ** do certificado digital (notas **recebidas** — NFS-e e/ou NF-e).
2. Permitir ao usuário **decidir e registrar a manifestação** (“assinar ou não”): Ciência, Confirmação, Desconhecimento ou Operação não Realizada (padrão NF-e) e eventos equivalentes de confirmação do tomador no padrão nacional NFS-e.
3. **Cruzar valores** das notas com bases de referência (contratos, lançamentos contábeis, planilhas de conferência ou totais esperados) para detectar divergências de forma assistida.

O objetivo é eliminar o trabalho manual e o risco fiscal que escritórios contábeis e departamentos jurídicos enfrentam ao receber notas emitidas por terceiros contra seus CNPJs: falta de visibilidade, decisão tardia de manifestação e ausência de conferência sistemática de valores.

Este módulo complementa o **RFC-001** (download em massa de XMLs de NFS-e), reutilizando a mesma base de certificados A1, a mesma carteira de empresas e a mesma estrutura de pastas de XMLs.

---

## 2. Contexto e Motivação

### 2.1 Cenário Atual (As-Is)

- Notas emitidas contra o CNPJ da empresa chegam de forma dispersa (e-mail, portal, download manual ou via RFC-001).
- A decisão de **manifestar** (confirmar, desconhecer, ciência etc.) é feita de forma isolada, muitas vezes só quando há cobrança ou fiscalização.
- Não há ferramenta unificada que mostre, por empresa e por período:
  - Quais notas foram recebidas
  - Quais já foram manifestadas
  - Quais ainda estão pendentes de decisão
  - Quais apresentam divergência de valor em relação ao esperado
- O cruzamento de valores (nota × contrato × contabilidade) é feito em planilhas, com alto risco de erro e retrabalho.

### 2.2 Problemas Identificados

| # | Problema | Impacto |
|---|---|---|
| P1 | Notas recebidas sem visibilidade centralizada | Passivo fiscal oculto |
| P2 | Manifestação feita tarde ou de forma inconsistente | Risco de confirmação automática / perda de prazo |
| P3 | Decisão de “assinar ou não” sem contexto de valor | Aceite de notas indevidas ou com valor errado |
| P4 | Cruzamento de valores manual (Excel) | Erro humano, tempo perdido, retrabalho |
| P5 | Múltiplos CNPJs / filiais sem visão consolidada | Processo não escala |
| P6 | Falta de trilha de auditoria da decisão | Dificuldade de defesa em fiscalização |

### 2.3 Objetivos

| ID | Objetivo |
|---|---|
| O1 | Centralizar a visão de todas as notas emitidas contra cada CNPJ da carteira |
| O2 | Permitir decisão assistida de manifestação (Ciência / Confirmação / Desconhecimento / Operação não Realizada) |
| O3 | Registrar a manifestação via API oficial (SEFAZ / ADN) com o certificado A1 correto |
| O4 | Cruzar valores das notas com bases de referência e destacar divergências |
| O5 | Gerar relatório de pendências, manifestações e divergências por empresa/período |
| O6 | Reutilizar certificados, carteira e pastas do RFC-001 |

### 2.4 Não-Objetivos (Out of Scope na v1)

- Emissão de notas.
- Cancelamento de notas emitidas pela própria empresa.
- Contabilização automática no ERP.
- Integração bidirecional com sistemas contábeis (apenas importação de planilha/CSV na v1).
- Suporte a CT-e (conhecimento de transporte) na primeira versão.
- Interface web / SaaS multiusuário.

---

## 3. Proposta Técnica

### 3.1 Arquitetura Macro

```
┌─────────────────────────────────────────────────────────────────────┐
│              Módulo / Aplicativo Desktop (WinForms / .NET 8)        │
│                                                                     │
│  ┌──────────────┐   ┌────────────────┐   ┌────────────────────┐    │
│  │  UI WinForms │──▶│  Orquestrador  │──▶│  Motor de          │    │
│  │  (grid +     │   │  de Carteira   │   │  Manifestação      │    │
│  │   filtros)   │   └────────────────┘   └─────────┬──────────┘    │
│  └──────────────┘                                   │               │
│         │                                           │               │
│         ▼                                           ▼               │
│  ┌──────────────┐   ┌────────────────┐   ┌────────────────────┐    │
│  │  Repositório │   │  Certificados  │   │  Cliente HTTP      │    │
│  │  de Notas +  │◀──│  A1 (mesmo do  │──▶│  mTLS (ADN /       │    │
│  │  Estado de   │   │  RFC-001)      │   │  SEFAZ NF-e)       │    │
│  │  Manifestação│   └────────────────┘   └────────────────────┘    │
│  └──────────────┘                                                   │
│         │                                                           │
│         ▼                                                           │
│  ┌──────────────┐   ┌────────────────┐                               │
│  │  Motor de    │   │  Importador de │                               │
│  │  Cruzamento  │◀──│  Valores       │                               │
│  │  de Valores  │   │  (CSV/Excel)   │                               │
│  └──────────────┘   └────────────────┘                               │
└─────────────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
     ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
     │ API ADN     │  │ SEFAZ NF-e  │  │ XMLs locais │
     │ (NFS-e)     │  │ (eventos de │  │ (RFC-001)   │
     │             │  │ manifestação)│  │             │
     └─────────────┘  └─────────────┘  └─────────────┘
```

### 3.2 Stack Tecnológico

| Camada | Tecnologia | Justificativa |
|---|---|---|
| Runtime | **.NET 8** | Mesma base do RFC-001 |
| UI | **WinForms** | Consistência com o aplicativo de download em lote |
| HTTP / mTLS | `HttpClient` + `SocketsHttpHandler` | Certificado A1 por empresa |
| Certificados | `X509Certificate2` (pasta `.pfx` ou store) | Reutilização total do RFC-001 |
| Persistência | SQLite | Estado de notas, manifestações e resultados de cruzamento |
| Importação de valores | ClosedXML / EPPlus ou CSV nativo | Cruzamento com planilhas de referência |
| Logging | Serilog | Trilha de auditoria das decisões |

### 3.3 Conceitos de Manifestação

#### 3.3.1 NF-e (modelo 55) — Eventos oficiais

| Código | Evento | Significado | Conclusivo? |
|---|---|---|---|
| 210210 | **Ciência da Operação** | Tomou conhecimento da nota, mas ainda não tem elementos para decisão final | Não |
| 210200 | **Confirmação da Operação** | Confirma que a operação ocorreu conforme a nota | Sim |
| 210220 | **Desconhecimento da Operação** | Não reconhece a operação / CNPJ usado indevidamente | Sim |
| 210240 | **Operação não Realizada** | Reconhece a participação, mas a operação não se efetivou | Sim |

**Prazos típicos (sujeitos a atualização por NT):**
- Ciência da Operação: ~10 dias da autorização
- Manifestações conclusivas: 90 dias (conforme NT mais recente) ou 180 dias (regras anteriores) — o sistema deve exibir o prazo restante e alertar.

#### 3.3.2 NFS-e Nacional (ADN)

- O tomador (destinatário do serviço) pode registrar eventos de **confirmação / manifestação** vinculados à chave de acesso da NFS-e.
- O módulo deve consultar eventos existentes via `GET /NFSe/{ChaveAcesso}/Eventos` e permitir o registro do evento de confirmação quando disponível na API de eventos do ADN/SEFIN.

### 3.4 Fluxo de Uso Principal

```
INÍCIO
  │
  ├─▶ 1. Carregar carteira + certificados (mesmo mecanismo do RFC-001)
  │
  ├─▶ 2. Sincronizar notas recebidas
  │       ├─▶ A partir dos XMLs já baixados (pasta do RFC-001)
  │       └─▶ E/ou consulta complementar na API (ADN / distribuição destinatário)
  │
  ├─▶ 3. Exibir grid consolidada por empresa / período
  │       Colunas sugeridas:
  │         - Chave de acesso
  │         - Emitente (CNPJ / Nome)
  │         - Data de emissão / autorização
  │         - Valor total
  │         - Tipo (NFS-e / NF-e)
  │         - Status de manifestação atual
  │         - Prazo restante
  │         - Resultado do cruzamento de valor (OK / Divergente / Sem referência)
  │         - Situação (Pendente / Manifestada / Erro)
  │
  ├─▶ 4. Usuário filtra e seleciona notas
  │
  ├─▶ 5. Decisão de manifestação (em lote ou individual)
  │       ├─▶ Ciência da Operação
  │       ├─▶ Confirmação da Operação
  │       ├─▶ Desconhecimento da Operação
  │       └─▶ Operação não Realizada (+ justificativa quando exigida)
  │
  ├─▶ 6. Envio do evento via mTLS (certificado do CNPJ destinatário)
  │       └─▶ Atualização do status + log de auditoria
  │
  ├─▶ 7. Cruzamento de valores (sob demanda ou automático)
  │       ├─▶ Importar planilha/CSV de valores esperados
  │       ├─▶ Comparar por chave, por CNPJ emitente + período, ou por valor ± tolerância
  │       └─▶ Destacar divergências na grid e no relatório
  │
  ├─▶ 8. Gerar relatório / exportar Excel de pendências e divergências
  │
FIM
```

### 3.5 Estratégia de Cruzamento de Valores

O motor de cruzamento deve ser **configurável** e operar em camadas:

| Nível | Critério de match | Uso típico |
|---|---|---|
| 1 | Chave de acesso exata | Quando a planilha de referência já contém a chave |
| 2 | CNPJ emitente + número da nota + série | Conferência com controles internos |
| 3 | CNPJ emitente + data + valor (com tolerância %) | Quando não há chave disponível |
| 4 | CNPJ emitente + período + soma de valores | Conferência de totais mensais |

**Resultados possíveis por nota:**
- **OK** — valor dentro da tolerância
- **Divergente** — diferença acima da tolerância (exibir valor esperado × valor da nota × delta)
- **Sem referência** — nota não encontrada na base de comparação
- **Múltiplos matches** — ambiguidade (usuário decide)

**Entrada de dados de referência (v1):**
- Importação de arquivo Excel/CSV com colunas mínimas: `CNPJ_Emitente`, `Valor`, `Data` (e opcionalmente `Chave`, `Numero`, `Serie`, `Observacao`).
- Futuro: conexão direta com ERP / banco contábil (fora do escopo da v1).

### 3.6 Reutilização do RFC-001

| Elemento | Reutilização |
|---|---|
| Pasta de certificados `.pfx` | Idêntica |
| Senhas / Credential Manager | Idêntica |
| Carteira de empresas (CNPJ + thumbprint) | Idêntica |
| Pasta destino de XMLs | Fonte principal das notas recebidas |
| Persistência de NSU | Não alterada; este módulo consome os XMLs já baixados |
| Logging | Mesmo padrão estruturado |

---

## 4. Requisitos

### 4.1 Funcionais

| ID | Requisito |
|---|---|
| RF-01 | Listar notas recebidas (NFS-e e NF-e) por CNPJ da carteira |
| RF-02 | Exibir status atual de manifestação e prazo restante |
| RF-03 | Permitir seleção em lote e envio de Ciência da Operação |
| RF-04 | Permitir envio de Confirmação da Operação |
| RF-05 | Permitir envio de Desconhecimento da Operação |
| RF-06 | Permitir envio de Operação não Realizada (com justificativa) |
| RF-07 | Registrar trilha de auditoria (quem, quando, qual evento, resposta da API) |
| RF-08 | Importar planilha/CSV de valores de referência |
| RF-09 | Cruzar valores e classificar (OK / Divergente / Sem referência) |
| RF-10 | Destacar divergências na grid e permitir filtro |
| RF-11 | Exportar relatório de pendências e divergências (Excel) |
| RF-12 | Reutilizar certificados e carteira do RFC-001 |
| RF-13 | Suportar múltiplos CNPJs / filiais na mesma execução |

### 4.2 Não-Funcionais

| ID | Requisito | Meta |
|---|---|---|
| RNF-01 | Performance de listagem | ≥ 10.000 notas carregadas com filtros responsivos |
| RNF-02 | Segurança | Certificado/senha nunca em log; eventos assinados com o certificado correto |
| RNF-03 | Resiliência | Retry com backoff para erros de rede / 429 |
| RNF-04 | Auditoria | Toda manifestação registrada com timestamp e resposta da SEFAZ/ADN |
| RNF-05 | Compatibilidade | Windows 10+, .NET 8, mesmos pré-requisitos do RFC-001 |

---

## 5. Considerações de Segurança e Compliance

- Cada evento de manifestação é assinado/enviado com o **certificado A1 do CNPJ destinatário**.
- Justificativas de “Operação não Realizada” são persistidas e auditáveis.
- Logs não contêm chave privada nem senha.
- O sistema deve alertar claramente sobre **prazos** e sobre o efeito da **confirmação automática** (quando a legislação considerar a operação ocorrida na ausência de manifestação).
- Dados fiscais tratados conforme LGPD (acesso restrito à máquina do usuário / escritório).

---

## 6. Alternativas Consideradas

| Alternativa | Prós | Contras | Decisão |
|---|---|---|---|
| Continuar só com planilhas + portal SEFAZ | Sem desenvolvimento | Não escala, erro humano, sem trilha | ❌ |
| Usar apenas o download do RFC-001 | XMLs disponíveis | Sem decisão de manifestação nem cruzamento | Insuficiente |
| Módulo integrado ao RFC-001 | Uma única aplicação | Complexidade de UI | ✅ Preferida (mesmo instalador ou módulo separado) |
| SaaS com certificado no servidor | Multiusuário | Risco LGPD / certificado remoto | ❌ Descartada na v1 |

---

## 7. Plano de Entrega (Sugestão)

| Fase | Escopo | Duração estimada |
|---|---|---|
| F1 | Leitura dos XMLs recebidos + grid de notas + status de manifestação (somente leitura) | 1–2 semanas |
| F2 | Envio de Ciência e Confirmação (NF-e) + auditoria | 2 semanas |
| F3 | Desconhecimento + Operação não Realizada + prazos | 1 semana |
| F4 | Importação de planilha + motor de cruzamento de valores | 2 semanas |
| F5 | Relatórios, filtros avançados e polimento de UI | 1 semana |
| F6 | Homologação com equipe fiscal/contábil | 1 semana |

---

## 8. Riscos

| ID | Risco | Mitigação |
|---|---|---|
| R1 | Mudança de prazos ou regras de manifestação (NT) | Parametrizar prazos + camada de abstração de eventos |
| R2 | Diferenças de comportamento entre SEFAZ estaduais | Testes em múltiplos ambientes + tratamento de códigos de retorno |
| R3 | Eventos de NFS-e ainda em evolução no ADN | Começar pelo que estiver documentado e estável; feature flag |
| R4 | Falso positivo/negativo no cruzamento de valores | Tolerância configurável + revisão humana obrigatória em divergências |
| R5 | Usuário manifesta “Confirmação” por engano | Confirmação em duas etapas + log irreversível claro |

---

## 9. Métricas de Sucesso

- 100% das notas recebidas da carteira visíveis em um único painel.
- Redução do tempo de decisão de manifestação de dias/horas para minutos.
- Zero perda de prazo por falta de alerta.
- Divergências de valor detectadas antes do fechamento contábil/fiscal.
- Trilha de auditoria completa para qualquer nota manifestada.

---

## 10. Questões em Aberto

1. O módulo será **integrado na mesma aplicação** do RFC-001 ou um executável separado (com mesma base de certificados)?
2. Escopo inicial: **somente NF-e**, **somente NFS-e**, ou **ambos**?
3. Qual a tolerância padrão de valor no cruzamento (ex.: 0,01 / 0,5% / configurável por empresa)?
4. É necessário suporte a **múltiplos usuários** na mesma máquina (controle de quem manifestou)?
5. Deve haver **regra automática** (ex.: valor < R$ X → sugerir Ciência; divergência > Y% → bloquear Confirmação)?

---

## 11. Relação com o RFC-001

| Aspecto | RFC-001 | RFC-002 |
|---|---|---|
| Foco | Download em massa de XMLs (emitidas + recebidas + eventos) | Decisão sobre notas **recebidas** + cruzamento de valores |
| Entrada principal | API ADN (NSU) | XMLs já baixados + APIs de eventos de manifestação |
| Saída principal | Arquivos XML organizados | Eventos de manifestação registrados + relatório de divergências |
| Certificados | A1 por empresa | Mesmos certificados |
| Valor para o escritório | Escala operacional do download | Redução de risco fiscal e tempo de conferência |

Juntos, os dois módulos cobrem o ciclo completo do lado do **destinatário/tomador**:
1. Baixar tudo que foi emitido contra o CNPJ (RFC-001)
2. Decidir o que aceitar / rejeitar e conferir valores (RFC-002)

---

## 12. Glossário (complementar ao RFC-001)

| Termo | Definição |
|---|---|
| **Manifestação do Destinatário** | Conjunto de eventos pelos quais o destinatário informa ao Fisco se reconhece ou não a operação |
| **Ciência da Operação** | Evento não conclusivo; declara conhecimento da nota |
| **Confirmação da Operação** | Evento conclusivo; confirma que a operação ocorreu |
| **Desconhecimento da Operação** | Evento conclusivo; declara que não reconhece a operação |
| **Operação não Realizada** | Evento conclusivo; a operação não se efetivou |
| **Cruzamento de valores** | Comparação sistemática entre o valor da nota e uma base de referência (contrato, contabilidade, planilha) |
| **Tomador** | Destinatário do serviço na NFS-e |

---

## 13. Aprovações

| Papel | Nome | Data | Assinatura |
|---|---|---|---|
| **Autor** | [Seu nome] | ___/___/______ | |
| **Revisor Técnico** | [Nome] | ___/___/______ | |
| **Revisor Fiscal / Contábil** | [Nome] | ___/___/______ | |
| **Aprovação Final** | [Nome] | ___/___/______ | |

---

> **Nota:** Documento vivo. Alterações de Nota Técnica da NF-e ou do ADN NFS-e devem ser refletidas em novas versões.

### Histórico de Revisões

| Versão | Data | Autor | Alterações |
|---|---|---|---|
| 0.1 | 2025-01 | — | Versão inicial: escopo de manifestação + cruzamento de valores, alinhado ao RFC-001 |

---

## Anexo A — Valor para o Setor Contábil / Jurídico

| Antes | Depois (com RFC-002) |
|---|---|
| Notas recebidas espalhadas e sem status claro | Painel único por CNPJ com status de manifestação e prazo |
| Decisão de “assinar ou não” sem contexto | Grid com valor, emitente, prazo e resultado do cruzamento |
| Manifestação feita em portais diferentes, um a um | Envio em lote com o certificado correto e trilha de auditoria |
| Cruzamento de valores em planilhas manuais | Importação + motor de match + destaque de divergências |
| Risco de confirmação automática por prazo | Alertas de prazo e visão de pendências |
| Dificuldade de provar o que foi decidido | Log completo de cada manifestação enviada |

O módulo transforma a gestão de notas recebidas de um processo reativo e arriscado em um fluxo **controlado, auditável e escalável** — exatamente a dor que aparece quando o volume de CNPJs e de notas cresce.
