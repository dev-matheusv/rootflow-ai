# Root.IA: plano da IA única da família Root (card RB-17)

Status: **proposta, aguardando aprovação do Matheus.** Nada foi implementado.
Data: 07/11/2026. Revisado em 13/11/2026 (alvo .NET 10, RootFlow segue na OpenAI, nome, privacidade de dado de saúde, veredito sobre IA única).

## 0. Vale a pena ter uma IA só? (veredito de 13/11/2026)

**Sim, com três condições.** Hoje já existem três motores, e a cópia começou (extrator do RootFlow copiado para o RootBoost em 07/11). Cada correção de segurança, cada controle de custo e cada melhoria de qualidade teria que ser feita três vezes, e com uma pessoa mantendo tudo isso não acontece. Juntar também cumpre o que o RootBoost já decidiu em 27/10 (PLANO, Onda 3, cards RB-17 e RB-56).

O que se compartilha é **o motor**, não o conhecimento:

| Compartilha (Root.IA) | Fica em cada produto |
|---|---|
| Porta do modelo, adaptadores Claude e OpenAI, streaming, tool use | Prompts, regras de negócio, créditos e preço ao cliente |
| Registro de custo em USD, teto de gasto, aviso de modelo sem preço | Base de conhecimento e vetores de cada cliente |
| Extração de documento, embeddings, busca (fase 2) | Dado de paciente, dado de cliente, dado de campanha |
| Ação com confirmação humana (proposta assinada) | Quais ferramentas existem e quem pode usar cada uma |
| Redação de log (só metadados), testes de contrato, modo Fake | Termo de uso da IA, consentimento, trilha de auditoria do produto |

Condições:
1. **Pacote pequeno e versionado.** Cada produto fixa a versão e sobe quando quiser. Nenhum produto para por causa de outro.
2. **Nenhum dado cruza produtos.** "Somar conhecimento" vale para o código e para o aprendizado de engenharia (prompts que funcionam, avaliação, custo), nunca para dado de cliente. Base do RootFlow de um escritório não alimenta o MedlyCare, e prontuário nunca sai do MedlyCare. Isso não é só boa prática: misturar dado de saúde com outra finalidade fere a LGPD (art. 6º, finalidade, e art. 11).
3. **Cada produto mantém a própria porta.** Se o Root.IA tiver problema, o produto troca a implementação sem reescrever regra de negócio.

Manter como está (três motores) só faria sentido se os produtos usassem IA de jeitos sem nada em comum. Não é o caso: os três precisam de modelo, custo, documento, busca e log seguro.

## 0.1 Nome: Root.IA ou Nativa IA

**Recomendação: `Root.IA` para o motor interno; "Nativa IA" fica para o módulo que o cliente vê.**

- A decisão de marca de 11/11 já separa as camadas: **Nativa Labs** é a empresa, **Nativa** é a plataforma de marketing (Nativa Ads, Nativa Studio, Nativa IA), **Root** é o motor interno (Root.IA, RootFlow). O motor ficar com "Root" honra a origem no RootFlow e segue essa regra.
- Se o motor se chamar Nativa IA, o nome passa a significar duas coisas: o módulo de marketing que o cliente compra e o motor que roda dentro do MedlyCare e do RootFlow. Um médico nunca deveria ver "Nativa" (marketing) dentro do prontuário.
- Nome interno (namespace, pacote, repositório) não aparece para o cliente, então não depende do registro no INPI. Em 13/11, "NATIVA" isolada está registrada por terceiros nas classes 35 e 42; isso pesa para a marca pública, não para o pacote.
- Alternativa, se preferir amarrar tudo à empresa: pacotes `NativaLabs.Ai.*`. Funciona, mas troca um nome que já está nos cards e docs (RB-17, PLANO) sem ganho técnico.
- Grafia no código: `Root.Ia` (padrão .NET de siglas com duas letras ou mais: só a inicial maiúscula). Em texto: Root.IA.

## 1. Objetivo

