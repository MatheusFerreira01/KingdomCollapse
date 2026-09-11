## Why

O jogo hoje é ilegível para playtest: terreno é cubo de cor chapada, recursos e status
são só texto, o clima aparece um segundo e some, e não dá pra saber se uma carta ou
construção é compatível sem tentar. O retorno visual entregue em
`add-rival-kingdoms-economy` (grupo 3) resolveu a encenação do dia e os números na
origem, mas não toca terreno, ícones, clima persistente nem os menus — e sem isso
o jogador não consegue julgar se a economia nova é divertida. Por isso esta mudança
pausa o avanço em `add-rival-kingdoms-economy` (grupos 4 em diante) até o jogo ficar
jogável de verdade.

## What Changes

- Terreno passa de cubo colorido para arte real por tipo (planície, floresta,
  ruína, mina, rio), com tooltip ao passar o mouse explicando o tipo — substitui o
  painel de seleção como forma de identificar terreno.
- Recursos, energia, defesa e população no painel passam de texto para ícone + valor.
- Clima vira elemento fixo no canto superior direito: ícone com tooltip explicando o
  efeito de hoje e uma seta apontando a previsão de amanhã. O cenário reage ao clima
  — luz direcional muda de cor/intensidade, e partículas por tipo de clima (chuva,
  vento seco, brilho de sol). Célula com bônus ganha partícula dourada; com penalidade,
  partícula preta.
- Relógio de ameaça sobe para o cabeçalho, ao lado do dia: "Dia X / ameaça em Y",
  com animação de número verde (+N, ameaça adiada) ou vermelho (-N, ameaça antecipada)
  quando um evento muda a data.
- Log deixa de ser barra fixa e vira caixa flutuante semi-transparente ao lado da mão,
  aberta com Enter e fechada pelo X.
- Construir em uma célula abre um menu central com os assets de cada construção
  disponível; fundo amarelo indica compatibilidade com o terreno selecionado, sem cor
  indica incompatível.
- **BREAKING** (comportamento, não dado salvo): clicar numa célula compravel deixa de
  comprar direto — primeiro seleciona, permitindo que cartas com alvo em célula
  compravel (ex.: doar terreno) recebam alvo corretamente. Corrige o bug em que essas
  cartas nunca resolviam.
- Cartas sem arte própria continuam funcionais (nome + ícone de custo + texto de
  regra), já que nem toda carta vai ter ilustração nesta leva.

## Capabilities

### New Capabilities
- `hud-icons`: recursos, energia, defesa, população e relógio de ameaça exibidos como
  ícone + valor no cabeçalho, com animação de delta no relógio de ameaça.
- `terrain-art`: aparência real por tipo de terreno e tooltip de identificação no hover.
- `weather-scenery`: clima como elemento persistente com tooltip, luz de cena e
  partículas reagindo ao clima e a buffs/debuffs de célula.
- `building-placement`: menu central de construção ao selecionar célula, com destaque
  de compatibilidade.

### Modified Capabilities
(nenhuma — o bug de alvo em célula compravel é a UI não cumprindo o já especificado em
`card-system` / "Requirement: Alvo de carta"; não muda o requisito, corrige a
implementação. Log flutuante e cartas sem arte são mudanças de apresentação sem
requisito novo, cobertas nas tarefas.)

## Impact

- `ProjectKC/Assets/Scripts/Game/View/TerrainVisuals.cs`, `GridView.cs`, `TileView.cs`
  — troca de material chapado por prefab/sprite real.
- `ProjectKC/Assets/Scripts/Game/Runtime/GameBootstrap.cs` — reescrita do HUD (OnGUI
  atual sai ou é substituído por UI Toolkit/Canvas, a decidir em design.md), incluindo
  `HandleClick` (raiz do bug de alvo).
- Novo: sistema de partículas e luz reativa a clima (`WeatherDefinition` já existe no
  Core; a ponte visual é nova, camada Game).
- Assets novos: modelos/sprites de terreno e construção, ícones de recurso — fonte
  externa gratuita (Kenney.nl e similares), trabalho de import é `[Editor]`.
- `openspec/changes/add-rival-kingdoms-economy/tasks.md` — grupos 4–11 pausados até
  esta mudança arquivar.
