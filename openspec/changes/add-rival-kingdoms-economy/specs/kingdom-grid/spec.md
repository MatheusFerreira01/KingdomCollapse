## MODIFIED Requirements

### Requirement: Custo escalonado de expansão
O custo de comprar um quadrado SHALL aumentar conforme o número de quadrados já possuídos, e SHALL ser pago em ouro. O que impede a expansão descontrolada SHALL ser a capacidade da economia de operar e alimentar o território, e não apenas o preço da terra.

#### Scenario: Segundo quadrado custa menos que o décimo
- **WHEN** o jogador compara o custo do próximo quadrado possuindo 1 quadrado e possuindo 9
- **THEN** o custo com 9 quadrados é estritamente maior

#### Scenario: Comprar terra não basta para crescer
- **WHEN** o jogador compra quadrados sem população nem comida para operá-los
- **THEN** os quadrados ficam ociosos e não aumentam produção nem defesa

### Requirement: Ocupação por edifício
Cada quadrado SHALL comportar no máximo um edifício por vez. Construir SHALL custar madeira e pedra conforme a definição do edifício, e construir em um quadrado ocupado SHALL exigir demolição explícita do edifício existente.

#### Scenario: Quadrado ocupado recusa novo edifício
- **WHEN** o jogador tenta construir em um quadrado que já tem edifício
- **THEN** a construção é rejeitada até que o edifício existente seja demolido

#### Scenario: Construir consome materiais
- **WHEN** o jogador constrói um edifício que exige madeira e pedra
- **THEN** os dois recursos são debitados, e a construção é recusada se qualquer um deles faltar

#### Scenario: A recusa diz o que falta
- **WHEN** a construção é recusada por falta de material
- **THEN** o motivo informa qual recurso está faltando e quanto

### Requirement: Sinergia de adjacência
Edifícios SHALL poder modificar sua produção com base nos terrenos e edifícios ortogonalmente adjacentes, por recurso, e a produção resultante de cada quadrado SHALL ser inspecionável pelo jogador antes do Fim do Dia.

#### Scenario: Bônus de adjacência aplicado
- **WHEN** uma Serraria está adjacente a dois quadrados de floresta
- **THEN** sua produção de madeira é maior do que a de uma Serraria sem floresta adjacente, e a diferença é exibida no detalhe do quadrado

#### Scenario: Adjacência é por recurso
- **WHEN** um edifício recebe bônus de adjacência
- **THEN** o bônus se aplica ao recurso declarado, e não à produção total do quadrado