Hoje existem três motores de IA separados: RootFlow, RootBoost (C:\RootMark) e MedlyCare (C:\SFA_Sistema). Melhoria feita em um não chega nos outros, e a extração de PDF e DOCX já foi copiada do RootFlow para o RootBoost em 07/11. O Root.IA junta as peças comuns num lugar só, consumido pelos três, sem que nenhum produto pare de funcionar durante a troca.

Fica fora do Root.IA tudo que é regra de negócio de um produto: créditos e markup do RootFlow, roteiro de vídeo do RootBoost, regras clínicas e LGPD específicas do MedlyCare, prompts de cada funcionalidade.

## 2. O que existe hoje (levantamento)

| Peça | RootFlow (net9.0) | RootBoost (net9.0) | MedlyCare (net8.0) |
|---|---|---|---|
| Porta do modelo | `IChatCompletionService.CompleteAsync(Messages)`, só texto | `ILanguageModel.GenerateJsonAsync` com JSON Schema e imagem JPEG | `IModeloLinguagem` com mensagens em blocos, tool use e streaming |
| Provedor | OpenAI por HTTP próprio (`gpt-4.1-mini`) | Claude pelo SDK oficial `Anthropic` 13.3.0 | Claude e Fake |
| Modos | `AI:Mode` OpenAI ou Fake | `Ai:Mode` Off, Fake, Claude | `Ia__Modo` Desligado por padrão, Fake, Claude |
| Modelo por finalidade | não | sim, `Ai__Models__<finalidade>` | sim, `PedidoIa.Modelo` por pedido |
| Custo por chamada | tokens viram créditos (`ConfiguredAiUsagePricingCalculator`) | `AiUsageEntry` em USD por finalidade e marca | uso e custo gravados por funcionalidade |
| Embeddings | `IEmbeddingService` OpenAI `text-embedding-3-small` | não tem | `IGeradorEmbeddings` com `FinalidadeEmbedding` |
| Busca | `PostgresKnowledgeSearchService` pgvector com nota vetorial, de palavra e de frase | não tem | `Conhecimento/` com cortador de seções e reordenador |
| Extração de documento | `SimpleDocumentTextExtractor`: PDF (PdfPig), DOCX e PPTX (OpenXml), XLSX (ClosedXML), imagem pela visão da OpenAI | cópia de PDF e DOCX em `DocumentTextExtractor.cs` | não tem |
| Ação com confirmação humana | não | não | proposta assinada válida por 15 minutos |

Observações do levantamento:
- O assistente de IA do MedlyCare está na branch `feat/mc-429-ia-ligar-na-empresa`, ainda fora da `develop`.
- O RootBoost devolve custo zero em silêncio quando o modelo não está na tabela de preços (`AiPricing.Cost`). O Root.IA deve avisar em vez disso.
- A extração do RootFlow recebe caminho de arquivo (`StoredFile.StoragePath`). Para servir aos três, o Root.IA deve receber `Stream`.
- A leitura de imagem do RootFlow chama a OpenAI direto, fora do cálculo de créditos.

## 3. Como distribuir

### Opções avaliadas

| Opção | Prós | Contras |
|---|---|---|
| **A. Repositório próprio `dev-matheusv/root-ia` com pacotes NuGet no GitHub Packages** | Versão semântica, cada produto atualiza no seu ritmo, código privado, CI próprio | Precisa de token de leitura no build (CI e Docker do Railway) |
| B. Pacote publicado de dentro do repositório do RootFlow | Um repositório a menos | RootFlow é público, a versão do pacote fica presa ao ciclo do RootFlow |
| C. Pacote público no nuget.org | Build sem token | Código e decisões expostos, nome público difícil de mudar depois |
| D. Git submodule ou projeto referenciado por caminho | Nada para publicar | Build frágil, o Docker do Railway não enxerga caminho local, versão implícita |
| E. Continuar copiando | Nada a fazer agora | É exatamente o problema que queremos resolver |

### Recomendação: opção A

Repositório próprio e privado, pacotes NuGet no GitHub Packages, alvo `net10.0`.

