## Purpose

Define os encontros: acontecimentos com identidade própria — um minotauro ferido, um dragão que se instala, um herói errante — que deixam algo para trás, encadeiam consequências e oferecem escolhas em que nenhuma opção é obviamente a certa. É deles que vem a variedade que distingue uma run da seguinte.

## ADDED Requirements

### Requirement: Encontro tem identidade
Um encontro SHALL ter nome próprio, texto de apresentação e ser reconhecível como uma criatura, pessoa ou lugar, e não apenas como um ajuste de recurso.

#### Scenario: O jogador sabe com o que está lidando
- **WHEN** um encontro é apresentado
- **THEN** ele traz nome e descrição que identificam quem ou o que apareceu

### Requirement: Escolha assimétrica
Um encontro com escolha SHALL oferecer opções cujos ganhos são de naturezas diferentes, de modo que nenhuma opção seja a melhor em toda situação.

#### Scenario: Curar ou matar recompensam coisas diferentes
- **WHEN** o jogador encontra uma criatura ferida
- **THEN** uma opção rende recursos imediatos e outra rende ajuda futura, e a escolha depende do estado da run

#### Scenario: Nenhuma opção domina
- **WHEN** as opções de um encontro são comparadas em situações diferentes
- **THEN** existe ao menos uma situação em que cada opção é a preferível

### Requirement: Presença persistente
Um encontro SHALL poder deixar uma presença que permanece na run depois de resolvido. Uma presença SHALL agir periodicamente, de forma anunciada, e SHALL ser visível ao jogador enquanto existir.

#### Scenario: O dragão fica e cobra
- **WHEN** o jogador aceita a presença de um dragão em seu território
- **THEN** o dragão permanece na run, cobra tributo em intervalos declarados, e o jogador vê quando o próximo tributo vence

#### Scenario: O aliado fica e ajuda
- **WHEN** o jogador cura uma criatura e ela se junta ao reino
- **THEN** ela passa a contribuir de forma declarada enquanto permanecer

#### Scenario: Presenças são consultáveis
- **WHEN** o jogador está no Planejamento
- **THEN** ele consegue ver todas as presenças ativas e o que cada uma faz

### Requirement: Presença pode ser resolvida
O jogador SHALL poder encerrar uma presença — expulsando, pagando, matando ou cumprindo o que ela pede — e o custo dessa resolução SHALL ser conhecido antes de ser tentado.

#### Scenario: Livrar-se do dragão tem preço conhecido
- **WHEN** o jogador consulta uma presença hostil
- **THEN** ele vê o que custa encerrá-la antes de decidir

#### Scenario: Presença encerrada para de agir
- **WHEN** uma presença é resolvida
- **THEN** ela deixa de agir e some da lista de presenças ativas

### Requirement: Encontros encadeiam
Um encontro SHALL poder agendar outro encontro para um dia futuro, e a continuação SHALL fazer referência à escolha que a originou.

#### Scenario: A escolha volta
- **WHEN** o jogador ajuda uma criatura e o encontro declara uma continuação
- **THEN** o encontro seguinte acontece no dia agendado e reconhece a ajuda anterior

#### Scenario: Cadeia não se perde
- **WHEN** uma continuação está agendada
- **THEN** ela acontece mesmo que outros encontros ocorram no intervalo

### Requirement: Encontros respeitam o orçamento de severidade
Encontros SHALL obedecer aos mesmos tetos de severidade dos eventos de fim de dia, e uma presença SHALL NOT causar, ao longo da run, perda maior que o teto declarado para a sua categoria.

#### Scenario: Encontro não encerra a run
- **WHEN** um encontro ou presença causa dano que zeraria a integridade
- **THEN** a integridade é reduzida no máximo até 1 e a run continua

#### Scenario: Presença hostil tem custo limitado
- **WHEN** uma presença hostil cobra tributo repetidamente
- **THEN** o custo por cobrança respeita o teto da categoria, e o jogador sempre tem como encerrá-la

### Requirement: Encontros não substituem a campanha
Encontros SHALL ser independentes da campanha do rival vigente, e SHALL NOT agendar, cancelar nem resolver ataques de rival.

#### Scenario: Encontro não vira ataque
- **WHEN** qualquer encontro é resolvido
- **THEN** nenhum ataque de rival é criado, adiantado ou cancelado
