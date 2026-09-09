## Purpose

Define a pressão que acaba destruindo o reino: ameaças agendadas com antecedência visível, cuja força escala com o dia e com o tamanho do território, resolvidas contra as defesas que o jogador teve tempo de preparar.

## ADDED Requirements

### Requirement: Ameaças são telegrafadas
Toda ameaça SHALL ser agendada e anunciada ao jogador com pelo menos 2 dias de antecedência, informando o dia de chegada e a força prevista. Nenhuma ameaça SHALL ser resolvida sem ter sido anunciada antes.

#### Scenario: Anúncio antecipado
- **WHEN** uma horda é agendada para o dia 8
- **THEN** o jogador é informado no dia 6 ou antes sobre o dia de chegada e a força prevista

#### Scenario: Ameaça surpresa não existe
- **WHEN** a fase de Resolução de um dia começa
- **THEN** as únicas ameaças resolvidas são as que já estavam anunciadas no relógio de ameaça

### Requirement: Escalada de pressão
A força das ameaças agendadas SHALL crescer com o número do dia e com o número de quadrados possuídos, sem teto superior.

#### Scenario: Expandir aumenta a pressão
- **WHEN** dois jogadores chegam ao mesmo dia, um com 4 quadrados e outro com 12
- **THEN** a ameaça agendada para o jogador com 12 quadrados é mais forte

#### Scenario: Pressão nunca estabiliza
- **WHEN** o jogador sobrevive a dias muito avançados sem expandir
- **THEN** a força das ameaças continua subindo apenas em função do dia

### Requirement: Resolução de ataque
Na Resolução, a força da ameaça SHALL ser confrontada com a defesa total do reino. A força excedente SHALL destruir quadrados e, esgotados os alvos ou conforme a natureza da ameaça, reduzir a integridade da base.

#### Scenario: Defesa suficiente anula o ataque
- **WHEN** a defesa do reino é maior ou igual à força da ameaça
- **THEN** nenhum quadrado é destruído e a integridade da base não muda

#### Scenario: Excedente destrói quadrados
- **WHEN** a força da ameaça excede a defesa do reino
- **THEN** o excedente destrói quadrados a partir da borda do território e o restante reduz a integridade da base

#### Scenario: Resultado é explicado
- **WHEN** um ataque é resolvido
- **THEN** o jogador vê a força, a defesa e as perdas resultantes de forma discriminada

### Requirement: Defesa temporária dura o dia
Defesa concedida por carta ou por evento SHALL valer apenas no dia em que foi concedida, e SHALL expirar no Fim do Dia mesmo que nenhum ataque tenha acontecido. Defesa vinda de edifícios SHALL ser permanente enquanto o edifício estiver de pé.

#### Scenario: Defesa de carta não acumula entre dias
- **WHEN** o jogador ganha defesa por carta num dia sem ataque e o dia termina
- **THEN** essa defesa não está mais disponível no dia seguinte

#### Scenario: Defesa de edifício permanece
- **WHEN** o dia termina e o jogador tem uma Torre de Vigia em pé
- **THEN** a defesa da Torre continua contando no dia seguinte

#### Scenario: Preparar no dia certo é a decisão
- **WHEN** uma ameaça é anunciada para daqui a três dias
- **THEN** jogar defesa hoje não ajuda no dia da chegada, e o jogador precisa guardar a carta para o dia do ataque

### Requirement: Visibilidade do relógio
O relógio de ameaça SHALL estar visível durante todo o Planejamento, mostrando os dias restantes e a força prevista de cada ameaça pendente.

#### Scenario: Relógio consultável no Planejamento
- **WHEN** o jogador está planejando o dia
- **THEN** ele consegue ver todas as ameaças pendentes com dia de chegada e força prevista sem sair da tela de jogo