Por quê:
1. **Cada produto continua funcionando durante a migração.** Com a versão fixada (`0.1.0`, `0.2.0`), o RootFlow pode subir para uma versão nova sem quebrar o MedlyCare, que segue na anterior até alguém migrar.
2. **`net10.0` porque o .NET 8 e o .NET 9 saíram de suporte em 10/11/2026** (anúncio oficial da Microsoft: o STS passou a ter 24 meses, e o 9 terminou junto com o 8). Desde então não recebem correção de segurança. O .NET 10 é LTS com suporte até novembro de 2028. O MedlyCare vai para o 10 na v15, e o RootFlow e o RootBoost migram antes do Root.IA (RF-01 no roadmap do RootFlow). Não mirar o .NET 11: é STS e sai de suporte antes do 10.
3. **Privado.** O repositório do RootFlow é público, e o Root.IA vai concentrar decisões de custo e provedor da família inteira.
4. **Custo operacional conhecido:** um token com `read:packages` guardado como secret no GitHub Actions de cada produto e como variável de build no Railway, com o `nuget.config` lendo a variável de ambiente. Nenhum segredo no código.

### Pacotes (pequenos de propósito, para nenhum produto carregar dependência que não usa)

| Pacote | Conteúdo | Dependências |
|---|---|---|
| `Root.Ia.Abstractions` | Interfaces, records, exceções, opções, registro de uso | só o BCL |
| `Root.Ia.Anthropic` | Adaptador Claude (código vem do `ClaudeLanguageModel` do RootBoost) | `Anthropic` |
| `Root.Ia.OpenAI` | Adaptadores de chat e embeddings OpenAI (código vem do RootFlow) | `System.Net.Http.Json` |
| `Root.Ia.Documents` | Extração de PDF, DOCX, PPTX, XLSX, texto e imagem | PdfPig, OpenXml, ClosedXML |
| `Root.Ia.Testing` | Implementações Fake determinísticas e testes de contrato que todo adaptador roda | xUnit |
| `Root.Ia.Search.Postgres` (fase 2) | Busca híbrida com pgvector | Npgsql, Pgvector |
| `Root.Ia.Knowledge` | Pacotes de conhecimento: carga, validação, ingestão idempotente, portões de segredo e dado pessoal (seção 4.6) | só o BCL |

## 4. Interface de cada peça

Nomes em inglês, que é o padrão de dois dos três produtos e do ecossistema .NET. Cada produto pode manter os próprios nomes na sua camada de aplicação durante a migração (ver seção 5).

### 4.1 Modelo de linguagem

```csharp
public interface ILanguageModel
{
    Task<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken ct = default);
    IAsyncEnumerable<ModelStreamEvent> StreamAsync(ModelRequest request, CancellationToken ct = default);
}

public sealed record ModelRequest(
    string Purpose,                         // finalidade, vai para o registro de custo
    string? System,                         // nunca recebe texto do usuario final (regra do MedlyCare vira regra da casa)
    IReadOnlyList<ModelMessage> Messages,
    IReadOnlyList<ModelTool>? Tools = null,
    JsonElement? ResponseSchema = null,     // structured output
    int? MaxTokens = null,
    string? Model = null,                   // troca de modelo so neste pedido
    string? Tenant = null);                 // workspace, marca ou clinica, para o custo

public sealed record ModelMessage(ModelRole Role, IReadOnlyList<ModelContent> Content);
public enum ModelRole { User, Assistant }

public abstract record ModelContent;
public sealed record TextContent(string Text) : ModelContent;
public sealed record ImageContent(ReadOnlyMemory<byte> Data, string MediaType) : ModelContent;
public sealed record ToolCallContent(string Id, string Name, string InputJson) : ModelContent;
public sealed record ToolResultContent(string CallId, string Json, bool IsError = false) : ModelContent;

public sealed record ModelTool(string Name, string Description, JsonElement InputSchema);

public sealed record ModelResponse(
    IReadOnlyList<ModelContent> Content,
    ModelStopReason StopReason,
    ModelUsage Usage,
    string Provider,
    string Model)
{
    public string Text => string.Concat(Content.OfType<TextContent>().Select(t => t.Text));
    public IEnumerable<ToolCallContent> ToolCalls => Content.OfType<ToolCallContent>();
}

public enum ModelStopReason { End, ToolUse, MaxTokens, Refusal }

public sealed record ModelUsage(
    long InputTokens, long OutputTokens, long CacheReadTokens, long CacheWriteTokens, decimal? CostUsd);

public abstract record ModelStreamEvent;
public sealed record TextDelta(string Text) : ModelStreamEvent;
public sealed record ToolCallRequested(ToolCallContent Call) : ModelStreamEvent;
public sealed record StreamCompleted(ModelStopReason StopReason, ModelUsage Usage, string Provider, string Model) : ModelStreamEvent;
```

