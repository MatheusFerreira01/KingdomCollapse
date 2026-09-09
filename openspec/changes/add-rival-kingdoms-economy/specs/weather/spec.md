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

## ADDED Requirements

### Requirement: Clima pode pressionar a comida
Um clima SHALL poder aumentar o consumo de comida do dia ou reduzir sua produção, dentro do teto de severidade declarado, e o efeito SHALL ser visível na previsão.

#### Scenario: Clima duro aparece na previsão de comida
- **WHEN** a previsão indica um clima que reduz a comida
- **THEN** o jogador vê o impacto no saldo previsto de comida antes do dia chegar
