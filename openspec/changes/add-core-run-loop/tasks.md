> Tarefas marcadas **[Editor]** são trabalho do Matheus dentro do Unity (cenas, prefabs, assets, preencher ScriptableObjects). As demais são código C# gerado aqui.

## 1. Fundação do projeto

- [x] 1.1 [Editor] Criar o projeto Unity `ProjectKC/` (URP, 3D) na raiz de `KingdomCollapse/` e verificar que abre sem erros no Console
- [x] 1.2 Adicionar `.gitignore` de Unity na raiz de `KingdomCollapse/` e verificar com `git status` que `Library/`, `Temp/`, `Logs/` e `Build/` não aparecem
- [x] 1.3 Criar os assembly definitions `KingdomCollapse.Core` (sem referência a UnityEngine), `KingdomCollapse.Game` (referencia Core) e `KingdomCollapse.Tests.EditMode` (referencia Core), e verificar que o projeto compila com os três presentes
- [x] 1.4 Adicionar um teste de EditMode trivial e verificar que ele roda e passa no Test Runner
- [x] 1.5 Commit inicial do repositório e verificar com `git log` que o histórico do KingdomCollapse é independente do repositório do MM

## 2. Modelo de grid

- [x] 2.1 Implementar `TerrainType` (planície, floresta, mina, rio, ruína) e `Tile` (coordenada, terreno, possuído, revelado, edifício, destruído), verificando por teste que um tile novo nasce não possuído e não revelado
- [x] 2.2 Implementar `KingdomGrid` com dicionário esparso, consulta de adjacência ortogonal e enumeração de células compráveis, verificando por teste que um reino de 1 célula oferece exatamente 4 compráveis
- [x] 2.3 Implementar a compra de célula com débito de ouro, revelação de terreno e custo escalonado por quantidade possuída, verificando por teste que a compra sem ouro é rejeitada sem alterar estado e que o custo com 9 células é maior que com 1 (spec `kingdom-grid`)
- [x] 2.4 Implementar geração de terreno com semente por run, verificando por teste que a mesma semente gera o mesmo mapa e sementes distintas geram mapas distintos
- [x] 2.5 Implementar construção e demolição de edifício com validação de terreno e ocupação, verificando por teste que construir em terreno incompatível ou em célula ocupada é rejeitado (spec `kingdom-grid`)
- [x] 2.6 Implementar cálculo de produção com bônus de adjacência e detalhamento por célula, verificando por teste que uma Serraria com 2 florestas adjacentes produz mais que uma sem, e que o detalhamento explica a diferença
- [x] 2.7 Implementar destruição de célula preservando posse e permitindo reconstrução, verificando por teste que uma célula destruída para de produzir e continua possuída

## 3. Sistema de cartas

- [x] 3.1 Definir `CardDefinition` (custo de energia, requisito de alvo, lista de efeitos, raças permitidas) como ScriptableObject espelhado por um tipo puro em Core, verificando que o asset aparece no menu Create do Unity
- [x] 3.2 Implementar `RunDeck` com deck, mão e descarte, compra e reciclagem do descarte, verificando por teste que a mão é reabastecida, que o descarte é embaralhado de volta quando o deck esvazia e que deck e descarte vazios não causam erro (spec `card-system`)
- [x] 3.3 Implementar energia diária com recarga no Início do Dia e sem acúmulo, verificando por teste que uma carta cara é rejeitada sem gastar energia e que a sobra não passa para o dia seguinte
- [x] 3.4 Implementar seleção e validação de alvo com cancelamento gratuito, verificando por teste que alvo inválido e cancelamento deixam mão e energia intactas
- [x] 3.5 Implementar descarte no Fim do Dia com suporte a carta retida, verificando por teste que a mão é esvaziada e que a carta retida permanece
- [x] 3.6 Implementar adição de cartas ao deck da run durante a run, verificando por teste que a carta concedida entra no descarte e que o deck da run seguinte não a contém (spec `card-system`)

## 4. Efeitos componíveis

