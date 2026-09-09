# Eventos de fim de dia — rascunho

Rascunho de conteúdo para a tarefa 10.3. Nada aqui é definitivo; a lista existe para ser cortada e reescrita durante o balanceamento.

Regras que todo evento desta lista respeita (ver spec `day-events` e design D10):

- um evento por dia, no máximo
- nunca encerra a run, nunca destrói quadrado construído, nunca agenda ataque
- severidade não escala com o dia
- classe visível ao jogador antes de confirmar

## Orçamento de severidade por classe

| Classe | Ouro | Grid | Deck | Base |
|---|---|---|---|---|
| Positivo | ganho até 60% do ouro atual | até 1 quadrado de graça ou revelado | até 1 carta ganha | cura até 20% |
| Neutro | troca, sem perda líquida | altera terreno, não destrói | embaralha ou troca 1 carta | sem alteração |
| Negativo | perda até 30% do ouro atual | só quadrado vazio, só 1 | até 1 carta removida | dano até 15%, nunca letal |

O ouro é sempre cobrado em fração do saldo atual, nunca em valor fixo — é o que mantém o custo relativo igual no dia 5 e no dia 30.

## Positivos (~6)

| Nome | Efeito | Observação |
|---|---|---|
| Caravana de mercadores | ganha ouro proporcional ao número de quadrados possuídos | recompensa quem expandiu |
| Safra farta | dobra a produção do dia seguinte | telegrafado, dá o que planejar |
| Refugiados | ganha 1 carta aleatória da raça | única fonte de carta grátis fora de marco |
| Veio exposto | revela 3 quadrados não comprados ao redor do território | informação, não poder |
| Terreno cedido | ganha 1 quadrado adjacente vazio de graça | pula o custo escalonado uma vez |
| Reforço de muralha | ganha defesa extra até o próximo ataque resolvido | interage com o relógio sem alterá-lo |

## Neutros (~6)

| Nome | Efeito | Observação |
|---|---|---|
| Cheia do rio | um quadrado de planície vira rio | pode ajudar ou atrapalhar a build |
| Mercador ambulante | troca ouro por uma carta à escolha entre 2 | escolha, custo opcional |
| Nevoeiro | os quadrados revelados voltam a ficar ocultos por 2 dias | atrapalha planejamento, não causa perda |
| Escavação | um quadrado de ruína vira mina | recompensa quem comprou ruína |
| Boato | a próxima ameaça mostra força imprecisa por 1 dia | tensão sem dano |
| Migração | descarta a mão e compra uma mão nova | reroll puro |

## Negativos (~6)

| Nome | Efeito | Observação |
|---|---|---|
| Imposto do reino vizinho | perde fração do ouro | o clássico; custo relativo constante |
| Praga | um edifício não produz no dia seguinte | perde um dia, não o edifício |
| Deserção | remove 1 carta do deck da run | limite de 1, sempre |
| Erosão | um quadrado vazio possuído é destruído | só vazio, nunca construído |
| Incêndio pequeno | dano leve à base | nunca letal, teto de 15% |
| Sabotagem | a energia do dia seguinte cai em 1 | dói no planejamento, não no reino |

## Com escolha (≥3)

Reaproveitam os efeitos acima, em pares onde nenhuma opção é obviamente melhor:

- **Mercenários à porta** — pagar ouro por defesa contra a próxima ameaça, ou recusar e ganhar uma carta
- **Ruína encontrada** — escavar (ganha ouro, perde a produção de um quadrado por 1 dia) ou selar (nada acontece)
- **Peste no gado** — sacrificar a produção de 2 dias, ou tomar dano leve na base

## Ideias descartadas

- **Evento que agenda um ataque imediato** — quebra a telegrafia; toda pressão vem do relógio de ameaça
- **Evento cuja severidade cresce com o dia** — mistura aleatoriedade com escalada, que é a receita de review "aleatório demais"
- **Evento que destrói quadrado construído** — perda grande demais para um sorteio que o jogador não pôde prever

## Em aberto

- Frequência: evento todo dia, ou dias sem evento? Todo dia é mais legível; dias vazios dão respiro. Decidir no playtest (tarefa 12.3)
- Pesos por classe ao longo da run: fixos, ou mais positivos no começo para o onboarding?