Atalho para o estilo do RootBoost (pede JSON, recebe objeto):

```csharp
public static Task<StructuredResult<T>> GenerateJsonAsync<T>(
    this ILanguageModel model, ModelRequest request, CancellationToken ct = default);
```

Erros iguais nos três produtos: `AiDisabledException` (modo Off ou sem chave, a tela mostra o aviso em vez de quebrar) e `AiRequestFailedException` (mensagem segura para exibir, detalhe só no log).

Configuração, juntando o melhor de cada produto:

```csharp
public sealed class RootIaOptions
{
    public AiProvider Mode { get; set; } = AiProvider.Off;   // Off, Fake, Anthropic, OpenAI
    public string Model { get; set; } = "";                  // sem padrao escondido; cada produto define
    public Dictionary<string, string> ModelsByPurpose { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? ApiKey { get; set; }                      // vem de variavel de ambiente
}
```

Off é o padrão, como no RootBoost e no MedlyCare: nada é chamado nem cobrado até ligar e pôr a chave.

### 4.2 Registro de custo

O Root.IA calcula o custo em dólar. O que fazer com ele (créditos, relatório, alerta) é decisão de cada produto.

```csharp
public sealed record AiUsageRecord(
    Guid Id, string Purpose, string? Tenant, string Provider, string Model,
    long InputTokens, long OutputTokens, long CacheReadTokens, long CacheWriteTokens,
    decimal? CostUsd, DateTimeOffset CreatedAt);

public interface IAiUsageSink            // o produto implementa (tabela AiUsage, ledger de creditos etc.)
{
    Task RecordAsync(AiUsageRecord record, CancellationToken ct = default);
}

public interface IModelPricing           // tabela vem de configuracao, com padrao embutido
{
    bool TryGetCost(string model, ModelUsage tokens, out decimal costUsd);
}
```

Um decorador `UsageRecordingLanguageModel` envolve qualquer `ILanguageModel` e grava cada chamada, inclusive em streaming (grava no `StreamCompleted`). Modelo sem preço na tabela gera aviso no log e custo `null` (desconhecido), nunca zero em silêncio.

Os créditos e o markup do RootFlow continuam no RootFlow, alimentados pelo `IAiUsageSink`.

### 4.3 Embeddings

```csharp
public interface IEmbeddingGenerator
{
    Task<EmbeddingResult> GenerateAsync(
        IReadOnlyList<string> inputs, EmbeddingPurpose purpose, CancellationToken ct = default);
}

public enum EmbeddingPurpose { Document, Query }

public sealed record EmbeddingResult(
    IReadOnlyList<float[]> Vectors, int InputTokens, string Provider, string Model, int Dimensions);
```

`EmbeddingPurpose` vem do MedlyCare: alguns provedores tratam texto de documento e texto de pergunta de forma diferente. Embeddings também passam pelo registro de custo.

A Anthropic não oferece embeddings próprios, então o adaptador de embeddings da fase 1 é o da OpenAI, que o RootFlow já usa. Outro provedor só entra se algum produto precisar, e com a documentação oficial dele em mãos.

### 4.4 Extração de documento

```csharp
public interface IDocumentTextExtractor
{
    bool CanExtract(string fileName, string? contentType);
    Task<ExtractedDocument> ExtractAsync(
        Stream content, string fileName, string? contentType, CancellationToken ct = default);
}

public sealed record ExtractedDocument(
    string Text,
    IReadOnlyList<ExtractedSection> Sections,   // pagina, slide ou aba, para citar a fonte
    IReadOnlyList<string> Warnings);            // pagina ilegivel, aba vazia etc.

public sealed record ExtractedSection(string Label, string Text);
```

