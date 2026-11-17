# RootFlow: roadmap

Atualizado em 13/11/2026. Fonte da verdade do que falta fazer no RootFlow. Segue o formato do RootBoost (`C:\RootMark\docs\PLANO.md`) e o jeito de trabalhar do MedlyCare.

Cards usam o prefixo **RF**. Cada card vira uma branch a partir da `develop` e um PR para ela. A `main` só recebe release (`develop` → `main`), que dispara o deploy.

---

## 1. Onde estamos (13/11/2026)

**Produção:** backend no Railway (`/health` 200), frontend na Vercel, banco Postgres com pgvector no Supabase, pagamentos no Stripe, e-mail pela Resend, erros no Sentry. Último smoke pós deploy verde. Zero clientes pagantes. Primeiro cliente em negociação (Grupo Renac, 120 pessoas, jurídico).

**`main` = `c0c7505`.** O que já está em produção:
- Workspaces com Proprietário, Admin e Membro; convite por e-mail; cadastro, login, recuperação de senha.
- Base de conhecimento: upload de PDF, DOCX, PPTX, XLSX, texto e imagem; corte em trechos; embeddings OpenAI `text-embedding-3-small`; busca híbrida no pgvector.
- Chat com respostas baseadas nos documentos e citações (`gpt-4.1-mini`).
- Modelos de documento com geração por IA.
- Cobrança: planos Starter, Pro e Business, pacotes de crédito, teste grátis de 7 dias com 3.000 créditos, portal do Stripe, créditos primeiro e assinatura depois, webhooks com assinatura conferida, painel do admin da plataforma.
- Modo Treinamento: programas, módulos, quiz gerado por IA com revisão, tentativas, nota, certificado em PDF com verificação pública, liberado por workspace.
- Endurecimento: limite de requisições, Sentry, normalização de assinaturas. CI com jobs separados de frontend e backend e smoke pós deploy.

**Branches (limpas em 13/11/2026):**

| Branch | Para quê |
|---|---|
| `main` | Produção. Protegida: só PR com CI verde, histórico linear |
| `develop` | Integração (criada em 13/11 a partir da `main`). Cards saem daqui |
| `feat/enterprise-plan-renac` | PR #12 aberto. **Não mesclar ainda** (ver RF-10) |
| `docs/root-ia-plano` | Este roadmap e o plano do Root.IA |
| `release/v1` | Marco antigo, igual à `main` antiga. Pode ser apagada quando quiser |

