## Purpose

Substituir a lista de botões de texto por um menu visual central de construção, que
mostra de relance quais opções cabem no terreno selecionado.

## ADDED Requirements

### Requirement: Menu central de construção
Ao selecionar uma célula construível, o sistema SHALL abrir um menu próprio,
posicionado no centro da tela ou ao lado da célula, exibindo as construções
disponíveis com sua arte.

#### Scenario: Selecionar célula construível abre o menu
- **WHEN** o jogador seleciona uma célula sem edifício e sem estar arrasada
- **THEN** o menu de construção aparece com as opções disponíveis

#### Scenario: Selecionar outra célula fecha o menu anterior
- **WHEN** o jogador seleciona uma célula diferente enquanto o menu está aberto
- **THEN** o menu anterior fecha e, se aplicável, um novo é aberto para a nova célula

### Requirement: Compatibilidade é indicada por cor de fundo
Cada opção do menu de construção SHALL indicar se é compatível com o terreno da
célula selecionada: fundo amarelo quando compatível, sem cor quando não.

#### Scenario: Construção compatível com o terreno
- **WHEN** uma construção pode ser erguida no terreno da célula selecionada
- **THEN** sua opção aparece com fundo amarelo no menu

#### Scenario: Construção incompatível com o terreno
- **WHEN** uma construção não pode ser erguida no terreno da célula selecionada
- **THEN** sua opção aparece sem cor de fundo, distinguível das compatíveis
