## ADDED Requirements

### Requirement: Cartas operam sobre os cinco recursos
Cartas SHALL poder custar e conceder qualquer um dos cinco recursos, além de energia. Uma carta que exige um recurso indisponível SHALL ser recusada sem gastar energia, informando o que falta.

#### Scenario: Carta com custo de material é recusada sem o material
- **WHEN** o jogador joga uma carta que exige madeira e não tem madeira suficiente
- **THEN** a carta é recusada, a energia permanece intacta e o motivo informa qual recurso falta

#### Scenario: Carta concede o recurso declarado
- **WHEN** o jogador joga uma carta que concede comida
- **THEN** a comida aumenta e nenhum outro recurso é alterado

### Requirement: Cartas podem realocar população
Cartas SHALL poder mover trabalhadores entre produção e guarnição, e o efeito SHALL ser visível na defesa e na produção do dia.

#### Scenario: Convocação tira gente da produção
- **WHEN** o jogador joga uma carta que guarnece torres
- **THEN** a defesa do dia sobe e a produção prevista cai, com as duas mudanças exibidas
