# weather Specification

## Purpose
Define a condição natural de cada dia — sol, chuva, seca, tempestade, neblina, dia limpo — que sempre traz vantagem e desvantagem ao mesmo tempo, é anunciada com um dia de antecedência e pode atrasar ou enfraquecer uma ameaça já agendada.

## Requirements

### Requirement: Todo dia tem clima
Todo dia da run SHALL ter exatamente um clima vigente. O clima SHALL ser sorteado do pool disponível e SHALL ser distinto de evento de fim de dia: clima acontece sempre, evento acontece às vezes.

#### Scenario: Nenhum dia sem clima
- **WHEN** qualquer dia da run começa
- **THEN** existe um clima vigente para aquele dia, consultável pelo jogador

#### Scenario: Clima não ocupa o lugar do evento
- **WHEN** um dia tem clima e também sorteia um evento de fim de dia
- **THEN** os dois acontecem, sem que um exclua o outro

### Requirement: Previsão de um dia
O clima do dia seguinte SHALL ser conhecido durante o Planejamento do dia atual. O clima SHALL NOT mudar entre a previsão e o dia previsto.

#### Scenario: Previsão disponível no Planejamento
- **WHEN** o jogador está planejando o dia
- **THEN** ele consegue ver qual será o clima de amanhã

#### Scenario: Previsão é honrada
- **WHEN** o dia seguinte começa
- **THEN** o clima vigente é exatamente o que havia sido previsto

### Requirement: Clima é sempre misto
Cada clima SHALL declarar ao menos uma vantagem e ao menos uma desvantagem. Um clima SHALL NOT ser puramente bom nem puramente ruim, com exceção explícita do dia limpo, que não altera nada.

#### Scenario: Clima traz os dois lados
- **WHEN** o catálogo de climas é inspecionado
- **THEN** todo clima, exceto o dia limpo, declara ao menos um efeito favorável e um desfavorável

### Requirement: Clima altera produção por terreno
Um clima SHALL poder modificar a produção de acordo com o terreno de cada quadrado, e SHALL poder aplicar um multiplicador à produção total do dia.

#### Scenario: Chuva favorece rio e prejudica obra
- **WHEN** o clima do dia é chuva e o jogador possui um quadrado de rio produtivo
- **THEN** aquele quadrado produz mais que produziria em dia limpo, e construir custa mais caro naquele dia

#### Scenario: Seca inverte o favorecimento
- **WHEN** o clima do dia é seca
- **THEN** quadrados de mina produzem mais e fazendas em planície produzem menos

### Requirement: Clima altera a ameaça na previsão, nunca na chegada
Um clima SHALL poder reduzir a força ou adiar a chegada de uma ameaça já anunciada. Essa alteração SHALL ser aplicada no momento em que o clima é previsto, e SHALL estar refletida no relógio de ameaça antes do dia de chegada. Um clima SHALL NOT agendar, criar nem cancelar uma ameaça.

#### Scenario: Tempestade enfraquece a horda antes do dia
- **WHEN** a previsão indica tempestade para o dia em que uma horda chega
- **THEN** a força exibida no relógio já aparece reduzida, e é essa força reduzida que é resolvida no dia

#### Scenario: Chuva adia a chegada
- **WHEN** a previsão indica chuva para o dia de chegada de uma ameaça
- **THEN** a ameaça passa a chegar mais tarde e o relógio mostra o novo dia

#### Scenario: A força prevista é a força que chega
- **WHEN** uma ameaça é resolvida
- **THEN** a força aplicada é igual à última força exibida no relógio para aquela ameaça

#### Scenario: Clima não cria ataque
- **WHEN** qualquer clima é aplicado
- **THEN** nenhuma ameaça nova é agendada e nenhuma ameaça anunciada é cancelada

### Requirement: Severidade limitada do clima
Nenhum clima SHALL destruir quadrado, remover carta, encerrar a run nem alterar a produção total do dia além do teto declarado. O dano de clima à base SHALL ser limitado e nunca letal.

#### Scenario: Clima nunca encerra a run
- **WHEN** um clima causa dano à base e esse dano zeraria a integridade
- **THEN** a integridade é reduzida no máximo até 1 e a run continua

#### Scenario: Clima nunca arrasa território
- **WHEN** qualquer clima é aplicado
- **THEN** nenhum quadrado é destruído

#### Scenario: Variação de produção respeita o teto
- **WHEN** o catálogo de climas é inspecionado
- **THEN** nenhum clima altera a produção total do dia além do teto declarado para a categoria

### Requirement: Clima pode bloquear ações
Um clima SHALL poder impedir ações específicas do dia, como comprar quadrado ou revelar terreno, e a razão SHALL ser comunicada ao jogador.

#### Scenario: Neblina impede expandir
- **WHEN** o clima do dia é neblina e o jogador tenta comprar um quadrado
- **THEN** a compra é recusada e o motivo informa que a neblina impede a expansão hoje
