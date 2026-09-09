# Roteiro

Decisão de 2026-09-09: **fazer o jogo completo, bem feito, mesmo demorando mais.** O critério para decidir escopo deixou de ser "isso atrasa o lançamento?" e passou a ser "isso deixa o jogo melhor?".

Isso não muda o tamanho de cada passo. Continuamos fatiando em mudanças pequenas e revisáveis — "completo" é o destino, não o tamanho de cada entrega. Uma mudança de 110 tarefas não é mais ambiciosa que duas de 55; é só menos revisável.

## Concluído

**`add-core-run-loop`** — arquivada em 2026-09-09. Fatia vertical jogável: núcleo em C# puro sem dependência de engine, ciclo de dia, grid, cartas, efeitos componíveis, relógio de ameaça telegrafado, eventos com orçamento de severidade, clima com previsão, meta-progressão persistida, simulador de balanceamento em lote. 197 testes.

O que ela ensinou, e que define tudo o que vem depois:

- **Um recurso só produz uma estratégia só.** O bot convergia sempre para "acumular e comprar defesa", e o diagnóstico repetia "ouro parado" em toda medição.
- **Ameaça anônima e infinita não dá o que perseguir.** Medimos dias de sobrevivência por quatro rodadas num jogo cuja árvore de meta tinha zero nós.
- **Sem retorno visual não dá para saber se é divertido.** O clímax de cada quatro dias era uma linha de log.

## Em andamento

**`add-rival-kingdoms-economy`** — proposta pronta, 65 tarefas.

Economia de cinco recursos com população como bem e dívida; reinos rivais nomeados com campanha finita e vitória; escada de dificuldade; árvore de meta com poder permanente sob teto; camada de retorno visual; encontros com presença persistente.

## Próximas mudanças

Ordem por dependência, não por importância. Cada uma só faz sentido depois da anterior.

### `add-race-identities`
Raças deixam de ser modificadores e passam a ter verbo próprio, cada uma com pool de cartas.

- **Humanos** — comerciar: convertem recursos entre si, resolvem escassez com dinheiro
- **Anões** — aprofundar: empilham melhorias na mesma célula, jogam alto em pouco território
- **Elfos** — preservar: floresta rende sozinha, mas **não podem construir em floresta**
- **Orcs** — saquear: comida vem da guerra, e a campanha do rival vira fonte de renda

Depende da mudança atual porque exige a economia de recursos e os pontos de extensão que a tarefa 1.11 garante.

### `add-onboarding`
O jogo hoje não ensina nada. Primeira run guiada, texto de regra nas cartas, e a razão de cada recusa dita em português. Pré-requisito de qualquer demo pública.

### `add-audio-and-polish`
Som, música, transições, tela de título. É o que separa "protótipo jogável" de "jogo".

### `add-steam-integration`
Steamworks: conquistas, nuvem, estatísticas. Ver `steam.md` para o pipeline comercial, que corre em paralelo e não depende disto.

## O que corre em paralelo

Não espera nenhuma mudança acima:

- **Página na Steam no ar** assim que houver um GIF decente. Wishlist acumula durante o desenvolvimento, e é o que determina visibilidade no lançamento.
- **Aprender o pipeline da Steam** — Direct, formulários fiscais, capsules, build de review. Dá para fazer inteiro com o jogo em desenvolvimento.
- **Feedback barato e cedo**: GIFs em rede social, playtest com 3 ou 4 pessoas. Dá sinal em semanas sem gastar a carta da demo.

## A demo é bala única

A demo e o Next Fest são gastos uma vez. Quem joga uma demo crua não volta, e o Next Fest só aceita um jogo uma vez.

A demo sai depois de `add-race-identities` e `add-onboarding`, no mínimo. Antes disso ela leria como protótipo, e a primeira impressão estaria gasta.

## Nome

Continua aberto. `KingdomCollapse` é provisório, e precisa ser resolvido antes de pagar o Steam Direct. Ver `steam.md`.
