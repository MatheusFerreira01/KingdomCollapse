## Purpose

Define os reinos rivais que substituem a ameaça anônima: adversários nomeados, cada um com uma identidade que pressiona um recurso diferente, derrotados ao terem sua campanha repelida, e cuja derrota completa encerra a run em Vitória.

## ADDED Requirements

### Requirement: Rival vigente
A run SHALL ter sempre exatamente um rival vigente enquanto não terminar, e o jogador SHALL saber quem é, qual sua identidade e quantos ataques faltam para derrotá-lo.

#### Scenario: O rival é conhecido desde o começo
- **WHEN** a run começa
- **THEN** o primeiro rival é apresentado com nome, identidade e o número de ataques da sua campanha

#### Scenario: Progresso da campanha é visível
- **WHEN** o jogador está no Planejamento
- **THEN** ele vê quantos ataques do rival vigente já repeliu e quantos faltam

### Requirement: Campanha finita
Cada rival SHALL declarar uma campanha com número finito de ataques. Os ataques SHALL continuar sendo anunciados com antecedência e com força prevista, como qualquer ameaça.

#### Scenario: A campanha tem fim
- **WHEN** o jogador consulta a campanha do rival vigente
- **THEN** o número total de ataques é conhecido e não muda durante a campanha daquele rival

#### Scenario: Ataque de rival é telegrafado
- **WHEN** um ataque da campanha é agendado
- **THEN** ele é anunciado com a mesma antecedência mínima exigida de qualquer ameaça

### Requirement: Repelir derrota o rival
Um ataque SHALL contar como repelido quando a defesa do reino for maior ou igual à força do ataque. Repelidos todos os ataques da campanha, o rival SHALL ser derrotado.

#### Scenario: Campanha repelida derrota o rival
- **WHEN** o jogador repele o último ataque da campanha do rival vigente
- **THEN** o rival é derrotado e isso é comunicado ao jogador

#### Scenario: Ataque que passa não conta como repelido
- **WHEN** a força do ataque excede a defesa do reino
- **THEN** o ataque não conta para a derrota do rival, e o reino sofre as perdas normais

#### Scenario: Sofrer não faz perder a campanha
- **WHEN** o jogador falha em repelir um ataque mas sobrevive
- **THEN** a campanha continua e ele ainda pode derrotar o rival repelindo os ataques restantes

### Requirement: Sucessão de rivais
Derrotado um rival, o próximo SHALL declarar guerra, com campanha mais forte e identidade diferente da anterior. SHALL haver um intervalo declarado entre a derrota de um rival e o primeiro ataque do próximo.

#### Scenario: O próximo rival é anunciado
- **WHEN** um rival é derrotado e ainda restam rivais
- **THEN** o próximo é apresentado com sua identidade, e seu primeiro ataque é agendado com antecedência

#### Scenario: Cada rival é mais forte
- **WHEN** dois rivais consecutivos são comparados
- **THEN** a campanha do segundo exige mais defesa que a do primeiro

#### Scenario: Há respiro entre campanhas
- **WHEN** um rival é derrotado
- **THEN** existe ao menos um dia sem ataque antes do primeiro ataque do rival seguinte

### Requirement: Identidade pressiona recursos diferentes
Cada rival SHALL ter uma identidade que define o tipo de pressão que exerce, e identidades diferentes SHALL exigir respostas diferentes do jogador. Ao menos um rival SHALL pressionar algo que não seja resolvido por defesa.

#### Scenario: Rival de assalto é respondido com defesa
- **WHEN** o rival vigente ataca as muralhas
- **THEN** aumentar a defesa guarnecida é uma resposta eficaz

#### Scenario: Rival econômico não é respondido com muralha
- **WHEN** o rival vigente ataca a comida ou a população
- **THEN** aumentar a defesa não impede a perda, e o jogador precisa de estoque ou de redundância

#### Scenario: A identidade é conhecida antes de sofrer
- **WHEN** um rival declara guerra
- **THEN** o jogador sabe que tipo de pressão esperar antes do primeiro ataque

### Requirement: Vitória da run
Derrotados todos os rivais do nível de dificuldade, a run SHALL terminar em Vitória. A Vitória SHALL ser um desfecho distinto do Colapso, com placar próprio e recompensa de meta maior.

#### Scenario: Último rival derrotado vence a run
- **WHEN** o jogador derrota o último rival do nível
- **THEN** a run termina imediatamente em Vitória, sem esperar mais dias

#### Scenario: Vitória rende mais que Colapso
- **WHEN** duas runs alcançam o mesmo reino construído, uma vencendo e outra colapsando
- **THEN** a run vencida rende mais moeda de meta

#### Scenario: Colapso continua existindo
- **WHEN** a integridade da base chega a zero antes de todos os rivais serem derrotados
- **THEN** a run termina em Colapso, e nenhum rival restante é considerado derrotado
