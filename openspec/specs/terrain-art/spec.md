# terrain-art Specification

## Purpose

Fazer o tipo de cada célula ser reconhecível por aparência real — floresta parece
floresta, ruína parece ruína — em vez de depender de um código de cor sem contexto.

## Requirements

### Requirement: Aparência real por tipo de terreno
Cada tipo de terreno (planície, floresta, mina, rio, ruína) SHALL ter uma aparência
visual distinta e reconhecível como aquele tipo de terreno, substituindo o bloco de
cor chapada.

#### Scenario: Tipos diferentes são distinguíveis sem seleção
- **WHEN** o jogador observa o tabuleiro sem selecionar nenhuma célula
- **THEN** consegue apontar qual célula é floresta e qual é ruína pela aparência

#### Scenario: Terreno arrasado continua identificável
- **WHEN** uma célula é arrasada
- **THEN** a aparência do terreno reflete o dano, distinta da aparência intacta

### Requirement: Descrição do terreno ao passar o mouse
Ao passar o mouse sobre uma célula, o sistema SHALL exibir uma descrição textual do
tipo de terreno e do que ele significa para o jogo, sem exigir clique ou seleção.

#### Scenario: Passar o mouse sobre floresta
- **WHEN** o cursor fica sobre uma célula de floresta sem clicar
- **THEN** uma descrição aparece explicando que é floresta e o que ela produz

#### Scenario: Descrição some ao tirar o mouse
- **WHEN** o cursor deixa de estar sobre a célula
- **THEN** a descrição desaparece
