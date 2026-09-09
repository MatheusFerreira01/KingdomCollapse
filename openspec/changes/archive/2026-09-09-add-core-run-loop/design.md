## Context

Projeto novo, greenfield, sem código legado. Ver `proposal.md - Why` para a motivação. Restrições que moldam o desenho:

- **Dev solo**, com o projeto MM em pausa. O tempo é o recurso escasso; o objetivo comercial (renda + aprender Steam) só se realiza se o jogo terminar.
- **Trabalho de editor Unity é do Matheus** (cenas, prefabs, importação de assets, preenchimento de ScriptableObjects). O código C# é gerado aqui. Isso empurra o desenho para "código genérico, conteúdo em dados".
- **Decisões já travadas com o usuário**: Unity, 3D baixo-poli isométrico com câmera fixa, run de 25–35 min / ~30 dias, meta-progressão por desbloqueio de conteúdo + galho por raça.
- O jogo é de **turnos e determinístico dentro do dia**: nenhuma simulação em tempo real, nenhuma física. Isso permite manter toda a lógica fora de `MonoBehaviour` e testável.

Requisitos formais estão em `specs/`; este documento não os repete.

## Goals / Non-Goals

**Goals:**
- Lógica de run pura em C#, sem dependência de Unity, coberta por testes de EditMode — resolver um dia inteiro deve ser possível sem abrir uma cena.
- Todo conteúdo (cartas, edifícios, terrenos, eventos, raças, nós de meta) em ScriptableObjects, para o Matheus balancear no Inspector sem recompilar e sem pedir código.
- Uma única fatia vertical jogável: 1 raça, ~15 cartas, ~10 edifícios, ~15 eventos, ~8 nós de meta. Suficiente para gravar GIF/trailer e montar a página da Steam.
- Efeitos de carta e de evento compartilhando o mesmo vocabulário, para que adicionar conteúdo não exija código novo na maior parte dos casos.

**Non-Goals:**
- Sem save/load da run em andamento nesta fatia (apenas a meta-progressão persiste).
- Sem Steamworks, sem conquistas, sem nuvem, sem localização.
- Sem ECS, sem DOTS, sem framework de injeção de dependência, sem addressables.
- Sem animação de personagem: o "3D" é diorama estático com tweens de escala/posição e VFX simples.
- Sem editor de nós customizado para a árvore de meta; a árvore é dado + layout manual.

## Decisions

### D1 — Núcleo de simulação em C# puro, Unity só como camada de apresentação
Toda a regra vive em um assembly `KingdomCollapse.Core` sem `using UnityEngine`. A camada Unity (`KingdomCollapse.Game`) observa o núcleo e desenha. O núcleo expõe comandos (`BuyTile`, `PlayCard`, `EndDay`) e emite eventos de domínio que a view consome.

*Por quê:* balancear um roguelite exige rodar milhares de runs. Com o núcleo puro dá para simular uma run inteira em milissegundos num teste, e detectar "run sempre morre no dia 12" sem jogar. Também torna o `EndDay` — a parte mais propensa a bug de ordem — testável.

*Alternativas:* lógica em MonoBehaviours (rápido de começar, impossível de balancear em massa e de testar); ECS (poder desnecessário para um grid de dezenas de células).

### D2 — Máquina de estados explícita para as fases do dia
`DayPhase` é um enum e a transição é uma máquina de estados que só aceita comandos do jogador em `Planning`. Cada fase é um passo discreto e observável.

*Por quê:* a spec de `run-loop` fixa a ordem produção → evento → ameaça. Ordem implícita espalhada por `Update()` é a origem clássica de bugs de "o ataque comeu o ouro antes de eu receber". A máquina de estados também dá pontos naturais de pausa para animação sem alterar a regra.

*Alternativas:* corrotinas encadeadas (a ordem vira dependência de tempo, e testar exige rodar o motor do Unity).

### D3 — Efeitos como dados componíveis, não como subclasse por carta
Um efeito é uma instância de ScriptableObject com um tipo (`GainGold`, `BuildOn`, `DestroyTile`, `AddDefense`, `DrawCards`, `RevealTile`, `AddCardToDeck`, ...) e parâmetros. Uma carta é uma lista de efeitos mais um requisito de alvo. Um evento é uma lista de efeitos mais condições de elegibilidade. Cartas e eventos consomem o **mesmo** conjunto de efeitos.

*Por quê:* é o que permite o Matheus criar conteúdo sem código. Também é o que faz o conteúdo crescer barato — a fatia vertical entrega ~15 cartas, mas o jogo comercial precisa de ~60, e escrever 60 classes é inviável para dev solo.

*Alternativas:* uma classe por carta (explode em manutenção); scripting embarcado tipo Lua (poder demais, custo de integração e de debug alto demais para o escopo).

### D4 — Grid em coordenadas axiais inteiras num dicionário esparso, não em array
`Dictionary<Vector2Int, Tile>` com adjacência ortogonal. O território cresce em qualquer direção, sem limite pré-alocado.

