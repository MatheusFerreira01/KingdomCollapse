## Purpose

Define a escada de dificuldade que dá longevidade ao jogo e que é o que torna seguro conceder poder permanente na árvore de meta: cada vitória abre um degrau mais duro, e o degrau é calibrado supondo o poder que o jogador já acumulou.

## ADDED Requirements

### Requirement: Níveis de dificuldade
O jogo SHALL ter uma sequência ordenada de níveis de dificuldade. O jogador SHALL começar no primeiro nível e SHALL poder escolher qualquer nível já desbloqueado ao iniciar uma run.

#### Scenario: Começa no primeiro nível
- **WHEN** um perfil novo é criado
- **THEN** apenas o primeiro nível está disponível

#### Scenario: Jogar num nível anterior é permitido
- **WHEN** o jogador desbloqueou o nível 3
- **THEN** ele pode iniciar uma run em qualquer nível de 1 a 3

### Requirement: Vitória desbloqueia o próximo nível
Vencer uma run SHALL desbloquear o próximo nível de dificuldade, e apenas a vitória SHALL desbloquear. O desbloqueio SHALL persistir entre sessões.

#### Scenario: Vencer abre o degrau seguinte
- **WHEN** o jogador vence uma run no nível 2
- **THEN** o nível 3 passa a estar disponível

#### Scenario: Colapsar não abre nada
- **WHEN** o jogador colapsa numa run
- **THEN** nenhum nível novo é desbloqueado, mesmo com pontuação alta

#### Scenario: O desbloqueio sobrevive ao fechamento do jogo
- **WHEN** o jogador desbloqueia um nível, fecha o jogo e reabre
- **THEN** o nível continua disponível

### Requirement: Cada nível endurece algo declarado
Cada nível SHALL declarar o que endurece em relação ao anterior — número de rivais, força das campanhas, escassez inicial ou pressão econômica — e o jogador SHALL poder ler isso antes de escolher o nível.

#### Scenario: O jogador sabe no que está se metendo
- **WHEN** o jogador escolhe um nível na tela de seleção
- **THEN** a diferença daquele nível para o anterior é exibida em texto

#### Scenario: Níveis são monotonicamente mais duros
- **WHEN** dois níveis consecutivos são comparados
- **THEN** o mais alto exige mais do jogador em ao menos um eixo, e não é mais fácil em nenhum

### Requirement: A escada absorve o poder permanente
Cada nível SHALL ser calibrado supondo o poder que a árvore de meta concede até aquele ponto, de modo que acumular buffs permanentes não torne os níveis altos triviais.

#### Scenario: Nível alto continua exigente com árvore avançada
- **WHEN** um jogador com boa parte da árvore comprada joga o nível mais alto desbloqueado
- **THEN** a run continua sendo perdível, e vencer continua exigindo boas decisões

#### Scenario: Progresso não substitui perícia
- **WHEN** dois jogadores com a mesma árvore jogam o mesmo nível
- **THEN** o resultado varia conforme as decisões da run, e não é determinado pela árvore

### Requirement: Teto de poder por nível
A validação de catálogo SHALL verificar que o poder total concedido pela árvore de meta não excede o teto declarado para o nível em que ele se torna disponível.

#### Scenario: Árvore dentro do teto passa
- **WHEN** o catálogo de nós é validado e o poder acumulado cabe no teto de cada nível
- **THEN** nenhuma violação é reportada

#### Scenario: Árvore poderosa demais é reprovada
- **WHEN** o poder acumulado da árvore excede o teto do nível
- **THEN** a validação reporta a violação, apontando o nível e o excedente
