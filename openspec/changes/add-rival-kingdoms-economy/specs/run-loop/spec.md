## MODIFIED Requirements

### Requirement: Produção diária
Na fase de Resolução, o sistema SHALL aplicar a produção de todos os recursos dos edifícios operados do reino, e em seguida o consumo de comida da população, antes de qualquer evento ou ataque daquele dia.

#### Scenario: Produção precede ameaça
- **WHEN** um dia tem produção e também um ataque agendado
- **THEN** os recursos são creditados antes de o ataque ser resolvido

#### Scenario: Consumo vem depois da produção
- **WHEN** o dia é resolvido
- **THEN** a comida produzida no dia entra antes de a população comer, e o saldo do dia é exibido de forma discriminada

#### Scenario: Edifício ocioso não produz
- **WHEN** um edifício está sem trabalhadores suficientes
- **THEN** ele não contribui com nenhum recurso na produção do dia

### Requirement: Colapso encerra a run
A run SHALL terminar em Colapso quando a integridade da base chegar a zero ou quando o jogador perder o último quadrado de pé. O Colapso SHALL ser apresentado como derrota da run, com placar, distinto da Vitória.

#### Scenario: Integridade zerada encerra a run
- **WHEN** a integridade da base chega a zero durante a Resolução
- **THEN** a run termina imediatamente e a tela de Colapso é exibida com o placar

#### Scenario: Perda do último quadrado encerra a run
- **WHEN** o último quadrado de pé do jogador é destruído
- **THEN** a run termina em Colapso

#### Scenario: Fome não causa Colapso
- **WHEN** a população chega a zero por falta de comida
- **THEN** a run continua, e o Colapso só acontece pelas duas condições acima

### Requirement: Pontuação de run
Ao encerrar, o sistema SHALL calcular uma pontuação a partir do desfecho, dos rivais derrotados, do nível de dificuldade, do reino construído e dos marcos alcançados, e SHALL converter essa pontuação em moeda de meta-progressão.

#### Scenario: Placar é gerado e creditado
- **WHEN** a run termina, por Vitória ou por Colapso
- **THEN** o placar exibe cada componente da pontuação separadamente e a moeda de meta correspondente é creditada ao perfil do jogador

#### Scenario: Rivais derrotados valem mais que dias
- **WHEN** duas runs colapsam com o mesmo reino, uma tendo derrotado dois rivais e outra nenhum
- **THEN** a que derrotou rivais recebe pontuação maior

#### Scenario: Run mais longa vale mais
- **WHEN** duas runs terminam com o mesmo reino construído e os mesmos rivais derrotados, mas uma durou mais dias
- **THEN** a run mais longa recebe pontuação maior

#### Scenario: Dificuldade maior vale mais
- **WHEN** duas runs alcançam o mesmo resultado em níveis diferentes
- **THEN** a do nível mais alto recebe pontuação maior

### Requirement: Duração alvo da run
O ritmo da run SHALL ser calibrado para que uma campanha completa seja vencida por um jogador competente dentro de 25 a 35 minutos. A duração SHALL ser medida pela campanha, e não por um número fixo de dias.

#### Scenario: A campanha define a duração
- **WHEN** o ritmo do nível inicial é medido
- **THEN** derrotar todos os rivais leva um número de dias compatível com a sessão alvo

#### Scenario: A run não se estende indefinidamente
- **WHEN** o jogador evita o confronto e apenas acumula
- **THEN** a campanha do rival vigente continua avançando e a run chega a um desfecho

#### Scenario: Escalada garante o fim
- **WHEN** o jogador sobrevive a muitos ataques sem derrotar o rival vigente
- **THEN** os ataques seguintes da campanha continuam ficando mais fortes, de modo que a run não pode ser sustentada indefinidamente

## ADDED Requirements

### Requirement: Vitória encerra a run
A run SHALL terminar em Vitória quando todos os rivais do nível forem derrotados. A Vitória SHALL ser apresentada como triunfo, com placar próprio, e SHALL desbloquear o próximo nível de dificuldade.

#### Scenario: Vitória é um desfecho distinto
- **WHEN** o último rival é derrotado
- **THEN** a run termina em Vitória, e a tela de fim é distinta da tela de Colapso

#### Scenario: Vitória desbloqueia progresso
- **WHEN** a run termina em Vitória
- **THEN** o próximo nível de dificuldade é desbloqueado e a moeda de meta é creditada
