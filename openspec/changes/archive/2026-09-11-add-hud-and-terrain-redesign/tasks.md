> Tarefas marcadas **[Editor]** são trabalho do Matheus dentro do Unity (baixar e
> atribuir assets em campos já existentes no Inspector). As demais são código C#.
>
> Ordem importa: o grupo 1 corrige o clique quebrado e cria o popup que depois os
> demais grupos reaproveitam (comprar, construir, ver ameaça). Deixa isso destravado
> primeiro.

## 1. Seleção de célula e menu de construção

- [x] 1.1 Corrigir `GameBootstrap.HandleClick` para que clicar numa célula fantasma
      (comprável) selecione em vez de comprar direto, verificando em jogo que a
      célula fica marcada como selecionada e que uma carta com
      `TargetRequirement.PurchasableTile` na mão passa a resolver ao ser jogada
      com essa seleção (spec `card-system`, Requirement: Alvo de carta)
- [x] 1.2 Implementar o popup central de construção, substituindo a lista de botões
      atual: abre ao selecionar célula construível, fecha ao selecionar outra,
      verificando em jogo que selecionar uma célula abre o menu e selecionar outra
      troca o conteúdo (spec `building-placement`)
- [x] 1.3 Adicionar opção "Comprar" no popup quando a célula é comprável e não
      possuída, no lugar da lista de edifícios, verificando em jogo que comprar pelo
      popup produz o mesmo resultado que o clique direto antigo
- [x] 1.4 Colorir o fundo de cada opção do popup: amarelo se a construção é compatível
      com o terreno selecionado, sem cor se não é, verificando em jogo com uma célula
      de cada terreno que a cor muda conforme a compatibilidade (spec
      `building-placement`)
- [x] 1.5 Levantar as demais cartas do catálogo com `TargetRequirement` em célula não
      possuída e confirmar (ou corrigir) que cada uma resolve com a seleção corrigida,
      verificando em jogo uma partida com cada carta desse tipo na mão
- [x] 1.6 Renderizar um retrato 3D de cada prédio com prefab atribuído (câmera
      escondida, uma vez por run) e mostrar no popup de construção no lugar do texto
      puro, com fallback para texto quando o prédio não tem prefab, verificando em
      jogo que o Salão mostra a torre e um prédio sem modelo continua com texto
      (pedido do Matheus, playtest de 2026-09-11)
