# Checklist de lançamento na Steam

Documento de trabalho. O objetivo declarado do projeto é aprender este pipeline, então cada item traz o que é, o que custa e quando fazer.

> **Atualização de 2026-09-09.** O projeto deixou de ser "jogo pequeno para gerar renda rápido" e passou a ser o jogo completo, bem feito (ver `roadmap.md`). O pipeline abaixo continua valendo inteiro — o que muda é que a **demo sai bem mais tarde**, depois das raças e do onboarding. A página, essa continua indo ao ar cedo.

**Regra de ouro do cronograma**: a página da Steam deve ir ao ar **muito antes** do jogo ficar pronto. Wishlist acumulada antes do lançamento é o principal fator de visibilidade no dia 1. Página no ar cedo é a decisão de marketing mais barata e mais determinante do projeto.

## 1. Conta e burocracia

| Item | O que é | Custo | Quando |
|---|---|---|---|
| Conta Steamworks | Cadastro de parceiro | grátis | Antes de tudo |
| Steam Direct | Taxa por produto, recuperável após US$ 1.000 em vendas brutas | US$ 100 | Ao decidir o nome final |
| Formulário fiscal (W-8BEN) | Tratado Brasil–EUA; sem ele a retenção é 30% | grátis | Junto do Direct |
| Dados bancários | Conta para repasse | grátis | Junto do Direct |
| Verificação de identidade | Documento + espera de aprovação | grátis | Conta com ~1 semana |

Prazo total até poder criar a página: **1 a 3 semanas**, quase tudo espera de aprovação. Começar cedo, é tempo morto que não depende do jogo.

## 2. Página da loja

Requisitos de arte (medidas oficiais, conferir antes de produzir):

| Asset | Tamanho | Onde aparece |
|---|---|---|
| Header capsule | 460×215 | Listas, wishlist |
| Small capsule | 231×87 | Busca |
| Main capsule | 616×353 | Destaques |
| Vertical capsule | 374×448 | Promoções sazonais |
| Library capsule | 600×900 | Biblioteca do jogador |
| Library hero | 3840×1240 | Topo da biblioteca |
| Screenshots | 1920×1080, mín. 5 | Página |
| Trailer | 1920×1080 | Página |

Texto: descrição curta (~300 caracteres, é o que aparece na busca), descrição longa, tags, requisitos de sistema, idiomas.

**Ordem de prioridade quando o tempo apertar**: descrição curta > header capsule > trailer > screenshots > resto. A descrição curta e a capsule são o que decide o clique.

Aprovação da página leva de 2 a 5 dias úteis. Prever ao menos uma rodada de correção.

## 3. Demo

- Demo tem página própria e wishlist própria, e aparece na página do jogo principal.
- Deve terminar no ponto mais interessante, não no mais completo. Para este jogo: uma run limitada a ~15 dias, com uma raça, terminando num Colapso encenado.
- É pré-requisito prático para o Next Fest.

## 4. Next Fest

- Evento de demos, algumas vezes por ano. Só se participa **uma vez** por jogo — não gastar cedo demais.
- Inscrição costuma fechar semanas antes do evento.
- Participar com a página nova e sem wishlist acumulada desperdiça o evento. O ideal é chegar nele já com tração.
- Regra prática: entrar no Next Fest **de 1 a 3 meses antes do lançamento**, com demo polida.

## 5. Build e lançamento

- Upload via SteamPipe (`steamcmd`), com depots e branches.
- Build de review precisa estar no ar **pelo menos 2 semanas** antes da data pretendida — a Steam exige esse prazo antes de permitir lançar.
- Testar a branch `default` numa conta limpa antes de liberar.
- Definir preço, região e desconto de lançamento (o desconto de estreia é permitido; mudança de preço tem carência).

## 6. Depois do lançamento

- Responder às primeiras reviews. A proporção de reviews positivas dentro das primeiras semanas define a visibilidade a longo prazo.
- Patch rápido para o que aparecer nas primeiras 48h.
- Participar dos festivais sazonais e do Steam Awards nomeáveis.

## 7. Cronograma alvo

| Fase | Depende de | Marco |
|---|---|---|
| Conta e Direct | nada | Pode criar produto |
| Página no ar | um GIF decente | **Wishlist começa a contar** |
| Feedback barato | build privada | GIFs, playtest com 3–4 pessoas |
| Demo | raças + onboarding + som | Base para o Next Fest |
| Next Fest | demo polida | Pico de wishlist, e só acontece uma vez |
| Build de review | jogo completo | 2 semanas de espera antes de poder lançar |
| Lançamento | — | — |

As três primeiras linhas não dependem do jogo estar pronto e podem ser feitas agora.

## 8. Decisões em aberto

- **Nome final** (tarefa 13.2). `KingdomCollapse` é provisório. Antes de pagar o Direct: verificar se já existe jogo com o nome na Steam, se o domínio `.com` está livre e se o nome é buscável (evitar palavra genérica demais).
- **Preço**. Faixa de referência para roguelite indie de escopo pequeno: US$ 9,99 a US$ 12,99. Decidir com a demo no ar, olhando comparáveis diretos.
- **Data de lançamento**. Só travar depois de a build de review estar aprovada.

## 9. O que este projeto ainda não tem

- Integração Steamworks no código (conquistas, nuvem, estatísticas) — fora do escopo da fatia vertical, entra em mudança futura.
- Localização. Português e inglês são o mínimo esperado; decidir depois do playtest.