Mudanças em relação ao código atual do RootFlow:
- Recebe `Stream`, não caminho em disco. O RootFlow abre o arquivo e passa o stream.
- Imagem passa pelo `ILanguageModel` com `ImageContent`. Funciona com Claude ou OpenAI e entra no registro de custo.
- Seções por página, slide ou aba já saem prontas para a citação de fonte que o RootFlow faz.
- O tratamento de PDF malformado do RootFlow (try e catch no `MoveNext` do PdfPig e a queda para `GetWords`) vai junto, com os testes.

### 4.5 Fase 2 e depois

- **Busca semântica:** `ITextChunker`, `IVectorIndex` e a busca híbrida do `PostgresKnowledgeSearchService` em `Root.Ia.Search.Postgres`. O cortador de seções e o reordenador do MedlyCare entram como alternativas plugáveis. O `SemanticQueryExpander` (português e inglês) vira opção.
- **RAG e chat:** montagem de contexto com citação e a regra "se não está nos documentos, diga que não encontrou".
- **Ações com confirmação humana:** generalizar a proposta assinada de 15 minutos do MedlyCare (`ProposedAction` com assinatura HMAC e expiração). Nenhuma ferramenta que altera dados roda sem confirmação.

### 4.6 Conhecimento padrão: a IA já nasce sabendo (card RF-33, pedido de 13/11/2026)

Objetivo: no primeiro dia, a IA de cada produto já conhece o produto, o mercado e o que aprendemos, sem ninguém precisar subir documento.

**Levantamento (13/11/2026):**

| Origem | O que tem | Onde está | Já é usado pela IA? |
|---|---|---|---|
| RootBoost `knowledge/` | Playbook de copy, gatilhos, ganchos, checklist, limites e políticas por canal, formatos de vídeo, testes de anúncio, aprendizados por nicho (10 arquivos, 60 KB, validados por teste) | `C:\RootMark\knowledge` | Sim, pelo `FileKnowledgeBase` |
| Fonte do conhecimento do RootBoost | Versão 1 em texto (anúncios, aprendizados, conferência, método de copy, persuasão, vídeo) | `Documents\RootBoost Conhecimento` | Indireto: já virou o `knowledge/` |
| Docs do RootBoost | Visão geral, estúdio, publicação, Meta Ads, base de conhecimento, roadmap | `C:\RootMark\docs` | Não |
| MedlyCare catálogo de funcionalidades | Como cada tela e fluxo do sistema funciona (250 linhas) | `Ia/Conhecimento/Fontes/funcionalidades.md` na branch da v14 | Sim, ingerido na subida da API (MC-384) |
| Docs da IA do MedlyCare | Custos, LGPD, produção, desenho da v14 | `docs/ia-*.md` | Não (são para a equipe) |
| RootFlow | Docs de produto e de treinamento | `C:\RootFlow\docs` | Não |
| Notion, Jira, pastas de marketing | Decisões, cards, roteiros, relatórios de uso | Fora dos repositórios | Não |

**Desenho: pacotes de conhecimento.** Cada produto tem um pacote versionado no próprio repositório, e o Root.IA oferece o mecanismo comum para carregar, validar e indexar.

1. **Formato:** Markdown com cabeçalho (título, finalidade, data, origem, confiança, público: cliente ou equipe) e JSON quando for regra estruturada. É o modelo que o RootBoost já usa.
2. **Mudança só por PR**, com o porquê. Teste que contradiz uma regra ganha da regra (regra do `knowledge/README.md` do RootBoost, vale para todos).
3. **Ingestão na subida, idempotente:** só reindexa o que mudou (hash do trecho), em segundo plano, sem derrubar a API se falhar (modelo do MC-384).
4. **Separado por produto e por público.** O pacote do MedlyCare nunca entra no RootBoost e vice versa. O que é para a equipe nunca vai para o cliente.
5. **Portões de segurança no CI do pacote:** varredura de segredos (chave, token, senha, connection string), de dado pessoal (CPF, telefone, e-mail que não seja genérico) e de nome de cliente real. Pacote que falha não publica.

