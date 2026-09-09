## REMOVED Requirements

### Requirement: Escalada de pressão
**Reason**: A força passava a crescer com o número de quadrados possuídos, o que era uma penalidade artificial por expandir — inventada porque nada na economia impedia crescer para sempre. Com população que come e guarnece, o teto de expansão passa a ser econômico, e punir o tamanho vira dupla cobrança.

**Migration**: Substituída por "Escalada da campanha", abaixo, que faz a força crescer dentro da campanha de um rival e entre rivais sucessivos. O termo por célula sai da curva.

## MODIFIED Requirements

### Requirement: Resolução de ataque
Na Resolução, a força do ataque SHALL ser confrontada com a defesa guarnecida do reino. A força excedente SHALL causar perdas no eixo declarado pela identidade do rival. Um ataque cuja força não exceda a defesa SHALL contar como repelido para a campanha.

#### Scenario: Defesa suficiente anula o ataque
- **WHEN** a defesa guarnecida do reino é maior ou igual à força do ataque
- **THEN** nenhuma perda acontece e o ataque conta como repelido para a campanha do rival

#### Scenario: Excedente destrói quadrados
- **WHEN** a força de um rival de assalto excede a defesa do reino
- **THEN** o excedente destrói quadrados a partir da borda do território e o restante reduz a integridade da base

#### Scenario: Excedente econômico não toca o território
- **WHEN** a força de um rival econômico excede a defesa do reino
- **THEN** o excedente atinge comida, população ou outro recurso declarado, sem destruir quadrados

#### Scenario: Defesa não guarnecida não conta
- **WHEN** o reino tem edifícios defensivos sem trabalhadores alocados
- **THEN** eles não somam defesa na resolução, e o ataque é comparado apenas contra a defesa efetivamente guarnecida

#### Scenario: Resultado é explicado
- **WHEN** um ataque é resolvido
- **THEN** o jogador vê a força, a defesa guarnecida, as perdas resultantes e se o ataque contou como repelido

## ADDED Requirements

### Requirement: Escalada da campanha
A força dos ataques SHALL crescer ao longo da campanha de um rival e entre rivais sucessivos, de modo que cada rival exija mais do reino que o anterior. A força SHALL NOT depender do número de quadrados possuídos.

#### Scenario: Ataques ficam mais fortes dentro da campanha
- **WHEN** o primeiro e o último ataque da campanha de um rival são comparados
- **THEN** o último exige mais defesa que o primeiro

#### Scenario: Cada rival exige mais que o anterior
- **WHEN** as campanhas de dois rivais consecutivos são comparadas
- **THEN** a do rival mais tardio exige mais defesa

#### Scenario: Território não é punido por si só
- **WHEN** dois jogadores enfrentam o mesmo ataque, um com 4 quadrados e outro com 12
- **THEN** a força do ataque é a mesma para os dois, e a diferença de resultado vem do que cada reino consegue operar e guarnecer

### Requirement: O relógio é a campanha do rival
As ameaças pendentes SHALL pertencer à campanha do rival vigente, e o relógio SHALL exibir a que campanha cada ataque pertence.

#### Scenario: Relógio mostra a origem do ataque
- **WHEN** o jogador consulta o relógio de ameaça
- **THEN** cada ataque pendente indica de qual rival vem

#### Scenario: Derrotar o rival limpa a campanha dele
- **WHEN** um rival é derrotado
- **THEN** nenhum ataque restante daquela campanha permanece agendado