- [x] 1.7 [Editor] Localizar modelo 3D para os prédios da economia que ainda não têm
      (Fazenda, Serraria, Mina, Ancoradouro, Posto Avançado, Torre de Vigia) e
      atribuir no campo `Prefab` de cada `BuildingAsset`, verificando em jogo que o
      popup mostra o retrato certo — achei Fazenda (moinho) e Serraria (moinho
      d'água) no Kenney Fantasy Town Kit; Mina/Ancoradouro/Posto/Torre continuam sem
      modelo, não achei nada que combinasse sem forçar

## 2. HUD em ícone

- [x] 2.1 Adicionar campo de ícone por `ResourceKind` (ouro, madeira, pedra, comida,
      população) e para energia e defesa, com fallback para o texto atual quando o
      ícone não está atribuído, verificando em jogo que o painel mostra ícone quando
      atribuído e texto quando não (spec `hud-icons`)
- [x] 2.2 Mover o relógio de ameaça para o cabeçalho, ao lado do dia, mantendo o
      destaque de recurso em falta, verificando em jogo que dia e próxima ameaça
      aparecem juntos no topo (spec `hud-icons`)
- [x] 2.3 Estender `FloatingNumbers` com modo ancorado em coordenada de tela fixa (em
      vez de posição de mundo projetada), verificando por teste que a mesma lógica de
      subida e desaparecimento funciona com os dois tipos de âncora
- [x] 2.4 Animar o delta do relógio de ameaça — verde e positivo quando um evento
      adia a ameaça, vermelho e negativo quando antecipa — usando o modo ancorado em
      tela, verificando em jogo com um evento de cada tipo (spec `hud-icons`)

## 3. Log flutuante

- [x] 3.1 Substituir a barra de log fixa por uma caixa flutuante ao lado da mão,
      com fundo semi-transparente, verificando em jogo que o texto continua legível
      sobre o tabuleiro (spec proposal — "Log deixa de ser barra fixa")
- [x] 3.2 Abrir a caixa com Enter e fechar com um X visível, verificando em jogo que
      as duas ações funcionam e que o estado (aberto/fechado) não afeta o registro de
      eventos

## 4. Arte de terreno

- [x] 4.1 Adicionar campo de sprite/prefab por `TerrainType` no asset de conteúdo,
      usado por `TerrainVisuals` no lugar da cor chapada quando preenchido, com
      fallback para a cor atual quando vazio, verificando em jogo que atribuir um
      sprite troca a aparência da célula (spec `terrain-art`)
- [x] 4.2 Implementar a descrição ao passar o mouse sobre uma célula, com o nome do
      terreno e o que ele significa para o jogo, verificando em jogo que a descrição
      aparece ao pairar e some ao tirar o mouse (spec `terrain-art`)
- [x] 4.3 [Editor] Localizar e baixar um pacote de arte gratuita (baixo-poli, licença
      livre — ex. Kenney.nl) cobrindo planície, floresta, mina, rio e ruína, e
      atribuir cada peça no campo criado em 4.1, verificando em jogo que os cinco
      terrenos ficam visualmente distintos
- [x] 4.4 Estender `TerrainVisualSet`/`GridView` para aceitar um prefab 3D opcional
      por terreno, instanciado sobre a célula (mesmo padrão do marcador de
      construção), preferido sobre a textura quando os dois estiverem preenchidos,
      verificando em jogo que atribuir um prefab troca a aparência da célula sem
      quebrar colisão/clique (pedido do Matheus — elementos 3D, não só textura)
- [x] 4.5 [Editor] Baixar um pacote 3D baixo-poli gratuito (ex. Kenney Nature Kit)
      com pelo menos árvore e rocha, montar um prefab simples por terreno e atribuir
      no campo criado em 4.4, verificando em jogo que floresta e mina mostram
      elemento 3D em vez de textura plana
- [x] 4.6 Reposicionar a decoração de terreno para os cantos da célula em vez do
      centro, deixando o meio livre para o prédio caber sem sobrepor, verificando em
      jogo que floresta/mina mostram árvore ou rocha na borda com o prédio visível no
      centro (pedido do Matheus, playtest de 2026-09-11)
- [x] 4.7 Implementar conector visual entre células possuídas vizinhas — estrada, ou
      ponte quando a conexão cruza rio — com fallback procedural sem prefab
      atribuído, verificando em jogo que toda dupla de células adjacentes possuídas
      mostra a faixa entre elas (pedido do Matheus, playtest de 2026-09-11)

## 5. Clima e cenário reativo

- [x] 5.1 Substituir o aviso temporário de clima por um ícone fixo no canto superior
      direito, verificando em jogo que o ícone permanece visível durante todo o dia
      (spec `weather-scenery`)
- [x] 5.2 Implementar o tooltip do ícone de clima, explicando o efeito de hoje e
      apontando a previsão de amanhã, verificando em jogo que o hover mostra os dois
      climas distinguíveis (spec `weather-scenery`)
- [x] 5.3 Implementar a reação de luz de cena ao clima do dia (ex.: sol mais forte e
      amarelado), verificando em jogo que a luz muda perceptivelmente entre climas
      diferentes (spec `weather-scenery`)
- [x] 5.4 Implementar partícula por clima (chuva, seca, sol), criada por código sem
      prefab, verificando em jogo que a partícula certa aparece e some quando o clima
      muda (spec `weather-scenery`)
- [x] 5.5 Implementar partícula dourada sobre célula com bônus e partícula preta sobre
      célula com penalidade, verificando em jogo que aparecem sem seleção e somem
      quando o efeito termina (spec `weather-scenery`)
- [x] 5.6 [Editor] Baixar ícones de clima (sol, chuva, seca) de fonte gratuita e
      atribuir no campo criado em 5.1, verificando em jogo que cada clima mostra o
      ícone certo

## 6. Cartas sem arte

- [x] 6.1 Adicionar campo de arte opcional em `CardAsset`, com o layout atual
      (nome + custo + texto de regra) como fallback quando vazio, verificando em jogo
      que uma carta sem arte continua jogável e legível
- [x] 6.2 Rodar uma partida cobrindo todas as cartas do catálogo atual e registrar em
      `docs/playtest.md` qualquer carta que ainda não resolva corretamente, corrigindo
      antes de fechar esta mudança

## 7. Fechamento

- [x] 7.1 Rodar a suíte completa (`dotnet test` em `tools/CoreTests`) e confirmar que
      nenhum teste existente quebrou com as mudanças de `CardDefinition`/efeitos
- [x] 7.2 Jogar uma run completa cobrindo os 7 pontos do pedido original e confirmar
      em `docs/playtest.md` que cada um está resolvido
- [x] 7.3 Atualizar `docs/roadmap.md` e o cabeçalho de
      `openspec/changes/add-rival-kingdoms-economy/tasks.md` retomando os grupos 4–11
      agora que o jogo está jogável
