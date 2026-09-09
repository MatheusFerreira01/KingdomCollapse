## Purpose

Define os eventos de fim de dia que dão variedade e textura à run: sorteios ponderados de acontecimentos bons, ruins ou neutros que ajustam recursos, grid e deck sem jamais decidir o desfecho da run sozinhos. Eventos são o tempero aleatório da run; a pressão que de fato a encerra vem do relógio de ameaça, sempre anunciado com antecedência.

## ADDED Requirements

### Requirement: Um evento por dia
Na fase de Evento o sistema SHALL sortear no máximo um evento do pool elegível e apresentá-lo ao jogador antes de aplicar seus efeitos.

#### Scenario: Evento é apresentado antes de aplicar
- **WHEN** a fase de Evento começa e um evento é sorteado
- **THEN** o evento é exibido com seu texto e efeitos, e só então os efeitos são aplicados

#### Scenario: Dia sem evento
- **WHEN** nenhum evento do pool está elegível
- **THEN** o dia avança normalmente sem evento e sem erro

### Requirement: Classificação e mistura do pool
Cada evento SHALL ser classificado como positivo, negativo ou neutro. O sorteio SHALL ser ponderado de forma que, ao longo de uma run típica, nenhuma das três classes domine as demais.

#### Scenario: Sequência não é só desgraça
- **WHEN** uma run típica de 30 dias é simulada
- **THEN** eventos positivos e neutros somados representam a maioria dos eventos sorteados

### Requirement: Elegibilidade por condição
Eventos SHALL declarar condições de elegibilidade (dia mínimo, terreno possuído, edifício presente, raça) e SHALL só entrar no sorteio quando suas condições forem satisfeitas.

#### Scenario: Evento condicional fica fora
- **WHEN** um evento exige que o jogador possua uma mina e o jogador não possui nenhuma
- **THEN** esse evento não entra no sorteio daquele dia

### Requirement: Sem repetição imediata
Um evento sorteado SHALL ficar indisponível por um número mínimo de dias antes de poder ser sorteado de novo na mesma run.

#### Scenario: Evento não repete no dia seguinte
- **WHEN** um evento é sorteado no dia 10
- **THEN** ele não pode ser sorteado no dia 11

### Requirement: Eventos não encerram a run sozinhos
Nenhum evento SHALL, por si só, reduzir a integridade da base a zero nem remover o último quadrado do jogador. O dano de evento SHALL ser limitado de modo que a run só termine por acúmulo de decisões e ameaças.

#### Scenario: Dano de evento é limitado
- **WHEN** um evento negativo causa dano à base e esse dano zeraria a integridade
- **THEN** a integridade é reduzida no máximo até 1 e a run continua

### Requirement: Severidade limitada de evento
Nenhum evento SHALL causar uma perda maior que o orçamento de severidade declarado para a sua classe. Um evento negativo SHALL NOT destruir um quadrado ocupado por edifício, SHALL NOT tomar mais que uma fração declarada do ouro do jogador, e SHALL NOT remover mais de uma carta do deck da run.

#### Scenario: Evento não destrói quadrado construído
- **WHEN** um evento negativo tenta destruir um quadrado e o único candidato tem edifício
- **THEN** nenhum quadrado é destruído e o evento resolve seus demais efeitos normalmente

#### Scenario: Perda de ouro é proporcional e limitada
- **WHEN** um evento negativo cobra ouro do jogador
- **THEN** o valor cobrado é no máximo a fração declarada do ouro atual, e o jogador nunca fica com ouro negativo

#### Scenario: Severidade não escala com o dia
- **WHEN** o mesmo evento negativo é sorteado no dia 5 e no dia 30
- **THEN** o custo relativo imposto ao jogador é equivalente nos dois casos, sem escalada

### Requirement: Eventos não substituem ameaças
Eventos SHALL ser sorteados independentemente do relógio de ameaça e SHALL NOT agendar, adiantar nem cancelar uma ameaça anunciada. Um evento SHALL poder modificar a força prevista de uma ameaça já anunciada, e essa alteração SHALL ser refletida no relógio antes do dia de chegada.

#### Scenario: Evento não cria ataque surpresa
- **WHEN** um evento negativo é sorteado no dia 7
- **THEN** nenhuma ameaça é resolvida naquele dia por causa do evento

#### Scenario: Evento altera previsão visível
- **WHEN** um evento reforça uma horda já agendada para o dia 12
- **THEN** o relógio de ameaça passa a exibir a força atualizada antes do dia 12

### Requirement: Classe do evento é legível antes da resolução
A apresentação de um evento SHALL indicar sua classe — positivo, negativo ou neutro — antes que o jogador confirme, de modo que uma sequência de eventos ruins seja percebida como variação e não como punição arbitrária.

#### Scenario: Classe visível na apresentação
- **WHEN** um evento é exibido ao jogador
- **THEN** sua classe é identificável na apresentação, antes de qualquer confirmação

### Requirement: Eventos com escolha
Eventos SHALL poder oferecer ao jogador uma escolha entre opções com custos e recompensas distintos, e a escolha SHALL ser feita antes de qualquer efeito ser aplicado.

#### Scenario: Escolha do jogador é respeitada
- **WHEN** um evento oferece duas opções e o jogador escolhe a segunda
- **THEN** apenas os efeitos da segunda opção são aplicados
