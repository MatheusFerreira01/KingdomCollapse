## Why

Precisamos de um segundo projeto, menor e comercializável, para aprender o pipeline de monetização na Steam (Steam Direct, página de loja, demo, Next Fest, wishlists) e gerar renda, enquanto o projeto MM fica em pausa. O jogo escolhido é um roguelite de estratégia de gestão de reino: escopo pequeno o bastante para um dev solo terminar, e com nicho comprovado no mercado (Against the Storm, Luck be a Landlord, Kingdom Two Crowns, Balatro).

Esta mudança estabelece a **fatia vertical jogável**: uma run completa, do início ao Colapso, com progressão de meta mínima. Sem ela não há nada para testar, gravar em trailer ou colocar em demo — e a página da Steam depende de material jogável.

## What Changes

- **Nova base de projeto Unity** (`KingdomCollapse/`), repositório git separado do MM, 3D baixo-poli com câmera isométrica fixa.
- **Ciclo de dia** com fases explícitas: Planejamento → Fim do Dia (resolução) → Evento → Avanço de dia. Run alvo de ~30 dias / 25–35 min.
- **Grid de reino**: jogador começa com 1 quadrado e compra quadrados adjacentes com ouro. Cada quadrado tem um **terreno** (planície, floresta, mina, rio, ruína) que define o que pode ser construído ali e cria sinergias de adjacência.
- **Sistema de cartas**: deck de run, mão de 5 cartas por dia, custo de energia (3/dia), descarte no fim do dia. Cartas constroem, produzem, defendem ou manipulam o grid.
- **Relógio de ameaça telegrafado**: ataques e catástrofes são anunciados com pelo menos 2 dias de antecedência, com força prevista visível. Essa é a única fonte de pressão que escala e a única que pode encerrar a run.
- **Eventos aleatórios de fim de dia**, separados do relógio de ameaça: um por dia, classificados em positivos, neutros e negativos, com mistura equilibrada ao longo da run. Existem para diversificar as runs, não para dificultá-las — cada classe tem um orçamento de severidade fixo, e um evento negativo nunca destrói um quadrado construído, nunca encerra a run e não fica mais forte com o passar dos dias.
- **Colapso como fim previsto, não derrota**: toda run termina em Colapso. A run é pontuada (dias sobrevividos, reino construído, marcos), e a pontuação vira moeda de meta.
- **Raças jogáveis** escolhidas antes da run, cada uma alterando uma **regra** do jogo (não apenas números). A fatia vertical entrega 1 raça (Humanos); as outras 3 vêm em mudança posterior.
- **Meta-progressão por desbloqueio de conteúdo**, não por buff numérico: gastar moeda de meta adiciona cartas, edifícios, eventos e raças ao pool. Cada raça tem um galho próprio na árvore.
  - **Decisão registrada**: a ideia original pedia "buffs permanentes". Foi trocada por desbloqueio de conteúdo + galho por raça, para evitar que o jogo vire grind-para-vencer e para preservar a dificuldade entre runs.
- **Fora de escopo desta mudança**: multiplayer, narrativa ramificada, som e música finais, integração Steamworks, localização, mais de uma raça, mais de um bioma de mapa.

## Capabilities

### New Capabilities
- `run-loop`: ciclo de dia, fases, avanço de tempo, condição e pontuação de Colapso, estado da run.
- `kingdom-grid`: grid de quadrados, compra e custo escalonado, tipos de terreno, adjacência e ocupação por edifícios.
- `card-system`: deck de run, compra/mão/descarte, energia diária, resolução de efeitos de carta sobre o grid.
- `threat-clock`: agendamento e telegrafia de ameaças, resolução de ataque, dano à base e destruição de quadrados.
- `day-events`: pool de eventos de fim de dia (positivos, negativos, neutros), sorteio ponderado e aplicação de efeitos.
- `races`: definição de raça, seleção antes da run, regras próprias que alteram o comportamento dos outros sistemas.
- `meta-progression`: moeda de meta, árvore de desbloqueio geral e galhos por raça, persistência entre runs.

### Modified Capabilities
<!-- Nenhuma: projeto novo, sem specs existentes. -->

## Impact

- **Novo repositório** `KingdomCollapse/` dentro de `D:\Projetos\Meu Jogo`, com árvore git independente; adicionado ao `.gitignore` do repo MM.
- **Projeto Unity novo** (URP, 3D), sem reuso de código do ProjectMM.
- **Dados em ScriptableObjects** (cartas, edifícios, terrenos, eventos, raças, nós de meta) para permitir balanceamento sem recompilar.
- **Persistência local** em JSON para a meta-progressão (a run em si não precisa de save nesta fatia).
- **Trabalho de editor** (cenas, prefabs, assets, configuração de ScriptableObjects) é do Matheus; o código C# é gerado aqui.
- **Dependências externas**: nenhuma nesta fatia. Steamworks.NET entra em mudança futura.