**Código fonte, Notion e Jira: a IA aprende com eles, mas não lê direto.**

- **Código fonte nunca vai para a IA do cliente.** Expõe como o sistema funciona por dentro, rotas, regras de permissão e, por engano, segredos. Em vez disso, a cada release um passo de "o que mudou" lê o código e os cards e **gera ou atualiza o catálogo de funcionalidades** em linguagem de usuário. Uma pessoa revisa no PR. É assim que a IA fica craque no produto sem ver o código.
- **Notion e Jira entram curados, não despejados.** Card de Jira pode ter nome de cliente, print com dado de paciente e discussão interna. Só a decisão e o porquê viram conhecimento, reescritos de forma geral, como o `learnings.json` já faz.
- **Pastas de marketing:** o que ensinou algo vira regra geral em `knowledge/`; o material do cliente fica no perfil da marca, no banco.
- **Assistente da equipe (interno) é outra coisa.** Um assistente que lê código, docs e cards para ajudar a desenvolver pode existir, com acesso só da equipe, sem dado de cliente e sem dado de saúde. Hoje esse papel é do Claude Code com as memórias de cada projeto; não vale construir outro agora.

**Ordem:** (1) mover o mecanismo de carga e validação do RootBoost para `Root.Ia.Knowledge`; (2) MedlyCare passa a usar o mesmo mecanismo para o `funcionalidades.md`; (3) RootFlow ganha o próprio catálogo (ajuda do produto dentro do chat); (4) gerador de "o que mudou" por release, com revisão humana; (5) portões de segredo e dado pessoal no CI dos três.

## 5. Ordem de migração

Regra para todos: o produto mantém a própria porta (`IChatCompletionService`, o `ILanguageModel` do RootBoost, `IModeloLinguagem`) e passa a implementá-la por cima do Root.IA. A porta antiga só some depois que tudo estiver estável. Os testes em modo Fake de cada produto precisam passar iguais antes e depois.

### Etapa 0: fundação (no repositório novo)
1. Criar `dev-matheusv/root-ia` com a higiene da casa: `.editorconfig`, `.gitattributes`, `global.json`, CI com build, testes e publicação no GitHub Packages a cada tag.
2. Publicar `0.1.0` com `Abstractions`, `Testing` (Fake e testes de contrato) e `Documents` (código movido do RootFlow, com os testes).
3. Os adaptadores reaproveitam código que já roda em produção: Claude vem do RootBoost, OpenAI vem do RootFlow. Nada de escrever chamada de API de memória.

### Etapa 1: RootBoost primeiro
Por quê: a porta dele é a mais próxima da proposta, ele já registra custo em USD e tem a cópia do extrator que precisa sumir.
1. Trocar `DocumentTextExtractor.cs` por `Root.Ia.Documents` e apagar a cópia.
2. Mover o `ClaudeLanguageModel` para `Root.Ia.Anthropic`. O `ILanguageModel` do RootBoost passa a delegar.
3. `IAiUsageStore` vira a implementação de `IAiUsageSink`.

Risco baixo: tudo é chamado em fluxos internos (roteiro, cadastro, gravação de tela), com Off por padrão.

### Etapa 2: RootFlow
1. `SimpleDocumentTextExtractor` vira um adaptador fino sobre `Root.Ia.Documents`.
2. `OpenAiChatCompletionService` e `OpenAiEmbeddingService` passam a usar `Root.Ia.OpenAI`. O `IChatCompletionService` continua existindo, então o `ChatService` e o modo de treinamento não mudam.
3. Os créditos continuam calculados pelo RootFlow, agora a partir do `IAiUsageSink`.
4. **Não trocar provedor nem modelo de embedding nesta etapa.** Mesmo `gpt-4.1-mini`, mesmo `text-embedding-3-small`. Trocar exige reindexar todos os documentos e reavaliar a qualidade das respostas.
5. Portão de saída: `RagEvaluationBaselineTests` passando igual.

