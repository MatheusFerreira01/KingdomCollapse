## MODIFIED Requirements

### Requirement: Clima altera produção por terreno
Um clima SHALL poder modificar a produção de recursos de acordo com o terreno de cada quadrado, por recurso, e SHALL poder aplicar um multiplicador à produção total do dia.

#### Scenario: Chuva favorece rio e prejudica obra
- **WHEN** o clima do dia é chuva e o jogador possui um quadrado de rio operado
- **THEN** aquele quadrado produz mais comida que produziria em dia limpo, e construir custa mais material naquele dia

#### Scenario: Seca inverte o favorecimento
- **WHEN** o clima do dia é seca
- **THEN** quadrados de mina produzem mais pedra e fazendas em planície produzem menos comida

#### Scenario: O clima age no recurso do terreno
- **WHEN** um clima favorece floresta
- **THEN** o ganho aparece na madeira, e não num total genérico de produção

### Requirement: Clima pode bloquear ações
Um clima SHALL poder impedir ações específicas do dia, como comprar quadrado ou revelar terreno, e a razão SHALL ser comunicada ao jogador. O primeiro dia da run SHALL NOT ter clima que bloqueie ação, porque não houve véspera em que ele pudesse ter sido previsto.

#### Scenario: Neblina impede expandir
- **WHEN** o clima do dia é neblina e o jogador tenta comprar um quadrado
- **THEN** a compra é recusada e o motivo informa que a neblina impede a expansão hoje

#### Scenario: O primeiro dia nunca bloqueia
- **WHEN** a run começa
- **THEN** o clima do dia 1 não bloqueia nenhuma ação, e climas que bloqueiam só aparecem depois de terem sido previstos na véspera

## ADDED Requirements

### Requirement: Clima pode pressionar a comida
Um clima SHALL poder aumentar o consumo de comida do dia ou reduzir sua produção, dentro do teto de severidade declarado, e o efeito SHALL ser visível na previsão.

#### Scenario: Clima duro aparece na previsão de comida
- **WHEN** a previsão indica um clima que reduz a comida
- **THEN** o jogador vê o impacto no saldo previsto de comida antes do dia chegar