*Por quê:* o jogador começa em uma célula e expande livremente; um array exigiria escolher um tamanho máximo arbitrário e recentrar. Dicionário esparso mantém a lógica de "comprável = adjacente a possuído" trivial. O grid nunca passa de algumas centenas de células, então performance não é fator.

*Alternativas:* array 2D fixo (limite arbitrário, recentralização chata); grid hexagonal (mais interessante para adjacência, mas o pedido original é explicitamente "quadrados", e hex complica arte e UI).

### D5 — Aleatoriedade com semente única por run, canais separados por sistema
A run guarda uma semente e deriva geradores independentes para terreno, eventos e ameaças.

*Por quê:* permite reproduzir uma run reportada como bug, escrever testes determinísticos e, mais tarde, oferecer "run diária com semente" — um gancho barato de retenção e de conteúdo para streamer. Canais separados evitam que jogar uma carta a mais desloque toda a sequência de eventos futuros.

*Alternativas:* `UnityEngine.Random` global (não reproduzível, não testável fora do Editor).

### D6 — Ameaça agendada como fila visível, força derivada de fórmula
O relógio mantém uma lista de ameaças pendentes `(diaDeChegada, força, tipo)`, gerada com antecedência. A força vem de uma curva em função do dia e do número de quadrados possuídos, definida em um ScriptableObject de curva.

*Por quê:* a spec exige telegrafia. Manter a fila como dado, e não como sorteio no momento da resolução, é o que torna a antecipação possível e a UI trivial. A curva em SO deixa o balanceamento nas mãos do Matheus.

*Trade-off:* o custo de expandir precisa ficar visível — a força escala com o território, então a UI tem que mostrar "comprar este quadrado aumenta a próxima ameaça" antes da compra, ou o jogador se sente punido por jogar bem.

### D7 — Meta-progressão persistida em JSON no `Application.persistentDataPath`
Um único arquivo com saldo, nós comprados e estatísticas. Arquivo ausente ou corrompido gera perfil novo e renomeia o arquivo ilegível para `.corrupt`.

*Por quê:* volume minúsculo, precisa ser inspecionável durante o desenvolvimento e resistente a crash. `PlayerPrefs` é opaco e frágil; banco embarcado é exagero.

*Trade-off:* o arquivo é editável pelo jogador. Aceitável — jogo single-player, sem ranking online nesta fase.

### D8 — Raça como conjunto de regras nomeadas, consultadas pelos sistemas
Uma raça é um ScriptableObject com valores iniciais, listas de conteúdo permitido/proibido e um conjunto de **modificadores de regra** identificados (ex.: `TileCostMultiplier`, `MaxDistanceFromHall`, `StagnationPenalty`, `ForestProducesWithoutBuilding`, `BuildDelayDays`). Cada sistema consulta os modificadores que lhe dizem respeito; um modificador ausente significa comportamento padrão.

*Por quê:* a spec exige que raça altere regra, não só número. Consulta por modificador nomeado mantém as raças declarativas e impede que a lógica de raça se espalhe em `if (race == Dwarf)` pelo código todo. Espelha a regra que já vale no projeto MM: comportamento tipado, atribuição dinâmica, ausência nunca quebra.

*Alternativas:* herança por raça (rígida, cruza mal com conteúdo desbloqueável); `if` por raça espalhado (dívida imediata).

### D9 — Apresentação 3D isométrica com câmera ortográfica fixa
Câmera ortográfica, ângulo fixo, sem rotação livre. Cada célula é um prefab de bloco baixo-poli com uma sobreposição de edifício. Feedback por tween e VFX, sem rig nem animação esqueletal.

*Por quê:* entrega o apelo visual do 3D pelo custo de produção de um jogo de tabuleiro. Câmera fixa elimina classes inteiras de problema (oclusão, seleção ambígua, enquadramento de UI) e permite arte assimétrica desenhada para um único ângulo. É a alavanca de escopo mais importante do projeto.

*Trade-off:* menos "wow" que câmera livre; mitigado por iluminação, silhueta forte e movimento de câmera curado nos momentos de Colapso e ataque.

### D10 — Duas fontes de pressão separadas: ameaça determinística e evento aleatório
O jogo tem exatamente duas fontes de mudança fora do controle do jogador, e elas não se misturam:

| | Relógio de ameaça | Evento de fim de dia |
|---|---|---|
| Quando aparece | agendado, anunciado ≥2 dias antes | sorteado no fim do dia |
| Papel | pressão que encerra a run | variedade entre runs |
| Escala com o dia | sim, sem teto | não |
| Pode encerrar a run | sim | nunca |
| Severidade | crescente | limitada por orçamento fixo por classe |

Cada classe de evento tem um **orçamento de severidade** declarado em dado (fração máxima de ouro, sem destruição de quadrado construído, no máximo uma carta removida). O orçamento é fixo ao longo da run: um evento negativo custa proporcionalmente o mesmo no dia 5 e no dia 30.

*Por quê:* aleatoriedade e escalada juntas no mesmo canal é o que faz roguelite parecer injusto — o jogador não distingue "joguei mal" de "tomei azar". Separando, ele sempre sabe de onde veio a derrota. O evento continua podendo mudar a run inteira **para melhor** (uma carta forte, um quadrado de graça), porque ganho inesperado não gera a mesma frustração que perda inesperada.

