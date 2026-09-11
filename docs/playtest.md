# Playtest

## 2026-09-11 — `add-hud-and-terrain-redesign`

Primeira run jogável depois do redesign de HUD/terreno. Achados do Matheus:

**Funcionando:**
- Gameplay geral aprovada — "tá legal", jogo tomando forma.
- Popup de construção, seleção, log flutuante, terreno com textura/prop 3D: sem reclamação.

**Achado — população presa em 0 (causa raiz, não é bug de HUD):**
Os 7 assets de prédio (`farm`, `sawmill`, `mine`, `docks`, `outpost`, `watchtower`, `hall`)
e o asset de raça (`humans`) nunca foram atualizados depois da reescrita da economia de
cinco recursos (`add-rival-kingdoms-economy`, grupos 1-2). Nenhum prédio preenche
`_production`, `_cost`, `_workersRequired` ou `_populationCapacity` — só sobrou o
`_baseGoldProduction` antigo. Sem capacidade de população em nenhum prédio, o teto fica
em 0 e `RunEngine.ResolvePopulationGrowth` nunca libera crescimento, não importa quanta
comida sobre.

**Consequência — jogo fácil demais:**
Mesma causa: sem prédio produzindo comida/madeira/pedra, a pressão dos cinco recursos
não existe em jogo ainda, só ouro. Isso é conteúdo (`[Editor]`), não regra — a regra já
foi testada e está correta (276 testes verdes).

**Achado — informação ainda densa:**
Mesmo com ícones, o painel esquerdo ainda parece "muita informação agrupada" (palavras
do Matheus). Os ícones ajudaram mas não resolveram sozinhos — pode precisar de mais
espaçamento, ou mover conteúdo secundário pra fora da visão padrão.

**Achado — eventos aleatórios sem impacto visual:**
O jogador só sabe que um evento aconteceu lendo o log. A encenação do dia já inclui o
evento como um passo (`DayPresentation`), mas só como texto (headline), sem ícone nem
efeito gráfico — diferente do que já existe para clima (partícula, luz) e ataque
(cena própria). Pedido do Matheus: mais elementos gráficos, menos texto, para reduzir
poluição visual.

**Não corrigido nesta run** (fora do escopo de `add-hud-and-terrain-redesign`, pertence
ao conteúdo de `add-rival-kingdoms-economy`):
- Preencher `_cost`/`_production`/`_workersRequired`/`_populationCapacity` nos 7 prédios.
- Preencher recursos iniciais (`_startingResources` ou equivalente) na raça `humans`.
