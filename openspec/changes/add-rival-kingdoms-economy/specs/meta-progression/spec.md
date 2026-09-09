## REMOVED Requirements

### Requirement: Desbloqueio de conteúdo, não de poder
**Reason**: A proibição de poder permanente existia para evitar grind-para-vencer, e era correta enquanto o jogo não tinha escada de dificuldade. Com níveis que endurecem a cada vitória, o poder acumulado é absorvido pelo degrau seguinte, e a proibição passa a custar a sensação de progresso sem comprar segurança nenhuma.

**Migration**: Substituída por "Desbloqueio de conteúdo e de poder com teto", abaixo. O que impede o grind-para-vencer deixa de ser a proibição e passa a ser o teto de poder por nível, verificado pela validação de catálogo.

## ADDED Requirements

### Requirement: Desbloqueio de conteúdo e de poder com teto
Nós da árvore de meta SHALL poder desbloquear conteúdo — cartas, edifícios, eventos, raças, terrenos, rivais — e SHALL poder conceder bônus numéricos permanentes ao estado inicial da run. O poder total concedido SHALL respeitar o teto declarado para o nível de dificuldade em que se torna disponível.

#### Scenario: Nó adiciona conteúdo ao pool
- **WHEN** o jogador compra um nó que desbloqueia uma carta
- **THEN** essa carta passa a poder aparecer em runs futuras

#### Scenario: Nó concede poder dentro do teto
- **WHEN** o jogador compra um nó que concede comida inicial extra
- **THEN** a run seguinte começa com mais comida, e o total concedido pela árvore continua dentro do teto do nível

#### Scenario: Poder além do teto é reprovado
- **WHEN** o catálogo de nós concede poder acima do teto do nível
- **THEN** a validação reporta a violação em vez de aceitar o catálogo
