## Purpose

Define a economia do reino em cinco recursos — ouro, madeira, pedra, comida e população — onde a população é ao mesmo tempo o que faz o reino funcionar e o que o consome, criando um teto de crescimento que nasce da própria economia em vez de ser imposto por uma penalidade.

## ADDED Requirements

### Requirement: Cinco recursos distintos
O reino SHALL controlar exatamente cinco recursos: ouro, madeira, pedra, comida e população. Cada recurso SHALL ter origem, uso e escassez próprios, e nenhum SHALL ser conversível livremente em outro sem custo declarado.

#### Scenario: Recursos não são intercambiáveis
- **WHEN** o jogador tem ouro suficiente mas não tem madeira
- **THEN** ele não consegue construir um edifício que exige madeira, e o motivo informa qual recurso falta

#### Scenario: Cada recurso é exibido separadamente
- **WHEN** o jogador está no Planejamento
- **THEN** ele vê a quantidade atual de cada um dos cinco recursos e a variação prevista para o próximo dia

### Requirement: Produção por terreno
Cada terreno SHALL favorecer recursos diferentes, e um edifício SHALL produzir o recurso do seu tipo apenas no terreno que o comporta.

#### Scenario: Terrenos rendem coisas diferentes
- **WHEN** o jogador possui uma floresta com serraria e uma mina com poço
- **THEN** a floresta rende madeira e a mina rende pedra, e nenhuma das duas rende o recurso da outra

#### Scenario: Comida vem de planície e rio
- **WHEN** o jogador quer aumentar a produção de comida
- **THEN** ele precisa de quadrados de planície ou de rio, e não consegue resolver isso comprando qualquer quadrado

### Requirement: Consumo diário de comida
A população SHALL consumir comida todo dia, em quantidade proporcional ao seu tamanho. O consumo SHALL ser aplicado na Resolução, depois da produção do dia.

#### Scenario: População come todo dia
- **WHEN** o dia é resolvido com população maior que zero
- **THEN** a comida é reduzida na proporção declarada, e o valor consumido é exibido de forma discriminada

#### Scenario: Crescer o reino aumenta o custo de mantê-lo
- **WHEN** o jogador dobra a população
- **THEN** o consumo diário de comida aumenta na mesma proporção

### Requirement: Escassez de comida
Quando a comida não cobrir o consumo do dia, a população SHALL diminuir, e o jogador SHALL ser avisado antes que isso aconteça. A fome SHALL NOT encerrar a run diretamente.

#### Scenario: Fome reduz a população
- **WHEN** o consumo do dia excede a comida disponível
- **THEN** a comida vai a zero e a população cai na proporção do que faltou

#### Scenario: Fome é anunciada com antecedência
- **WHEN** a produção prevista de comida é menor que o consumo previsto para o dia seguinte
- **THEN** o jogador é avisado durante o Planejamento, a tempo de agir

#### Scenario: Fome não é morte súbita
- **WHEN** a população chega a zero por fome
- **THEN** a run continua, com o reino incapaz de operar edifícios até que a população volte a crescer

### Requirement: Crescimento de população
A população SHALL crescer quando houver excedente de comida e espaço para abrigá-la, e SHALL ser limitada por uma capacidade derivada dos edifícios do reino.

#### Scenario: Excedente vira gente
- **WHEN** o dia termina com comida acima do consumo e capacidade disponível
- **THEN** a população cresce no dia seguinte

#### Scenario: Capacidade limita o crescimento
- **WHEN** a população atinge a capacidade dos edifícios
- **THEN** ela para de crescer mesmo com excedente de comida, e o jogador é informado do limite

### Requirement: Trabalhadores operam edifícios
Cada edifício SHALL declarar quantos trabalhadores exige para operar. Um edifício sem trabalhadores suficientes SHALL NOT produzir nem conceder defesa, e SHALL permanecer de pé.

#### Scenario: Edifício sem gente não produz
- **WHEN** a população disponível é menor que o total exigido pelos edifícios
- **THEN** parte dos edifícios fica ociosa, não produz, e a ociosidade é visível no detalhe de cada quadrado

#### Scenario: Perder população desliga edifícios
- **WHEN** a população cai por fome ou por ataque
- **THEN** edifícios ficam ociosos na ordem declarada de prioridade, sem serem destruídos

### Requirement: Defesa exige guarnição
Defesa vinda de edifícios SHALL depender de trabalhadores alocados: um edifício defensivo sem guarnição SHALL conceder zero de defesa.

#### Scenario: Torre vazia não defende
- **WHEN** uma Torre de Vigia está de pé mas sem trabalhadores alocados
- **THEN** ela não soma defesa alguma na resolução do ataque

#### Scenario: A mesma gente não faz duas coisas
- **WHEN** o jogador aloca trabalhadores para guarnecer torres
- **THEN** esses trabalhadores deixam de operar edifícios de produção, e a queda de produção é visível

### Requirement: População é o teto da expansão
O crescimento do território SHALL ser limitado pela capacidade da economia de sustentá-lo, e não por uma penalidade aplicada ao número de quadrados.

#### Scenario: Expandir sem sustentar não compensa
- **WHEN** o jogador compra quadrados além do que a comida e a população conseguem operar
- **THEN** os quadrados novos ficam ociosos e não aumentam a produção nem a defesa

#### Scenario: Não existe penalidade artificial por tamanho
- **WHEN** o jogador possui muitos quadrados operados e alimentados
- **THEN** o tamanho do território por si só não impõe nenhuma perda ao jogador
