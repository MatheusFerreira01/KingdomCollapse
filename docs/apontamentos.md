# Apontamentos — KingdomCollapse

Documento de consulta escrito em 2026-11-16, depois de duas sessões de trabalho (10 e 16 de novembro).
Registra o que existe, o que foi decidido, o que **não** foi testado e o que eu faria em seguida.

> Tudo isto entrou em commit em 2026-11-16, num commit só (o roteiro abaixo sugeria partes; os arquivos se entrelaçam demais para separar sem quebrar a compilação).
> A pasta `.claude-sync/` na raiz é só transporte de arquivos e pode ser apagada.

## 1. Estado do jogo

O jogo roda de ponta a ponta com a cena vazia mais o componente `GameBootstrap`: toda a interface é IMGUI desenhada por código, e todo o conteúdo vem de `StarterContent` (modo `BuiltIn`). O `GameDatabase` do Inspector continua existindo, mas raças novas, encontros e árvores só existem no conteúdo embutido.

| Parte | Onde está |
|---|---|
| Regras, conteúdo, simulador | `Assets/Scripts/Core/` (sem dependência do Unity) |
| Conteúdo embutido (raças, cartas, eventos, árvores) | `Core/Content/StarterContent.cs` |
| Fluxo, entrada, eventos do Core | `Game/Runtime/GameBootstrap.cs` |
| Tela da run, tutorial, janela de escolha | `Game/Runtime/GameBootstrap.Hud.cs` |
| Menu, nível, raça, árvore, pausa, fim de run | `Game/Runtime/GameBootstrap.Screens.cs` |
| Estruturas e superfície das células | `Game/View/BuildingVisuals.cs` |
| Tabuleiro, seleção, cena do ataque | `Game/View/GridView.cs` |
| Áudio (efeitos e música, tudo sintetizado) | `Game/UI/UiSound.cs` |

## 2. Decisões tomadas

- **O jogo é run a run.** Não há salvar no meio; sair perde a run. Só o perfil (moedas, árvore, níveis) persiste.
- **Novo Jogo** zera o perfil e abre a run guiada. A primeira run de qualquer perfil novo tem tutorial de 6 passos.
- **Comprar célula pede dois cliques.** Um clique só gastava ouro por engano.
- **A árvore geral vale para todas as raças;** cada raça tem um galho próprio de 6 nós em duas colunas.
- **Teto de poder por nível** (16 / 28 / 40) é verificado por teste. Por isso os bônus numéricos novos ficaram quase todos nos níveis 2 e 3, e os primeiros degraus dos galhos são cartas.
- **Defesa concedida por evento vale para o dia seguinte.** O evento acontece depois dos ataques; antes ela era zerada sem defender nada.
- **O clima não fere a base no dia 1.**
- **Encontros usam o sistema de eventos:** três saídas, e a que acolhe deixa uma carta no baralho. Presença no mapa e encadeamento, que o planejamento original pedia, não foram feitos.

## 3. Raças

| | Humanos | Orcs | Elfos | Anões |
|---|---|---|---|---|
| Ideia | equilíbrio e comércio | saque | preservar a floresta | pouco chão, bem defendido |
| Regra própria | +1 carta por dia | ataque repelido rende +10 comida e +8 ouro | floresta rende +3 madeira, +2 comida, +2 ouro sem gente | rochedo vazio rende +2 pedra e +2 ouro |
| Guarnição | normal | +25% | +40% | +15% |
| Restrição | — | sem Ancoradouro e Mercado | não constrói em floresta, sem Serraria | célula 50% mais cara, até 3 casas do Salão |
| Base | 20 | 24 | 20 | 24 |
| Cartas iniciais | Mercadores, Censo | Investida, Tambores de Guerra | Colheita Silvestre, Saraivada | Veio de Ouro, Bigorna |
| Estruturas | conjunto original | conjunto próprio | conjunto próprio | as dos Humanos, em pedra e bronze |
| Vitórias do bot, nível 1 | 61,4% | 40,2% | 46,8% | 61,8% |

Os Anões são uma primeira versão: o "empilhar melhorias na mesma célula" do roteiro não existe.

## 4. Balanceamento

