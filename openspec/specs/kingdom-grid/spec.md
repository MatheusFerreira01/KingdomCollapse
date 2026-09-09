# kingdom-grid Specification

## Purpose
Define o território do jogador: um grid de quadrados que começa com uma única célula e cresce por compra de células adjacentes, onde cada célula tem um terreno que restringe e potencializa o que pode ser construído nela.

## Requirements

### Requirement: Território inicial
O reino SHALL começar com exatamente um quadrado possuído, contendo o Salão do Reino, que representa a integridade da base.

#### Scenario: Reino de uma célula
- **WHEN** a run inicia
- **THEN** o jogador possui um quadrado, ocupado pelo Salão do Reino, e nenhum outro

### Requirement: Compra de quadrado adjacente
O jogador SHALL poder comprar, com ouro e durante o Planejamento, qualquer quadrado ortogonalmente adjacente a um quadrado que já possua. Quadrados não adjacentes SHALL permanecer indisponíveis.

#### Scenario: Compra válida
- **WHEN** o jogador tem ouro suficiente e seleciona um quadrado adjacente ao seu território
- **THEN** o ouro é debitado, o quadrado passa a ser possuído e seu terreno é revelado

#### Scenario: Compra sem ouro é rejeitada
- **WHEN** o jogador seleciona um quadrado adjacente mas não tem ouro suficiente
- **THEN** a compra é rejeitada, nenhum ouro é debitado e o motivo é comunicado

#### Scenario: Quadrado distante não é comprável
- **WHEN** o jogador seleciona um quadrado que não faz fronteira com o território
- **THEN** o quadrado não é oferecido para compra

### Requirement: Custo escalonado de expansão
O custo de comprar um quadrado SHALL aumentar conforme o número de quadrados já possuídos, de modo que expandir sem economia de suporte se torne inviável.

#### Scenario: Segundo quadrado custa menos que o décimo
- **WHEN** o jogador compara o custo do próximo quadrado possuindo 1 quadrado e possuindo 9
- **THEN** o custo com 9 quadrados é estritamente maior

### Requirement: Terrenos
Cada quadrado SHALL ter exatamente um terreno entre planície, floresta, mina, rio e ruína. O terreno SHALL determinar quais edifícios podem ser construídos ali e SHALL ser desconhecido até a compra, salvo quando revelado por um efeito ou pela revelação inicial do território.

#### Scenario: Terreno revelado na compra
- **WHEN** um quadrado é comprado
- **THEN** seu terreno é revelado e passa a restringir a construção naquele quadrado

### Requirement: Revelação inicial do primeiro anel
Os quadrados ortogonalmente adjacentes ao Salão do Reino SHALL nascer revelados no início da run. Todo quadrado além desse primeiro anel SHALL permanecer oculto até ser comprado ou revelado por um efeito.

#### Scenario: Primeira decisão da run é informada
- **WHEN** a run começa
- **THEN** o jogador vê o terreno das 4 células vizinhas ao Salão e escolhe a direção da primeira expansão sabendo o que está comprando

#### Scenario: Segundo anel continua oculto
- **WHEN** o jogador compra uma célula do primeiro anel
- **THEN** as células recém-fronteiriças além do primeiro anel aparecem como compráveis, mas com terreno oculto

#### Scenario: Edifício incompatível é bloqueado
- **WHEN** o jogador tenta construir um edifício que exige floresta em um quadrado de mina
- **THEN** a construção é rejeitada e o requisito de terreno é comunicado

#### Scenario: Revelação antecipada por efeito
- **WHEN** um efeito de carta ou evento revela um quadrado ainda não comprado
- **THEN** o terreno desse quadrado passa a ser visível sem que ele seja possuído

### Requirement: Ocupação por edifício
Cada quadrado SHALL comportar no máximo um edifício por vez. Construir em um quadrado ocupado SHALL exigir demolição explícita do edifício existente.

#### Scenario: Quadrado ocupado recusa novo edifício
- **WHEN** o jogador tenta construir em um quadrado que já tem edifício
- **THEN** a construção é rejeitada até que o edifício existente seja demolido

### Requirement: Sinergia de adjacência
Edifícios SHALL poder modificar sua produção com base nos terrenos e edifícios ortogonalmente adjacentes, e a produção resultante de cada quadrado SHALL ser inspecionável pelo jogador antes do Fim do Dia.

#### Scenario: Bônus de adjacência aplicado
- **WHEN** uma Serraria está adjacente a dois quadrados de floresta
- **THEN** sua produção é maior do que a de uma Serraria sem floresta adjacente, e a diferença é exibida no detalhe do quadrado

### Requirement: Destruição de quadrado
Quadrados SHALL poder ser destruídos por ameaças e eventos. Um quadrado destruído SHALL perder seu edifício e parar de produzir, mas SHALL permanecer possuído e reconstruível.

#### Scenario: Quadrado destruído para de produzir
- **WHEN** um ataque destrói um quadrado com uma Fazenda
- **THEN** a Fazenda é removida, o quadrado não produz nada e continua possuído

### Requirement: Reparo de quadrado destruído
O jogador SHALL poder reparar um quadrado destruído gastando ouro, durante o Planejamento, sem depender de carta. Um quadrado reparado SHALL voltar a aceitar construção.

#### Scenario: Reparo devolve o quadrado ao jogo
- **WHEN** o jogador tem ouro suficiente e repara um quadrado destruído
- **THEN** o ouro é debitado, o quadrado deixa de estar destruído e pode receber um edifício

#### Scenario: Reparo sem ouro é rejeitado
- **WHEN** o jogador tenta reparar sem ouro suficiente
- **THEN** o reparo é recusado e o quadrado continua destruído

#### Scenario: Destruição não é amputação permanente
- **WHEN** todos os quadrados construídos do jogador são destruídos e ele ainda tem ouro
- **THEN** existe uma ação disponível que devolve o reino ao jogo, sem depender de sorteio de carta