As 12 branches já mescladas (PRs #1 a #11) e dois worktrees antigos foram apagados. Os PRs continuam no GitHub, e cada branch pode ser restaurada pelo botão do PR.

---

## 2. Ondas

### Onda 0: base saudável (agora, antes de qualquer funcionalidade)

| Card | O quê | Por quê |
|---|---|---|
| **RF-01** | **Migrar para .NET 10** (`global.json`, `TargetFramework`, imagens `sdk:10.0` e `aspnet:10.0` no Dockerfile, `dotnet-version` no CI, pacotes NuGet) | O .NET 9 saiu de suporte em 10/11/2026 e não recebe mais correção de segurança. O 10 é LTS até novembro de 2028. Também é pré-requisito do Root.IA (pacote `net10.0`) e alinha com a v15 do MedlyCare |
| **RF-02** | Fluxo `develop` → `main`: CI rodando em push na `develop`, proteção da `develop` igual à da `main`, seção "Como trabalhamos" no README | Mesmo fluxo do MedlyCare e do RootBoost |
| **RF-03** | Backup e restauração testados: confirmar o plano do Supabase (backup diário ou PITR), fazer um restore de teste num banco separado, documentar o passo a passo | Sem restore testado, backup é suposição. Cliente jurídico vai perguntar |
| **RF-04** | Monitoramento contínuo: smoke agendado (a cada 15 min, não só no deploy) e alerta por e-mail quando falhar; alertas do Sentry configurados | Hoje só sabemos que caiu quando alguém abre o site |
| **RF-05** | Repositório privado: `dev-matheusv/rootflow-ai` está **público** | Produto comercial com regra de cobrança, preço e infraestrutura expostos. Decisão do Matheus (ver seção 4) |
| **RF-06** | Leitura de imagem dentro dos créditos: hoje a visão da OpenAI é chamada direto, fora do cálculo de créditos | Vazamento de custo: o cliente usa e a gente paga |

### Onda 1: pronto para o primeiro cliente (Grupo Renac)

| Card | O quê | Por quê |
|---|---|---|
| **RF-10** | **Enterprise vendido por contrato, não por checkout.** O PR #12 cria o plano, mas a landing e a página de cobrança mostram todo plano ativo, então R$ 3.360 apareceria no preço público com botão de assinar. Ajustar: campo "visível no site" no plano (ou filtro), card "Enterprise: fale com a gente" na landing, atribuição do plano pelo painel admin. Depois: produtos no Stripe e variáveis `STRIPE__PLANPRICES__3__PRICEID` e `STRIPE__CREDITPACKS__3__PRICEID` no Railway | Bloqueia o merge do PR #12 |
| **RF-11** | **Remover membro e mudar papel** no workspace (endpoint, tela, quem pode: Proprietário e Admin; o Proprietário não pode ser removido) | Não existe hoje. Com 120 pessoas, quem sai do escritório precisa perder o acesso no mesmo dia |
| **RF-12** | **Apagar documento** (arquivo, trechos e vetores) e **apagar conversa** | Não existe hoje. Documento enviado por engano fica para sempre na busca. Também é direito do titular (LGPD art. 18) |
| **RF-13** | **Excluir conta e exportar meus dados** (titular), e encerrar workspace (cliente que sai) | LGPD art. 18. Cliente jurídico vai pedir em contrato |
| **RF-14** | **Convite em lote** (colar lista de e-mails ou CSV, papel único, relatório de quem falhou) | Convidar 120 pessoas uma a uma não é viável |
| **RF-15** | **Pacote LGPD comercial:** política de privacidade e termos atualizados, contrato de operador (DPA), lista de subprocessadores com país (OpenAI EUA, Supabase, Railway, Vercel, Stripe, Resend, Sentry), canal do encarregado | Escritório de advocacia vai ler isso antes de assinar. Transferência internacional precisa estar declarada (art. 33) |
| **RF-16** | Registro de auditoria mínimo: login, convite, remoção, upload, exclusão, mudança de plano (quem, quando, o quê; sem conteúdo) | Pedido comum de cliente jurídico e base para o painel de auditoria (RF-42) |
| **RF-18** | **Conversas por pessoa.** Hoje a conversa não tem dono: todo membro vê o chat de todos. Gravar quem criou, listar só as próprias, Admin vê todas se o cliente quiser (configuração por workspace) | Sigilo entre advogados e clientes diferentes dentro do mesmo escritório. Prioridade para o Renac |
| **RF-17** | Indicação: 10% da mensalidade do indicado. Primeiro manual (planilha + cupom no Stripe); automatizar só quando houver volume | Decidido no deal Renac |

### Onda 2: qualidade da IA

| Card | O quê | Por quê |
|---|---|---|
| **RF-20** | Avaliação de RAG como portão: ampliar o `RagEvaluationBaselineTests` com um conjunto de perguntas e respostas esperadas por tipo de cliente (jurídico primeiro), rodando no CI em modo Fake e manualmente com a OpenAI | Qualquer mudança de prompt, corte ou modelo precisa provar que não piorou |
| **RF-21** | Defesa contra instrução escondida em documento: texto dos documentos sempre em bloco marcado como dado, nunca no system prompt; testes com documento malicioso | Documento enviado por um membro pode tentar mudar o comportamento do assistente para os outros |
| **RF-22** | Citação com detalhe: clicar na citação abre o trecho e a página do documento | Advogado precisa conferir a fonte |
| **RF-23** | Saúde dos documentos: mostrar documento que falhou na extração, sem texto (PDF escaneado), duplicado ou muito grande; reprocessar | Hoje a falha é silenciosa para o usuário |
| **RF-24** | Teto de gasto por workspace e alerta de consumo anormal | Proteção contra abuso e contra erro de loop |

Toda funcionalidade de IA nova vem com **estimativa de custo por uso** (regra da casa) e roda em modo Fake no CI.

### Onda 3: Root.IA (motor comum da família)

Plano completo em [`docs/root-ia-plano.md`](root-ia-plano.md). Depende do RF-01 e da aprovação das decisões abertas daquele documento.

| Card | O quê |
|---|---|
| **RF-30** | Etapa 2 do plano: `SimpleDocumentTextExtractor`, chat e embeddings do RootFlow passam a usar `Root.Ia.Documents` e `Root.Ia.OpenAI`. Mesmo provedor (OpenAI), mesmos modelos, sem reindexar. Portão: RF-20 passando igual |
| **RF-31** | Créditos calculados a partir do `IAiUsageSink` do Root.IA |
| **RF-32** | Fase 2 do Root.IA: a busca híbrida do RootFlow vira `Root.Ia.Search.Postgres`, com o cortador de seções e o reordenador do MedlyCare como opção |

O RootFlow **segue na OpenAI** (decisão de 13/11/2026). Trocar de provedor só com RF-20 e estimativa de custo na mão.

### Onda 4: canais e integrações

| Card | O quê |
|---|---|
| **RF-40** | WhatsApp: perguntar à base pelo WhatsApp (API oficial da Meta, número verificado, vínculo do número ao membro) |
| **RF-41** | API pública com chaves por workspace (criar, revogar, escopo, limite) |
| **RF-42** | Painel de auditoria para Proprietário e Admin (usa o RF-16) |
| **RF-43** | Slack e Microsoft Teams |
| **RF-44** | Treinamento como adicional pago no Stripe, ligando o `training_enabled` sozinho pelo webhook |

### Onda 5: escala e organização

| Card | O quê |
|---|---|
| **RF-50** | Organização com vários workspaces (modelo B.2): um contrato, várias equipes, créditos compartilhados ou separados |
| **RF-51** | Login único corporativo (SSO, Microsoft e Google) |
| **RF-52** | Ambiente de staging (Railway e Vercel separados, banco próprio, Stripe em modo teste) |
| **RF-53** | Testes ponta a ponta no navegador (cadastro, upload, chat, checkout em modo teste) |
| **RF-54** | Limpeza de i18n (textos soltos sem tradução, inglês e português misturados) |
| **RF-55** | Dividir o `Program.cs` (mais de 1.700 linhas de endpoints) em arquivos por área |

---

## 3. Caminho crítico fora do código

| Item | Quem | Destrava |
|---|---|---|
| Call e proposta do Grupo Renac | Matheus | RF-10 a RF-16 |
| Produtos Enterprise no Stripe e variáveis no Railway | Matheus | RF-10 |
| Revisão jurídica de privacidade, termos e DPA | Advogado de proteção de dados | RF-15 |
| Decidir repositório privado | Matheus | RF-05 |
| Aprovar as decisões do Root.IA | Matheus | Onda 3 |
| Nome público (Nativa Labs) com especialista em marca | Matheus | Copy de cliente; código não muda antes disso |
| Compartilhar a página do RootFlow no Notion com a integração | Matheus | Registro de decisões no Notion |

---

## 4. Qualidade e segurança (vale para todo card)

- **CI verde para mesclar.** Testes unitários e de integração em modo Fake, sem credencial.
- **Nada de segredo no código.** Só variável de ambiente (`ROOTFLOW_*` para o que é sensível, `Secao__Chave` para configuração).
- **Webhook com assinatura conferida e idempotente.** Falha explícita, nunca engolir erro.
- **Isolamento por workspace** em toda consulta. Teste com dois workspaces para cada endpoint novo.
- **Log sem conteúdo.** Pergunta, resposta e texto de documento nunca vão para log nem para o Sentry; só IDs, contagem e tempo.
- **Nunca inventar payload de API.** Integração nova começa pela documentação oficial.
- **Copy de cliente sem hífen e sem travessão.**
- **Decisão documentada** no CLAUDE.md ou nos docs do projeto, com o porquê.

---

## 5. Dependências em uma linha

RF-01 → Root.IA (Onda 3) · RF-10 → merge do PR #12 → contrato Renac · RF-11, RF-12, RF-14 → onboarding Renac · RF-16 → RF-42 · RF-20 → RF-30 e qualquer troca de modelo · RF-52 → RF-53 · RF-41 → RF-40 e RF-43.