- [x] 4.1 Definir o contrato de efeito (aplicar sobre o estado da run com um contexto de alvo) e o resultado de aplicação, verificando por teste que um efeito nulo não altera o estado
- [x] 4.2 Implementar os efeitos da fatia vertical — `GainGold`, `LoseGold`, `BuildOn`, `DestroyTile`, `RepairBase`, `DamageBase`, `AddDefense`, `DrawCards`, `RevealTile`, `AddCardToDeck`, `GrantTile`, `ModifyProduction` — com um teste por efeito cobrindo o caso normal e o caso sem alvo válido
- [x] 4.3 Fazer cartas e eventos consumirem o mesmo conjunto de efeitos, verificando por teste que um mesmo efeito aplicado por carta e por evento produz estado idêntico (design D3)

## 5. Ciclo de dia

- [x] 5.1 Implementar `DayPhase` e a máquina de estados Início do Dia → Planejamento → Resolução → Evento → Fim do Dia, verificando por teste que as fases avançam nessa ordem
- [x] 5.2 Rejeitar comandos do jogador fora do Planejamento, verificando por teste que jogar carta ou comprar célula nas outras fases não altera o estado da run (spec `run-loop`)
- [x] 5.3 Aplicar a produção diária antes de evento e ameaça, verificando por teste que num dia com produção e ataque o ouro é creditado antes de o ataque resolver
- [x] 5.4 Implementar a detecção de Colapso por integridade zerada e por perda da última célula, verificando por teste que ambos os caminhos encerram a run imediatamente
- [x] 5.5 Implementar o cálculo de pontuação (dias, células, edifícios, marcos) e a conversão em moeda de meta, verificando por teste que duas runs com o mesmo reino pontuam diferente quando uma durou mais dias
- [x] 5.6 Emitir eventos de domínio do núcleo para a camada de apresentação, verificando por teste que resolver um dia completo emite a sequência esperada de eventos

## 6. Relógio de ameaça

- [x] 6.1 Implementar a fila de ameaças agendadas com dia de chegada, força e tipo, verificando por teste que toda ameaça resolvida foi anunciada com pelo menos 2 dias de antecedência (spec `threat-clock`)
- [x] 6.2 Implementar a curva de força em função do dia e do número de células, exposta como ScriptableObject de curva, verificando por teste que mais células no mesmo dia produzem ameaça mais forte e que a força não estabiliza com o avanço dos dias
- [x] 6.3 Implementar a resolução de ataque contra a defesa total, com excedente destruindo células a partir da borda e o restante ferindo a base, verificando por teste que defesa suficiente anula o ataque e que o excedente destrói na ordem esperada
- [x] 6.4 Produzir o detalhamento do ataque (força, defesa, perdas) consumível pela UI, verificando por teste que os três valores aparecem discriminados no resultado

## 7. Eventos de fim de dia

- [x] 7.1 Definir `EventDefinition` (classe, condições de elegibilidade, opções de escolha, efeitos) como ScriptableObject espelhado por um tipo puro, verificando que o asset aparece no menu Create
- [x] 7.2 Implementar o sorteio ponderado por classe com no máximo um evento por dia, verificando por teste que uma run simulada de 30 dias tem maioria de eventos positivos e neutros somados (spec `day-events`)
- [x] 7.3 Implementar a filtragem por elegibilidade e o bloqueio de repetição imediata, verificando por teste que um evento condicional sem condição satisfeita fica fora do sorteio e que um evento sorteado não repete no dia seguinte
- [x] 7.4 Limitar o dano de evento para que nunca zere a base nem remova a última célula, verificando por teste que um evento de dano letal deixa a integridade em 1 e a run continua
- [x] 7.5 Implementar eventos com escolha, aplicando apenas os efeitos da opção escolhida, verificando por teste que a opção não escolhida não produz efeito
- [x] 7.6 Implementar o orçamento de severidade por classe — fração máxima de ouro, proibição de destruir célula construída, no máximo uma carta removida — verificando por teste que um evento que excede o orçamento tem o efeito recortado e não aplicado por inteiro (design D10, spec `day-events`)
- [x] 7.7 Adicionar uma verificação automatizada sobre o catálogo de eventos que falha se algum evento declarar efeito acima do orçamento da sua classe ou escalar com o dia, verificando que ela acusa um evento propositalmente fora do orçamento
- [x] 7.8 Garantir que eventos não agendem nem resolvam ameaças, permitindo apenas alterar a força de uma ameaça já anunciada com atualização imediata do relógio, verificando por teste que nenhum evento produz ataque no mesmo dia (spec `day-events`)
- [x] 7.9 Expor a classe do evento (positivo, negativo, neutro) no dado apresentado à UI, verificando por teste que todo evento do catálogo declara sua classe

