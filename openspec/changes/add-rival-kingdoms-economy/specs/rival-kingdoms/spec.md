## Purpose

Define os reinos rivais que substituem a ameaça anônima: adversários nomeados, cada um com uma identidade que pressiona um recurso diferente, que entram em guerra de forma escalonada e se somam aos que já estão lutando, derrotados ao terem sua campanha repelida, e cuja derrota completa encerra a run em Vitória.

## ADDED Requirements

### Requirement: Rivais em guerra
A run SHALL ter ao menos um rival em guerra enquanto não terminar. O jogador SHALL saber quais rivais estão em guerra, a identidade de cada um e quantos ataques faltam para derrotar cada um, e SHALL saber qual rival entra a seguir e em que dia.

#### Scenario: O primeiro rival é conhecido desde o começo
- **WHEN** a run começa
- **THEN** apenas o primeiro rival do nível está em guerra, apresentado com nome, identidade e o número de ataques da sua campanha, e o dia de entrada do rival seguinte é conhecido

#### Scenario: Progresso de cada campanha é visível
- **WHEN** o jogador está no Planejamento
- **THEN** ele vê, para cada rival em guerra, quantos ataques já repeliu e quantos faltam

### Requirement: Campanha finita
Cada rival SHALL declarar uma campanha com número finito de ataques a repelir. Os ataques SHALL continuar sendo anunciados com antecedência e com força prevista, como qualquer ameaça.

#### Scenario: A campanha tem fim
- **WHEN** o jogador consulta a campanha de um rival em guerra
- **THEN** o número total de ataques a repelir é conhecido e não muda durante a campanha daquele rival

#### Scenario: Ataque de rival é telegrafado
- **WHEN** um ataque de campanha é agendado
- **THEN** ele é anunciado com a mesma antecedência mínima exigida de qualquer ameaça

### Requirement: Repelir derrota o rival
Um ataque SHALL contar como repelido quando a defesa do reino for maior ou igual à força do ataque. Repelidos todos os ataques da campanha, o rival SHALL ser derrotado e SHALL sair da guerra, enquanto os demais rivais em guerra continuam.

#### Scenario: Campanha repelida derrota o rival
- **WHEN** o jogador repele o último ataque da campanha de um rival em guerra
- **THEN** o rival é derrotado e isso é comunicado ao jogador

#### Scenario: Rival derrotado sai da guerra
- **WHEN** um rival é derrotado e outros continuam em guerra
- **THEN** o derrotado não ataca mais, e os demais seguem atacando normalmente

#### Scenario: Ataque que passa não conta como repelido
- **WHEN** a força do ataque excede a defesa do reino
- **THEN** o ataque não conta para a derrota do rival, e o reino sofre as perdas normais

#### Scenario: Sofrer não faz perder a campanha
- **WHEN** o jogador falha em repelir um ataque mas sobrevive
- **THEN** a campanha continua e ele ainda pode derrotar o rival repelindo os ataques restantes

### Requirement: Entrada escalonada
Os rivais do nível SHALL entrar em guerra na ordem declarada, cada um no seu dia de entrada, somando-se aos rivais que já estão em guerra, sem substituí-los. Se nenhum rival estiver em guerra e ainda restarem rivais, o próximo SHALL entrar sem esperar o seu dia, depois de ao menos um dia sem ataque. Cada rival SHALL ter campanha mais forte que a do rival que entrou antes dele, e nenhuma identidade SHALL se repetir entre os rivais de um nível.

#### Scenario: Entrar não substitui
- **WHEN** o segundo rival entra em guerra enquanto o primeiro ainda não foi derrotado
- **THEN** os dois ficam em guerra, e ataques de qualquer um deles podem ser agendados

#### Scenario: A entrada é anunciada antes do primeiro ataque
- **WHEN** um rival entra em guerra
- **THEN** ele é apresentado com sua identidade antes do seu primeiro ataque, e esse ataque respeita a antecedência mínima

#### Scenario: Cada rival é mais forte
- **WHEN** dois rivais consecutivos na ordem de entrada são comparados
- **THEN** a campanha do segundo exige mais defesa que a do primeiro

#### Scenario: Não há tempo morto entre campanhas
- **WHEN** todos os rivais em guerra são derrotados e ainda restam rivais por entrar
- **THEN** o próximo entra sem esperar o seu dia, e existe ao menos um dia sem ataque antes do primeiro ataque dele

#### Scenario: Identidades não se repetem no nível
- **WHEN** os rivais de um nível são comparados
- **THEN** nenhuma identidade aparece em mais de um deles

### Requirement: Ataques sorteados entre os rivais em guerra
A cada janela de ataque, o atacante SHALL ser sorteado entre os rivais em guerra, de forma reprodutível pela semente da run. O sorteio SHALL acontecer no anúncio, e SHALL NOT acontecer na resolução. As janelas SHALL ficar mais frequentes quanto mais rivais estiverem em guerra, e SHALL NOT haver mais de um ataque de rival no mesmo dia.

#### Scenario: O sorteio é anunciado
- **WHEN** uma janela de ataque é preenchida
- **THEN** o ataque é anunciado com a antecedência mínima, indicando de qual rival vem

#### Scenario: Mais rivais, mais pressão
- **WHEN** o número de rivais em guerra aumenta
- **THEN** o intervalo entre ataques diminui

#### Scenario: Nunca dois ataques no mesmo dia
- **WHEN** há dois ou mais rivais em guerra
- **THEN** no máximo um ataque de rival é resolvido por dia

#### Scenario: Adiamento não empilha ataques
- **WHEN** o clima adia um ataque para um dia que já tem outro ataque agendado
- **THEN** o ataque adiado vai para o primeiro dia seguinte sem ataque

#### Scenario: Mesma semente, mesma guerra
- **WHEN** duas runs com a mesma semente e as mesmas decisões são jogadas
- **THEN** a sequência de atacantes é a mesma

### Requirement: Identidade pressiona recursos diferentes
Cada rival SHALL ter uma identidade que define o tipo de pressão que exerce, e identidades diferentes SHALL exigir respostas diferentes do jogador. Ao menos um rival SHALL pressionar algo que não seja resolvido por defesa.

#### Scenario: Rival de assalto é respondido com defesa
- **WHEN** um rival de assalto ataca as muralhas
- **THEN** aumentar a defesa guarnecida é uma resposta eficaz

#### Scenario: Rival econômico não é respondido com muralha
- **WHEN** um rival econômico ataca a comida ou a população
- **THEN** aumentar a defesa não impede a perda, e o jogador precisa de estoque ou de redundância

#### Scenario: A identidade é conhecida antes de sofrer
- **WHEN** um rival entra em guerra
- **THEN** o jogador sabe que tipo de pressão esperar antes do primeiro ataque

### Requirement: Vitória da run
Derrotados todos os rivais do nível de dificuldade, a run SHALL terminar em Vitória. A Vitória SHALL ser um desfecho distinto do Colapso, com placar próprio e recompensa de meta maior.

#### Scenario: Último rival derrotado vence a run
- **WHEN** o jogador derrota o último rival do nível que ainda restava
- **THEN** a run termina imediatamente em Vitória, sem esperar mais dias

#### Scenario: Vitória rende mais que Colapso
- **WHEN** duas runs alcançam o mesmo reino construído, uma vencendo e outra colapsando
- **THEN** a run vencida rende mais moeda de meta

#### Scenario: Colapso continua existindo
- **WHEN** a integridade da base chega a zero antes de todos os rivais serem derrotados
- **THEN** a run termina em Colapso, e nenhum rival restante é considerado derrotado