Medido com `StewardPolicy`, 500 runs por configuração. Alvo do nível 1 para o bot: 40% a 65%.

- Nível 2: 8,4%. Nível 3: 2,0%. Medidos **sem** árvore comprada, com Humanos, antes dos encontros e dos Anões.
- Subir o ouro do Salão de 3 para 4 levou os Humanos a 68%. Voltei para 3 e dei +8 de ouro inicial.
- Os Anões saíram a 93,6% na primeira tentativa; foram ajustados em três rodadas.
- O bot não escolhe opção de encontro com critério próprio: os números com encontros dizem pouco sobre eles.

Para medir fora do Unity sem NuGet: compilar o Core com um `csproj` simples e chamar `SimulationHarness.RunBatch` com `StarterContent.CreateSetup(seed, nivel, raca)`.

## 5. O que não foi testado

Vale mais do que a lista do que foi feito. Nada abaixo foi visto ou ouvido funcionando:

- **Tela de Vitória.** Todas as runs que joguei terminaram em Colapso.
- **Áudio inteiro.** Não tenho como ouvir. Só sei que compila e que o jogo abre sem erro.
- **Cena do ataque no tabuleiro** (cubos vermelhos). O ataque foi adiado pelo clima no único teste.
- **Janela de escolha nova** (faixa "Você tem", linhas "Gasta" e "Ganha").
- **Encontros:** nenhum apareceu numa run jogada.
- **Anões:** nunca abertos no Unity. A tela de raça com quatro botões e a árvore com cinco abas também não.
- **Galhos de raça:** nenhum nó comprado; nenhuma das cartas de árvore foi jogada.
- **Painel esquerdo com dois ataques** e a barra de rolagem temática.
- **Superfície das células em tela cheia.** Só vi na janela pequena do editor.

O que foi conferido no Unity: menu, seleção de nível e raça, tutorial completo, Novo Jogo, compra em dois cliques, construção, cartas com e sem alvo, evento com escolha, ataque repelido, Colapso, compra de nó da árvore geral e efeito na run seguinte, saque dos Orcs, floresta dos Elfos, estruturas de Orcs e Elfos.

Os 386 testes do Core passam, mas rodados fora do Unity com um executor próprio, não pelo Test Runner. As mudanças desta rodada não ganharam teste novo.

## 6. Problemas conhecidos

- Quem não constrói Fazenda fica com zero de gente por volta do dia 4. O tutorial sugere a Fazenda, mas não avisa.
- O começo ainda é lento em ouro (+3 por dia com célula de 10 a 21).
- Comprar terreno desconhecido é aposta cega; só Batedores revela.
- O tampo novo das células cobre parte da cor de seleção e de alvo. Sobra um aro; se ficar difícil de ver, o caminho é tingir o tampo também.
- A dica de terreno e o painel "O que fazer" ainda falam de "clicar numa célula amarela", que não reflete a compra em dois cliques.
- O aviso "Deleting invalid font reference" aparece a cada recompilação. É inofensivo.
- Clique dentro do retângulo da janela flutuante do Claude não chega ao Unity. Isso me fez reportar um bug que não existia; vale lembrar em qualquer teste automatizado.

## 7. Próximos passos, em ordem

1. **Jogar uma run de cada raça até o fim** e fechar a lista da seção 5. É o que mais reduz risco.
2. ~~Fazer o commit~~ — feito em 2026-11-16.
3. **Medir níveis 2 e 3 com a árvore comprada** antes de mexer na curva deles.
4. **Testes para o que entrou sem teste:** defesa de evento valendo no dia seguinte, regra da floresta dos Elfos, saque dos Orcs, clima no dia 1, prévia de custo dos efeitos.
5. **Ensinar o bot a escolher em encontro,** ou os números de balanceamento com encontros não significam nada.
6. **Anões de verdade:** a mecânica de empilhar melhorias e um conjunto de estruturas próprio.
7. **Encontros com presença no mapa e encadeamento,** como o planejamento original descreve.
8. **Modelos no lugar dos blocos** e uma trilha gravada no lugar da sintetizada.
9. **Atualizar README, roteiro e `tasks.md` do OpenSpec,** que ainda descrevem o estado de setembro.
