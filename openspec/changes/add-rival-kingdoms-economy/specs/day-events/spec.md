## MODIFIED Requirements

### Requirement: Ofertas com custo em ouro
Um evento SHALL poder oferecer uma troca com custo em qualquer recurso. Uma opção marcada como oferta SHALL ficar isenta do teto de perda daquele recurso para a sua classe, e SHALL só ser válida em um evento que tenha ao menos uma opção alternativa sem custo — de modo que o jogador sempre possa recusar. Os demais tetos — dano à base, destruição de quadrado, remoção de carta, perda de população — SHALL continuar valendo para ofertas.

#### Scenario: Oferta cobra o preço cheio
- **WHEN** o jogador escolhe uma opção de oferta que custa mais recurso que o teto da classe do evento
- **THEN** o preço é cobrado integralmente, porque o jogador o aceitou ao escolher

#### Scenario: Oferta sempre pode ser recusada
- **WHEN** um evento contém uma opção de oferta
- **THEN** ele contém também ao menos uma opção sem custo, e escolher essa opção não cobra nada

#### Scenario: Oferta não vira dano disfarçado
- **WHEN** uma opção de oferta declara dano à base, destruição de quadrado ou perda de população
- **THEN** esses efeitos continuam limitados pelo orçamento de severidade da classe do evento

## ADDED Requirements

### Requirement: Severidade de evento considera população
Nenhum evento SHALL reduzir a população além do teto declarado para a sua classe, e nenhum evento SHALL zerar a população.

#### Scenario: Perda de população é limitada
- **WHEN** um evento negativo tira população do jogador
- **THEN** a perda respeita o teto da classe e deixa ao menos um habitante
