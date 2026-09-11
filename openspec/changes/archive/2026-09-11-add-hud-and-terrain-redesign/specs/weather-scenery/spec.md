## Purpose

Tornar o clima um elemento permanente e compreensível do jogo, em vez de um aviso que
aparece um instante e some, e fazer o cenário reagir visualmente a ele e aos
bônus/penalidades de célula.

## ADDED Requirements

### Requirement: Clima como elemento persistente
O clima do dia SHALL ser exibido como um ícone fixo no canto superior direito da tela
durante todo o dia, em vez de uma mensagem temporária.

#### Scenario: Ícone permanece visível
- **WHEN** o dia está em andamento
- **THEN** o ícone do clima de hoje continua visível até o fim do dia

### Requirement: Tooltip do clima com previsão
Ao passar o mouse sobre o ícone de clima, o sistema SHALL exibir o que o clima de
hoje faz, e uma indicação apontando o clima previsto para amanhã.

#### Scenario: Hover explica o efeito de hoje
- **WHEN** o cursor fica sobre o ícone de clima
- **THEN** aparece uma descrição do efeito do clima de hoje

#### Scenario: Previsão de amanhã é distinguível da de hoje
- **WHEN** o tooltip de clima está visível
- **THEN** o clima de amanhã aparece marcado como previsão, não como o clima atual

### Requirement: Cenário reage ao clima
A iluminação da cena SHALL mudar de acordo com o clima do dia, e uma partícula
característica de cada clima SHALL estar presente na cena enquanto ele durar.

#### Scenario: Sol altera a luz da cena
- **WHEN** o clima do dia é de sol
- **THEN** a luz da cena fica mais forte e amarelada em relação ao clima neutro

#### Scenario: Cada clima tem sua partícula
- **WHEN** o clima do dia é chuva, seca ou sol
- **THEN** a cena mostra a partícula correspondente (chuva caindo, vento seco, brilho
  de sol) enquanto aquele clima durar

### Requirement: Célula com bônus ou penalidade é marcada por partícula
Uma célula sob efeito de bônus de produção SHALL exibir uma partícula dourada, e uma
célula sob efeito de penalidade SHALL exibir uma partícula preta, enquanto o efeito
durar.

#### Scenario: Bônus visível sem seleção
- **WHEN** uma célula está sob efeito de bônus
- **THEN** a partícula dourada aparece sobre ela sem precisar selecioná-la

#### Scenario: Penalidade visível sem seleção
- **WHEN** uma célula está sob efeito de penalidade
- **THEN** a partícula preta aparece sobre ela sem precisar selecioná-la

#### Scenario: Partícula some com o efeito
- **WHEN** o efeito de bônus ou penalidade termina
- **THEN** a partícula correspondente deixa de aparecer sobre a célula
