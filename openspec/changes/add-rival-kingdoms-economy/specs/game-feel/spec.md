## Purpose

Define a camada de retorno visual que torna o jogo legível e gratificante sem ler texto: o que mudou aparece onde mudou, a resolução do dia é encenada em ordem, e o ataque é tratado como a cena principal que de fato é.

## ADDED Requirements

### Requirement: Mudança aparece na origem
Toda alteração de recurso causada por um quadrado SHALL ser exibida sobre aquele quadrado no momento em que acontece, e não apenas no contador do recurso.

#### Scenario: Produção salta sobre a célula
- **WHEN** a produção do dia é coletada
- **THEN** cada quadrado que produziu mostra o valor produzido sobre si, e o contador do recurso sobe em seguida

#### Scenario: Perda também aparece onde dói
- **WHEN** um quadrado é arrasado ou fica ocioso
- **THEN** a mudança é sinalizada naquele quadrado, e não apenas num texto de log

### Requirement: Resolução do dia é encenada em ordem
Ao encerrar o dia, o sistema SHALL apresentar os passos na mesma ordem em que o núcleo os resolve — produção, consumo, ameaça, evento — com uma pausa legível entre eles.

#### Scenario: A ordem apresentada é a ordem real
- **WHEN** o jogador encerra o dia
- **THEN** os passos aparecem na ordem em que foram resolvidos, e nenhum resultado é mostrado antes de sua causa

#### Scenario: A encenação não altera a regra
- **WHEN** a mesma semente é resolvida com e sem encenação
- **THEN** o estado final da run é idêntico

### Requirement: Encenação pode ser acelerada
O jogador SHALL poder acelerar ou pular a encenação da resolução, e a preferência SHALL persistir entre dias.

#### Scenario: Pular não perde informação
- **WHEN** o jogador pula a encenação
- **THEN** o resultado final do dia continua visível e consultável

#### Scenario: A escolha persiste
- **WHEN** o jogador acelera a encenação num dia
- **THEN** os dias seguintes usam a mesma velocidade sem precisar repetir a escolha

### Requirement: O ataque é a cena principal
A resolução de um ataque SHALL ser apresentada como o momento de maior destaque do dia, mostrando a força que chega, a defesa que responde e o desfecho, de modo que o jogador entenda o resultado sem ler números.

#### Scenario: Repelir é legível sem texto
- **WHEN** um ataque é repelido
- **THEN** a apresentação deixa claro que a defesa segurou, antes de qualquer texto explicativo

#### Scenario: A quebra também é legível
- **WHEN** um ataque excede a defesa
- **THEN** a apresentação mostra o que foi perdido, no lugar onde foi perdido

#### Scenario: O detalhamento continua disponível
- **WHEN** a cena termina
- **THEN** o jogador ainda consegue consultar força, defesa e perdas de forma discriminada

### Requirement: Progresso e desbloqueio são momentos
Derrotar um rival, vencer a run e desbloquear conteúdo SHALL ser apresentados como eventos destacados, distintos do fluxo normal do dia.

#### Scenario: Derrotar um rival é celebrado
- **WHEN** o último ataque da campanha de um rival é repelido
- **THEN** a derrota do rival é apresentada como um momento próprio, antes de o próximo declarar guerra

#### Scenario: Desbloqueio é mostrado, não anotado
- **WHEN** a run termina e algo é desbloqueado
- **THEN** o desbloqueio é apresentado na tela de fim, nomeando o que foi liberado

### Requirement: O estado do reino é legível sem log
O jogador SHALL conseguir identificar, olhando o tabuleiro, quais quadrados produzem, quais estão ociosos por falta de gente e quais estão arrasados.

#### Scenario: Ociosidade é visível
- **WHEN** um edifício está sem trabalhadores
- **THEN** o quadrado o indica visualmente, sem exigir seleção nem leitura de painel

#### Scenario: O que falta é apontado
- **WHEN** uma ação é recusada por falta de recurso
- **THEN** o recurso que falta é destacado no lugar onde ele é exibido
