# hud-icons Specification

## Purpose

Reduzir o HUD a ícone reconhecível de relance, tirando o peso de leitura de texto que
hoje esconde o estado do reino atrás de linhas de números.

## Requirements

### Requirement: Recursos exibidos como ícone e valor
Cada recurso (ouro, madeira, pedra, comida, população), energia e defesa SHALL ser
exibido com um ícone identificável e o valor numérico ao lado, sem rótulo de texto
como identificação primária.

#### Scenario: Recurso em falta continua destacável
- **WHEN** uma ação é recusada por falta de um recurso
- **THEN** o ícone e o valor daquele recurso são destacados, sem depender de texto
  para apontar qual é

#### Scenario: Variação prevista continua visível
- **WHEN** o painel mostra o saldo previsto do dia
- **THEN** a variação aparece junto do ícone do recurso correspondente

### Requirement: Relógio de ameaça no cabeçalho
O dia atual e os dias restantes até a próxima ameaça anunciada SHALL aparecer juntos
no cabeçalho superior, com o dia e o ícone de ameaça lado a lado.

#### Scenario: Nenhuma ameaça anunciada
- **WHEN** não há ataque anunciado
- **THEN** o cabeçalho indica a ausência de forma clara, sem exibir uma contagem
  inválida

### Requirement: Mudança na data da ameaça é animada
Quando um evento muda a data de uma ameaça anunciada, o cabeçalho SHALL mostrar uma
animação numérica indicando a variação: verde e positiva quando a ameaça é adiada,
vermelha e negativa quando é antecipada. `ThreatClock.Delay` hoje só move a data pra
mais tarde — a spec de `threat-clock` garante que a antecedência mínima nunca encurta
— então o caso vermelho não tem gatilho no sistema atual; a UI aceita os dois sinais
para não precisar mudar de novo se um efeito futuro passar a antecipar.

#### Scenario: Ameaça adiada
- **WHEN** um evento empurra a data de uma ameaça anunciada para mais tarde
- **THEN** o cabeçalho anima um número verde positivo igual aos dias adicionados
