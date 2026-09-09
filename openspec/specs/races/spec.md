# races Specification

## Purpose
Define a identidade jogável escolhida antes da run: cada raça altera regras concretas dos outros sistemas — economia, expansão, cartas e defesa — em vez de apenas ajustar números, dando a cada uma um jeito próprio de jogar.

## Requirements

### Requirement: Seleção antes da run
O jogador SHALL escolher exatamente uma raça antes de a run começar, entre as raças desbloqueadas, e a raça escolhida SHALL permanecer fixa até o Colapso.

#### Scenario: Raça é escolhida na abertura
- **WHEN** o jogador inicia uma nova run
- **THEN** a tela de seleção de raça é apresentada e a run só começa após uma raça ser confirmada

#### Scenario: Raça bloqueada não é escolhível
- **WHEN** uma raça ainda não foi desbloqueada na meta-progressão
- **THEN** ela aparece marcada como bloqueada e não pode ser selecionada

#### Scenario: Raça não muda no meio da run
- **WHEN** a run está em andamento
- **THEN** não existe ação disponível que troque a raça do jogador

### Requirement: Raça define o estado inicial
Cada raça SHALL definir seu ouro inicial, sua integridade de base inicial, seu tamanho de mão, sua energia diária e seu conjunto de cartas iniciais.

#### Scenario: Estado inicial vem da raça
- **WHEN** uma run começa com uma raça escolhida
- **THEN** ouro, integridade, tamanho de mão, energia e cartas iniciais correspondem exatamente aos valores declarados por aquela raça

### Requirement: Raça altera pelo menos uma regra
Cada raça SHALL modificar ao menos uma regra de comportamento dos sistemas de grid, cartas, economia, eventos ou ameaças — e não apenas valores numéricos.

#### Scenario: Regra própria dos Anões
- **WHEN** o jogador joga como Anões
- **THEN** o custo de comprar quadrados é reduzido, mas o território não pode se afastar do Salão do Reino além do raio declarado pela raça

#### Scenario: Regra própria dos Orcs
- **WHEN** o jogador joga como Orcs e passa um dia inteiro sem resolver um ataque nem saquear
- **THEN** ele perde ouro pela regra de estagnação da raça

#### Scenario: Regra própria dos Elfos
- **WHEN** o jogador joga como Elfos
- **THEN** quadrados de floresta possuídos produzem mesmo sem edifício, e a construção de edifícios leva um dia a mais para ficar pronta

### Requirement: Restrição de conteúdo por raça
Uma raça SHALL poder restringir quais cartas, edifícios e eventos entram nos pools da run, e o conteúdo restrito SHALL nunca aparecer numa run daquela raça.

#### Scenario: Carta proibida não aparece
- **WHEN** uma carta é declarada indisponível para a raça escolhida
- **THEN** ela não entra no deck inicial nem em nenhuma recompensa daquela run
