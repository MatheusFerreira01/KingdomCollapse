## Purpose

Define a economia de ações do jogador dentro de um dia: um deck de run do qual ele compra uma mão, gasta energia para jogar cartas que agem sobre o grid e o reino, e descarta o que sobrou ao encerrar o dia.

## ADDED Requirements

### Requirement: Deck de run
Cada run SHALL ter seu próprio deck, montado no início a partir das cartas iniciais da raça e das cartas desbloqueadas na meta-progressão. Alterações no deck durante a run SHALL valer apenas para aquela run.

#### Scenario: Deck é descartado ao fim da run
- **WHEN** uma run termina e outra é iniciada
- **THEN** o deck da nova run é montado do zero, sem as cartas adquiridas na run anterior

### Requirement: Compra de mão diária
No Início do Dia o sistema SHALL preencher a mão do jogador até o tamanho de mão vigente, comprando do deck. Quando o deck esvaziar, a pilha de descarte SHALL ser embaralhada de volta no deck.

#### Scenario: Mão é reabastecida
- **WHEN** um novo dia começa e a mão está vazia
- **THEN** o jogador recebe cartas até o tamanho de mão vigente

#### Scenario: Deck vazio recicla o descarte
- **WHEN** o deck não tem cartas suficientes para completar a mão
- **THEN** a pilha de descarte é embaralhada e vira o novo deck, e a compra continua

#### Scenario: Sem cartas em lugar nenhum
- **WHEN** deck e descarte estão ambos vazios
- **THEN** a compra para sem erro e o jogador segue o dia com a mão que tiver

### Requirement: Energia diária
O jogador SHALL receber uma quantidade fixa de energia no Início do Dia. Jogar uma carta SHALL custar sua energia, e uma carta SHALL só poder ser jogada se houver energia suficiente. A energia não gasta SHALL ser perdida no Fim do Dia.

#### Scenario: Carta cara é bloqueada
- **WHEN** o jogador tem 1 de energia e tenta jogar uma carta de custo 2
- **THEN** a carta não é jogada, nenhuma energia é gasta e a razão é comunicada

#### Scenario: Energia não acumula
- **WHEN** o jogador encerra o dia com energia sobrando
- **THEN** o dia seguinte começa com a energia cheia, sem acréscimo do que sobrou

### Requirement: Alvo de carta
Cartas que agem sobre o grid SHALL exigir a seleção de um quadrado alvo válido antes de resolver, e SHALL poder ser canceladas sem custo enquanto o alvo não for confirmado.

#### Scenario: Alvo inválido é recusado
- **WHEN** o jogador escolhe como alvo um quadrado que não satisfaz o requisito da carta
- **THEN** o alvo é recusado, a carta permanece na mão e nenhuma energia é gasta

#### Scenario: Cancelamento é gratuito
- **WHEN** o jogador cancela a seleção de alvo
- **THEN** a carta volta para a mão e a energia permanece intacta

### Requirement: Descarte no Fim do Dia
No Fim do Dia todas as cartas restantes na mão SHALL ir para a pilha de descarte, salvo cartas explicitamente marcadas como retidas.

#### Scenario: Mão é limpa
- **WHEN** o dia termina com 3 cartas na mão e nenhuma delas retida
- **THEN** as 3 cartas vão para o descarte e a mão fica vazia

#### Scenario: Carta retida permanece
- **WHEN** o dia termina e uma carta na mão está marcada como retida
- **THEN** essa carta continua na mão no dia seguinte e conta contra o limite de compra

### Requirement: Aquisição de cartas na run
O sistema SHALL conceder novas cartas ao deck da run como recompensa de eventos, de marcos e de edifícios, e SHALL informar ao jogador qual carta entrou no deck.

#### Scenario: Recompensa entra no deck
- **WHEN** um evento concede uma carta ao jogador
- **THEN** a carta é adicionada à pilha de descarte da run e mostrada ao jogador