## 8. Raças

- [x] 8.1 Definir `RaceDefinition` como ScriptableObject com estado inicial, listas de conteúdo permitido/proibido e modificadores de regra nomeados, verificando que o asset aparece no menu Create
- [x] 8.2 Implementar a consulta de modificadores de regra pelos sistemas com fallback para o comportamento padrão, verificando por teste que uma raça sem nenhum modificador declarado joga exatamente como o baseline (design D8)
- [x] 8.3 Implementar a montagem do estado inicial da run a partir da raça e do conteúdo desbloqueado, verificando por teste que conteúdo bloqueado ou proibido pela raça não aparece em nenhum pool da run (specs `races`, `run-loop`)
- [ ] 8.4 [Editor] Preencher o asset da raça Humanos (baseline, +1 carta por dia) e verificar em jogo que a run inicia com os valores declarados
- [x] 8.5 Implementar a trava que impede troca de raça durante a run, verificando por teste que nenhum comando altera a raça após o início

## 9. Meta-progressão

- [x] 9.1 Implementar o modelo de perfil (saldo, nós comprados, estatísticas, versão do formato) e a serialização JSON, verificando por teste que serializar e desserializar preserva o perfil
- [x] 9.2 Implementar a persistência em `Application.persistentDataPath` com recuperação de arquivo ausente e de arquivo corrompido — renomeando o ilegível para `.corrupt` — verificando por teste que ambos os casos produzem perfil novo sem exceção (spec `meta-progression`)
- [x] 9.3 Definir `MetaNodeDefinition` (custo, pré-requisitos, conteúdo desbloqueado, galho de raça) como ScriptableObject e implementar a validação de compra, verificando por teste que um nó com pré-requisito faltante não pode ser comprado mesmo com saldo suficiente
- [x] 9.4 Implementar o crédito de moeda ao Colapso, verificando por teste que uma run encerrada no dia 3 ainda rende moeda não nula
- [x] 9.5 Implementar galhos por raça com desbloqueio exclusivo daquela raça e independência entre galhos, verificando por teste que conteúdo do galho dos Anões não aparece numa run de Elfos
- [x] 9.6 Adicionar uma verificação automatizada de que nenhum nó do catálogo concede bônus numérico permanente, verificando que o teste falha se um nó desses for adicionado (spec `meta-progression`)

## 10. Conteúdo da fatia vertical

- [ ] 10.1 [Editor] Criar ~10 edifícios cobrindo os 5 terrenos, com produção, custo, defesa e regras de adjacência, verificando em jogo que cada um só pode ser construído no terreno correto
- [ ] 10.2 [Editor] Criar ~15 cartas usando apenas os efeitos da tarefa 4.2, verificando em jogo que cada carta resolve sem erro no alvo válido e é recusada no inválido
- [ ] 10.3 [Editor] Criar ~18 eventos a partir de `docs/eventos-rascunho.md`, distribuídos em aproximadamente 6 positivos, 6 neutros e 6 negativos, incluindo ao menos 3 com escolha, verificando em jogo que cada um aparece, resolve corretamente e passa na verificação de orçamento da tarefa 7.7
- [ ] 10.4 [Editor] Criar ~8 nós de meta — tronco geral e galho dos Humanos — verificando em jogo que a compra desbloqueia o conteúdo esperado na run seguinte
- [ ] 10.5 [Editor] Criar os tipos de ameaça da fatia vertical (horda, incêndio, colapso estrutural) com sua curva de força, verificando em jogo que cada um é telegrafado e resolvido

## 11. Apresentação e UI