*Alternativa:* eventos escalando com o dia como segunda camada de dificuldade — descartado, é o desenho que gera review de "aleatório demais". Se a dificuldade tardia ficar rasa, sobe a curva da ameaça (D6), não a dos eventos.

*Trade-off:* eventos ficam menos memoráveis por serem leves. Compensado pela variedade — o teto é de severidade, não de estranheza: um evento pode mudar terreno, embaralhar o deck ou revelar mapa sem custar quase nada.

### D12 — Clima como sistema próprio, não como classe de evento
Clima é a condição do dia; evento é o que às vezes acontece nele. São sistemas separados, com pools, sorteio e orçamentos independentes.

*Por quê:* três coisas quebrariam se clima virasse um evento. Ele competiria pelo único slot de evento por dia; o cooldown faria "chuva" sumir por cinco dias, quando clima deve ser contínuo; e a classificação positivo/neutro/negativo não descreve algo que é bom e ruim ao mesmo tempo, que é a definição do clima aqui.

*Consequência que compensa:* separado, o clima pode ter **previsão**. Isso o coloca no mesmo princípio do relógio de ameaça — o jogo avisa, o jogador decide — e transforma "tempestade amanhã, horda depois de amanhã" numa decisão em vez de azar.

*Alternativa:* clima como evento de classe neutra com efeitos mistos. Reaproveitaria todo o código, mas entregaria clima esporádico e sem previsão, que é outro sistema.

### D13 — Clima altera a ameaça na previsão, nunca na chegada
Quando o clima de um dia é previsto, ele já aplica seu efeito sobre as ameaças que chegam naquele dia: reduz a força ou adia o dia de chegada, e o relógio passa a exibir o valor corrigido.

*Por quê:* a spec promete que a força prevista é a força que chega. Aplicar o efeito no dia da chegada faria do número exibido uma mentira, e o jogador perderia a única informação em que ele se apoia para decidir se defende ou expande. Aplicando na previsão, a promessa continua verdadeira e a previsão do tempo ganha valor tático concreto.

*Trade-off:* o relógio muda de número entre um dia e outro, o que exige que a UI destaque a alteração; um número que muda sem aviso seria pior que um número fixo.

### D11 — Estrutura de repositório
```
KingdomCollapse/
  ProjectKC/                 <- projeto Unity
    Assets/
      Scripts/Core/          <- asmdef KingdomCollapse.Core (sem UnityEngine)
      Scripts/Game/          <- asmdef KingdomCollapse.Game (view, input, UI)
      Scripts/Tests/         <- asmdef de EditMode, referencia Core
      Data/                  <- ScriptableObjects de conteúdo
      Art/, Prefabs/, Scenes/
  openspec/                  <- planejamento
  docs/                      <- notas de Steam, marketing, balanceamento
```

## Risks / Trade-offs

- **Escopo do "só mais uma raça" cresce sem fim** → a fatia vertical trava em 1 raça; as outras 3 só entram depois de uma run completa estar divertida e gravável.
- **Balanceamento de roguelite consome mais tempo que a programação** → mitigado por D1 e D5: um harness de simulação em lote roda milhares de runs com IA burra e reporta a distribuição de dia de Colapso; sem isso, o balanceamento vira tentativa e erro manual.
- **Colapso garantido pode frustrar mesmo com placar** → mitigado com marcos visíveis durante a run ("dia 10 alcançado", "12 quadrados") e feedback de progresso de meta na tela de Colapso; a validação real é playtest, não desenho.
- **Escalada de ameaça punindo a expansão desincentiva o loop central** → mitigado por D6: a UI precisa mostrar o custo em pressão antes da compra. Se o playtest mostrar que os jogadores param de expandir, a força passa a escalar principalmente pelo dia.
- **Sistema de efeitos genérico vira genérico demais** → limitar a ~12 tipos de efeito na fatia vertical; adicionar um tipo novo só quando três conteúdos diferentes pedirem.
- **3D isométrico ainda custa mais arte que o previsto** → primeiro passe usa blocos primitivos com material chapado, jogável ponta a ponta; a arte final entra depois que a diversão estiver provada.
- **Dividir atenção com o projeto MM** → risco real de nenhum dos dois terminar; o MM fica arquivado enquanto esta fatia vertical não estiver jogável.

## Migration Plan

Não se aplica: projeto novo, sem usuários e sem dados a migrar. A única consideração de compatibilidade é o formato do save de meta, que carrega um número de versão desde o primeiro dia para permitir migração depois do lançamento.

## Open Questions

- Nome definitivo do jogo (`KingdomCollapse` é provisório). Precisa ser resolvido antes de reservar a página na Steam, não antes de codar.
- Recursos além de ouro (madeira, pedra, comida) entram ou a economia fica em uma moeda só? A fatia vertical usa apenas ouro; a decisão de expandir depende de o playtest mostrar que a economia está rasa.
- Preço final e data de lançamento — dependem do estado do jogo e da resposta da demo.
