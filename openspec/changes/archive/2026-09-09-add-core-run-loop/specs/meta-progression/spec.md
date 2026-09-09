## Purpose

Define o que persiste entre runs: uma moeda ganha ao Colapso, gasta numa árvore que desbloqueia conteúdo novo — cartas, edifícios, eventos e raças — e em galhos específicos de cada raça, sem conceder poder numérico direto.

## ADDED Requirements

### Requirement: Moeda de meta persistente
O sistema SHALL creditar moeda de meta ao fim de cada run, com base na pontuação, e SHALL persistir o saldo entre sessões do jogo.

#### Scenario: Saldo sobrevive ao fechamento do jogo
- **WHEN** o jogador conclui uma run, fecha o jogo e reabre
- **THEN** o saldo de moeda de meta é o mesmo que era ao fechar

#### Scenario: Toda run rende algo
- **WHEN** uma run termina em Colapso já no dia 3
- **THEN** o jogador ainda recebe uma quantidade não nula de moeda de meta

### Requirement: Desbloqueio de conteúdo, não de poder
Nós da árvore de meta SHALL desbloquear conteúdo — cartas, edifícios, eventos, raças, terrenos — e SHALL NOT conceder bônus numéricos permanentes ao estado inicial ou à produção da run.

#### Scenario: Nó adiciona conteúdo ao pool
- **WHEN** o jogador compra um nó que desbloqueia uma carta
- **THEN** essa carta passa a poder aparecer em runs futuras, sem alterar nenhum valor inicial da run

#### Scenario: Nó não vende poder direto
- **WHEN** o catálogo de nós da árvore é inspecionado
- **THEN** nenhum nó concede ouro inicial extra, integridade extra ou multiplicador de produção

### Requirement: Pré-requisitos da árvore
Nós SHALL poder declarar pré-requisitos de outros nós e de custo em moeda. Um nó SHALL só poder ser comprado quando todos os seus pré-requisitos estiverem comprados e o saldo cobrir o custo.

#### Scenario: Nó bloqueado por pré-requisito
- **WHEN** o jogador tem saldo suficiente mas não comprou o nó pai
- **THEN** o nó filho não pode ser comprado e o pré-requisito faltante é indicado

#### Scenario: Compra debita o saldo
- **WHEN** o jogador compra um nó disponível
- **THEN** o custo é debitado do saldo e o nó fica permanentemente comprado

### Requirement: Galho por raça
A árvore SHALL conter um galho dedicado a cada raça, cujos nós desbloqueiam conteúdo que só aparece em runs daquela raça, e esses galhos SHALL ser independentes entre si.

#### Scenario: Desbloqueio de raça é exclusivo
- **WHEN** o jogador compra um nó do galho dos Anões
- **THEN** o conteúdo desbloqueado aparece em runs de Anões e não aparece em runs de outras raças

#### Scenario: Progresso de uma raça não gasta o de outra
- **WHEN** o jogador investe fortemente no galho dos Elfos
- **THEN** a disponibilidade dos nós dos outros galhos permanece inalterada

### Requirement: Desbloqueio de raças
Raças além da inicial SHALL ser desbloqueadas por nós da árvore de meta e SHALL só ficar disponíveis na seleção após o desbloqueio.

#### Scenario: Raça aparece após compra
- **WHEN** o jogador compra o nó que desbloqueia os Orcs
- **THEN** os Orcs passam a ser selecionáveis na próxima run

### Requirement: Integridade do save de meta
O sistema SHALL persistir a meta-progressão localmente e SHALL se recuperar de um arquivo ausente ou corrompido iniciando um perfil novo, sem travar o jogo.

#### Scenario: Save ausente
- **WHEN** o jogo inicia e não existe arquivo de meta-progressão
- **THEN** um perfil novo é criado com saldo zero e apenas o conteúdo inicial desbloqueado

#### Scenario: Save corrompido
- **WHEN** o arquivo de meta-progressão não pode ser lido
- **THEN** o jogo informa o problema, preserva o arquivo ilegível para inspeção e segue com um perfil novo
