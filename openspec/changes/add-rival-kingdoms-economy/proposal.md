## Why

A fatia vertical anterior entregou uma run jogável, mas o playtest e a simulação expuseram dois vazios que nenhum ajuste de curva resolve.

**O ouro sozinho colapsa a decisão.** Com um recurso só, existe uma estratégia só: acumular e comprar defesa. O diagnóstico do simulador repetiu "OURO PARADO" em todas as medições, e o bot heurístico converge sempre para a mesma jogada — não por burrice, mas porque o jogo não oferece outra. Sintoma disso: foi preciso inventar uma penalidade artificial por expandir (`threat_per_tile`) porque não existia razão natural para parar de crescer.

**A ameaça é anônima e infinita, então não há o que perseguir.** A run mede "quantos dias até morrer". Não existe linha de chegada, não existe contra-jogo específico, e a árvore de meta — que é o elemento roguelite inteiro — está vazia. Passamos a calibrar um número de sobrevivência para um jogo cuja camada de progressão nunca foi construída.

Esta mudança ataca os dois: uma economia de cinco recursos onde população é bem e dívida ao mesmo tempo, e uma campanha de reinos rivais nomeados que dá vitória, contra-jogo e escada de dificuldade.

## What Changes

- **Cinco recursos** no lugar do ouro único: ouro, madeira, pedra, comida e população. Edifícios custam madeira e pedra; a população opera edifícios e guarnece defesas; a comida é consumida pela população todo dia.
  - **População é o teto natural de expansão**: cada edifício exige trabalhadores, cada pessoa come, mais comida exige mais terra, mais terra exige mais gente. Isso substitui a penalidade artificial por célula que existe hoje.
  - **Defesa deixa de ser número solto** e passa a exigir guarnição: uma torre sem gente não defende.
- **Reinos rivais nomeados** substituem a ameaça anônima. Cada rival tem identidade própria e uma campanha de ataques; repelir todos os ataques de um rival o derrota e o próximo declara guerra.
  - **Vitória de run** ao derrotar todos os rivais. **BREAKING** em relação à decisão original de que toda run termina em Colapso — o Colapso continua sendo a derrota, mas passa a existir triunfo.
  - Cada rival pressiona um recurso diferente, o que é o que torna os cinco recursos necessários em vez de decorativos. Um rival que ataca a comida não é respondido com muralha.
- **Escada de dificuldade**: vencer desbloqueia o próximo nível, que adiciona rivais ou endurece os existentes.
- **Árvore de meta com buffs permanentes**, além do desbloqueio de conteúdo. **BREAKING** em relação à decisão registrada na mudança anterior, que proibia poder permanente para evitar grind-para-vencer. A objeção cai porque a escada de dificuldade absorve o poder acumulado; sem a escada, ela continuaria valendo.
- **Recalibração completa** das curvas de custo, produção e ameaça. O balanceamento atual foi medido sobre a economia de ouro único e não sobrevive à mudança.
- **Fora de escopo**: contra-ataque ou invasão do rival, mais de uma raça jogável, arte final, Steamworks, UI definitiva além do necessário para jogar os cinco recursos e a campanha.

## Capabilities

### New Capabilities
- `resources`: os cinco recursos, produção por terreno, consumo diário de comida, crescimento e alocação de população, e as regras de escassez.
- `rival-kingdoms`: identidade de cada rival, campanha de ataques, condição de derrota do rival, sucessão entre rivais e vitória da run.
- `difficulty-ladder`: níveis de dificuldade, o que cada um endurece, e desbloqueio pelo triunfo.

### Modified Capabilities
- `kingdom-grid`: edifícios passam a custar madeira e pedra em vez de ouro, a produzir recursos distintos por terreno e a exigir trabalhadores para operar.
- `threat-clock`: a ameaça deixa de ser uma curva anônima sem teto e passa a ser a campanha do rival vigente, com número finito de ataques e identidade que define o tipo de pressão.
- `run-loop`: o fim da run deixa de ser apenas Colapso; passa a existir Vitória por derrotar todos os rivais, e a pontuação considera os dois desfechos.
- `card-system`: cartas passam a custar e conceder recursos distintos, não apenas ouro e defesa.
- `meta-progression`: nós passam a poder conceder bônus numéricos permanentes, e a validação que hoje proíbe isso é substituída por um teto por nível de dificuldade.
- `day-events`: eventos e ofertas passam a operar sobre os cinco recursos.
- `weather`: o clima passa a afetar recursos por terreno, não apenas ouro.

## Impact

- **Reescrita do núcleo econômico**: `RunState`, `ProductionCalculator`, `BuildingDefinition`, efeitos de recurso e todo o conteúdo autorado.
- **Simulador e política do bot** precisam entender cinco recursos e a campanha; a heurística atual só sabe raciocinar sobre ouro e defesa.
- **Conteúdo existente reautorado**: os 7 edifícios, 15 cartas, 20 eventos e 6 climas já criados precisam de custos e efeitos nos recursos novos. O gerador de conteúdo cobre a maior parte disso.
- **Balanceamento zerado**: as curvas atuais deixam de valer, e a medição recomeça com o alvo de duração medido em campanha vencida, não em dias sobrevividos.
- **HUD**: precisa mostrar cinco recursos, população alocada e o progresso da campanha do rival. O andaime IMGUI atual serve para provar o loop, e a UI definitiva continua sendo tarefa própria.