### Etapa 3: MedlyCare
1. Esperar a `feat/mc-429-ia-ligar-na-empresa` entrar na `develop` e a v15 (migração para .NET 10) estar em produção.
2. `IModeloLinguagem` passa a delegar ao `ILanguageModel` (tool use e streaming já cobertos pela interface). `IGeradorEmbeddings` delega ao `IEmbeddingGenerator`.
3. Conferir que nenhum log do Root.IA registra conteúdo de mensagem (ver risco 6).

É o último porque tem a maior superfície (ferramentas, streaming, dado de saúde) e depende da v15. Antes de ligar, passar pelo checklist da seção 5A.

### Etapa 4: fase 2 (busca e RAG) e fase 3 (ações com confirmação)
Só depois das três etapas estáveis em produção por pelo menos duas semanas.

### Autonomia (vem da Onda 3 do RootBoost)
Cada marca ou empresa escolhe o modo, e o padrão é sempre o mais seguro:
- **Copiloto (padrão):** a IA propõe, a pessoa confirma. Toda ação que grava algo passa pela proposta assinada.
- **Piloto automático (só RootBoost, por marca):** a IA executa sozinha dentro de teto de gasto, só ações reversíveis, com registro de auditoria e botão de desfazer.
- **No MedlyCare não existe piloto automático.** Nada clínico (prescrição, diagnóstico, prontuário) é gravado sem o profissional confirmar. A IA apoia, a decisão e a responsabilidade são do profissional.

## 5A. Segurança, LGPD e sigilo médico

O "nome de segurança e privacidade da parte médica" é o **sigilo médico** (Código de Ética Médica, Resolução CFM 2.217/2018, art. 73 a 79), somado às regras de **prontuário eletrônico** (Resolução CFM 1.821/2007 e Lei 13.787/2018) e à certificação **SBIS/CFM (NGS1 e NGS2)** para sistemas de registro eletrônico de saúde. Nos EUA o equivalente é a **HIPAA**; não se aplica a nós, mas serve de régua. Por cima de tudo, a **LGPD**: dado de saúde é **dado pessoal sensível** (art. 5º, II e art. 11), e o compartilhamento com fornecedor para obter vantagem econômica é vedado (art. 11, §4º).

Profissionais de outras áreas atendidas pelo MedlyCare têm o próprio sigilo (psicologia, fisioterapia, odontologia, nutrição), e as regras abaixo valem igual. Antes de lançar a IA para clientes, pedir revisão de um advogado de proteção de dados: este plano é técnico, não parecer jurídico.

Regras do Root.IA (valem para os três produtos, obrigatórias no MedlyCare):

| # | Regra | Como o código garante |
|---|---|---|
| 1 | **Mínimo necessário.** Só vai ao modelo o trecho que a tarefa precisa | O produto monta o pedido; o Root.IA não busca dado sozinho |
| 2 | **Pseudonimização antes de sair.** Nome, CPF, telefone e e-mail do paciente viram marcadores (`[PACIENTE_1]`) e voltam só na tela | O `Pseudonimizador` do MedlyCare (MC-385) vira etapa plugável do Root.IA, com teste que falha se um CPF passar |
| 3 | **Nada de conteúdo em log.** Log, Sentry e trilha de custo guardam só finalidade, modelo, tokens, custo, duração e IDs | Decorator de log com lista de campos permitidos; teste de contrato procura texto do pedido nos logs e falha se achar |
| 4 | **Fornecedor sem treino e sem retenção.** Usar só contas e endpoints em que o provedor não treina com os dados, com retenção mínima | Configuração por produto, conferida na documentação oficial do provedor antes de ligar (Bedrock no MedlyCare, já decidido em 22/10) |
| 5 | **Transferência internacional declarada.** Modelos rodam fora do Brasil (perfil global do Bedrock, OpenAI nos EUA) | Listar subprocessadores e país na política de privacidade e no contrato com a clínica (LGPD art. 33) |
| 6 | **Base legal e consentimento.** Clínica ativa a IA aceitando termo; uso clínico com base na tutela da saúde (art. 11, II, f) e transparência ao paciente | Termo `/ia/ativar` do MC-385, com versão e data gravadas |
| 7 | **Isolamento por cliente.** Cada pedido carrega `Tenant`; busca e cache nunca cruzam empresas | Teste de contrato com duas empresas no mesmo banco |
| 8 | **Ferramentas com o poder de quem pergunta.** A IA chama a API com o token do usuário, nunca com conta de serviço | Molde do MC-387 (`ICanalApiInterna`) |
| 9 | **Gravação só com confirmação humana.** | Proposta assinada (HMAC, 15 minutos), molde do MC-388 |
| 10 | **Defesa contra instrução escondida em documento** (prompt injection) | Conteúdo de documento e de paciente vai em bloco marcado como dado, nunca no system prompt; ferramentas que gravam exigem confirmação de qualquer forma |
| 11 | **Teto de custo.** Limite por empresa e por dia, alerta antes de estourar | `IAiUsageSink` + teto configurável; modelo sem preço gera aviso, nunca custo zero |
| 12 | **Direito do titular.** Apagar paciente apaga também uso, cache e vetores ligados a ele | Cada produto implementa; o Root.IA não guarda conteúdo, então não tem o que apagar do lado dele |
| 13 | **Incidente.** Vazamento de dado sensível tem prazo de comunicação à ANPD e ao titular | Runbook de incidente em cada produto, com contato do encarregado (DPO) |