- [ ] 11.1 [Editor] Montar a cena de jogo com câmera ortográfica isométrica fixa e iluminação base, verificando que o reino inteiro permanece enquadrado até ~25 células
- [ ] 11.2 [Editor] Criar os prefabs de bloco por terreno e as sobreposições de edifício em baixo-poli, verificando visualmente que cada terreno é distinguível a partir do ângulo fixo
- [x] 11.3 Implementar a view do grid que reage aos eventos de domínio — surgir, construir, destruir — verificando em jogo que comprar uma célula a faz aparecer com o terreno correto
- [x] 11.4 Implementar a seleção de célula e o realce de células compráveis com o custo visível, verificando em jogo que apenas células adjacentes ao território são realçadas
- [ ] 11.5 Implementar a UI de mão, energia e ouro, com arrastar-para-alvo e cancelamento, verificando em jogo que uma carta cancelada volta para a mão sem custo
- [ ] 11.6 Implementar a UI do relógio de ameaça mostrando dias restantes e força prevista, e o aviso de aumento de pressão antes da compra de célula, verificando em jogo que comprar uma célula altera visivelmente a previsão (design D6)
- [ ] 11.7 Implementar a UI de evento com opções de escolha, verificando em jogo que os efeitos só são aplicados após a escolha
- [ ] 11.8 Implementar o botão de Fim do Dia com a sequência encenada de Resolução, Evento e transição de dia, verificando em jogo que a ordem apresentada corresponde à ordem da simulação
- [ ] 11.9 Implementar a tela de Colapso com placar discriminado e moeda de meta ganha, verificando em jogo que os componentes da pontuação batem com o cálculo do núcleo
- [ ] 11.10 Implementar as telas de seleção de raça e de árvore de meta, verificando em jogo que conteúdo bloqueado aparece bloqueado e que a compra de um nó persiste após reiniciar o jogo

## 12. Balanceamento e validação

- [x] 12.1 Implementar um harness de simulação em lote que roda N runs com uma IA heurística simples e reporta a distribuição do dia de Colapso, verificando que 1000 runs rodam em segundos sem abrir cena
- [ ] 12.2 Ajustar as curvas de custo, produção e ameaça até que o dia mediano de Colapso caia entre 25 e 35 na simulação, verificando pelo relatório do harness (spec `run-loop`)
- [ ] 12.3 Jogar 5 runs completas manualmente e registrar em `docs/playtest.md` a duração real, os momentos de tédio e os momentos de injustiça percebida
- [ ] 12.4 Corrigir os problemas de ritmo apontados pelo playtest e confirmar por uma segunda rodada de 5 runs registrada no mesmo documento
- [ ] 12.5 Verificar que a suíte completa de EditMode passa e que o jogo roda uma run do dia 1 ao Colapso sem erro no Console

## 14. Clima

- [x] 14.1 Implementar `WeatherDefinition` (bônus por terreno, multiplicador de produção, custo de construção, energia, dano limitado, bloqueios, efeito sobre ameaça) e o pool de sorteio, verificando por teste que todo clima do catálogo declara vantagem e desvantagem (spec `weather`)
- [x] 14.2 Implementar o clima vigente e a previsão de um dia no ciclo de dia, verificando por teste que todo dia tem clima e que o clima previsto é exatamente o que vigora no dia seguinte
- [x] 14.3 Aplicar os efeitos do clima sobre produção por terreno, multiplicador do dia, custo de construção e energia, verificando por teste que chuva favorece rio e encarece obra, e que seca inverte o favorecimento
- [x] 14.4 Aplicar o efeito do clima sobre ameaças no momento da previsão — redução de força e adiamento — verificando por teste que a força resolvida é igual à última força exibida no relógio (design D13, spec `weather`)
- [x] 14.5 Implementar bloqueios de ação por clima com motivo comunicado, verificando por teste que a neblina recusa a compra de célula informando a razão
- [x] 14.6 Adicionar verificação automatizada do catálogo de climas que falha se algum clima destruir célula, remover carta, ser letal, exceder o teto de produção ou não ter os dois lados
- [ ] 14.7 [Editor] Criar os 6 climas iniciais — sol, chuva, seca, tempestade, neblina, dia limpo — verificando em jogo que cada um aparece, é previsto e resolve corretamente
- [ ] 14.8 Exibir clima de hoje e previsão de amanhã no HUD, destacando quando o clima alterou a força ou o dia de uma ameaça anunciada

## 13. Preparação comercial

- [x] 13.1 Registrar em `docs/steam.md` o checklist do Steam Direct — taxa, formulários fiscais, requisitos de página, capsules, política de demo e Next Fest — verificando que o documento cobre cada item com prazo estimado
- [ ] 13.2 Definir o nome final do jogo e verificar a disponibilidade na Steam e em domínio, registrando a decisão em `docs/steam.md` (questão aberta em `design.md`)
- [ ] 13.3 Gravar um GIF e um vídeo curto de uma run completa a partir da build da fatia vertical, verificando que ambos mostram expansão do grid, uso de carta, ataque telegrafado e Colapso
