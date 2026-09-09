## Purpose

Define o ciclo de uma partida (run): a sequência de fases de cada dia, o avanço do tempo, a condição de Colapso que encerra a run e a pontuação que converte o desempenho em moeda de meta-progressão.

## ADDED Requirements

### Requirement: Início de run
O sistema SHALL iniciar uma run apenas após o jogador escolher uma raça, e SHALL montar o estado inicial da run a partir da raça escolhida e do conteúdo desbloqueado na meta-progressão.

#### Scenario: Run inicia com estado padrão
- **WHEN** o jogador confirma a seleção de raça e inicia a run
- **THEN** a run começa no dia 1 com exatamente 1 quadrado de reino possuído, o ouro inicial da raça, a integridade de base inicial da raça e um deck montado apenas com cartas desbloqueadas e permitidas pela raça

#### Scenario: Conteúdo bloqueado não entra na run
- **WHEN** uma run é iniciada e existe conteúdo (cartas, edifícios, eventos) ainda não desbloqueado na meta-progressão
- **THEN** esse conteúdo não aparece em nenhum pool da run

### Requirement: Fases do dia
Cada dia SHALL ser resolvido nas fases, nesta ordem: Início do Dia, Planejamento, Resolução, Evento, Fim do Dia. O jogador SHALL só poder agir na fase de Planejamento.

#### Scenario: Ordem das fases é respeitada
- **WHEN** o jogador aciona "Fim do Dia" durante o Planejamento
- **THEN** o sistema resolve Resolução, depois Evento, depois Fim do Dia, e só então avança para o Início do Dia seguinte

#### Scenario: Entrada não é aceita fora do Planejamento
- **WHEN** o jogador tenta jogar uma carta ou comprar um quadrado durante Resolução, Evento ou Fim do Dia
- **THEN** a ação é rejeitada e o estado da run não muda

### Requirement: Produção diária
Na fase de Resolução, o sistema SHALL aplicar a produção de recursos de todos os edifícios do reino antes de qualquer evento ou ameaça daquele dia.

#### Scenario: Produção precede ameaça
- **WHEN** um dia tem produção de ouro e também um ataque agendado
- **THEN** o ouro é creditado antes de o ataque ser resolvido

### Requirement: Colapso encerra a run
A run SHALL terminar em Colapso quando a integridade da base chegar a zero ou quando o jogador perder o último quadrado possuído. O Colapso SHALL ser apresentado como fim previsto da run, com placar, e não como tela de derrota.

#### Scenario: Integridade zerada encerra a run
- **WHEN** a integridade da base chega a zero durante a Resolução
- **THEN** a run termina imediatamente e a tela de Colapso é exibida com o placar

#### Scenario: Perda do último quadrado encerra a run
- **WHEN** o último quadrado possuído pelo jogador é destruído
- **THEN** a run termina em Colapso

### Requirement: Pontuação de run
Ao encerrar, o sistema SHALL calcular uma pontuação a partir dos dias sobrevividos, do número de quadrados possuídos, dos edifícios construídos e dos marcos alcançados, e SHALL converter essa pontuação em moeda de meta-progressão.

#### Scenario: Placar é gerado e creditado
- **WHEN** a run termina em Colapso
- **THEN** o placar exibe cada componente da pontuação separadamente e a moeda de meta correspondente é creditada ao perfil do jogador

#### Scenario: Run mais longa vale mais
- **WHEN** duas runs terminam com o mesmo reino construído, mas uma sobreviveu mais dias
- **THEN** a run mais longa recebe pontuação maior

### Requirement: Duração alvo da run
O ritmo da run SHALL ser calibrado para um Colapso típico entre os dias 25 e 35 para um jogador competente, mantendo a sessão dentro de 25 a 35 minutos.

#### Scenario: Escalada garante o fim
- **WHEN** o jogador sobrevive além do dia 35
- **THEN** a pressão das ameaças continua escalando sem teto, de modo que a run não pode ser sustentada indefinidamente