## 6. Riscos e como tratar

| # | Risco | Tratamento |
|---|---|---|
| 1 | Mudança no pacote quebra um produto | Versão semântica, versão fixada em cada produto, testes de contrato que todo adaptador roda no CI do Root.IA |
| 2 | Build no Railway falha sem acesso ao GitHub Packages | Token `read:packages` como variável de build e `nuget.config` lendo a variável. Testar o build Docker antes de migrar o primeiro produto |
| 3 | Produto ainda em .NET 8 ou 9 não consegue consumir pacote `net10.0` | Migrar para o .NET 10 antes (RootFlow RF-01, MedlyCare v15, RootBoost card próprio). É obrigatório de qualquer forma: 8 e 9 estão sem suporte desde 10/11/2026 |
| 4 | Troca de modelo de embedding invalida os vetores salvos | Não trocar durante a migração. Se um dia trocar, reindexação planejada à parte |
| 5 | Respostas do RootFlow mudam ao trocar o provedor do chat | A migração mantém provedor e modelo. Trocar para Claude é decisão separada, com avaliação pela base de testes de RAG |
| 6 | Dado sensível em log (LGPD, principalmente no MedlyCare) | O Root.IA loga só metadados: finalidade, modelo, tokens, custo e duração. Nunca prompt, resposta, imagem ou documento |
| 7 | Tabela de preços desatualizada | Preços em configuração com padrão embutido. Modelo desconhecido gera aviso, nunca custo zero silencioso |
| 8 | SDK da Anthropic muda com frequência | Isolado em `Root.Ia.Anthropic`. Os outros pacotes não dependem dele |
| 9 | Pacote cresce demais | A fase 1 cobre só as peças 4.1 a 4.4. O resto entra quando um produto precisar de verdade |
| 10 | Uma pessoa mantendo tudo | Pacote pequeno, CI automático, changelog curto a cada versão |

## 7. Decisões que preciso do Matheus

Já respondidas:
- **Provedor do RootFlow (13/11/2026):** segue na OpenAI. O Claude ficou caro nos testes do RootBoost. Trocar só com avaliação pela base de testes de RAG e estimativa de custo por uso.
- **Valer a pena (13/11/2026):** a dúvida era se valia; o veredito está na seção 0.

Ainda abertas:
1. **Distribuição:** aprova repositório próprio privado com GitHub Packages (opção A)?
2. **Nome:** `Root.Ia` (recomendado, seção 0.1) ou `NativaLabs.Ai`?
3. **Ordem:** .NET 10 nos três, depois RootBoost, RootFlow e MedlyCare. De acordo?
4. **Revisão jurídica:** quem faz a revisão de LGPD e sigilo antes de a IA do MedlyCare chegar a clínicas reais?

Depois da aprovação, o primeiro passo concreto é a Etapa 0: repositório, CI e versão `0.1.0` com abstrações, Fake e extração de documento.
