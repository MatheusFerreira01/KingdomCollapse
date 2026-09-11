## Context

O HUD inteiro hoje é `OnGUI` (IMGUI) dentro de `GameBootstrap.cs`, comentado no
próprio código como andaime: "e andaime para provar o loop antes de existir UI de
verdade [...] As telas definitivas [...] são tarefas próprias e substituem isto." A
cena inteira (câmera, luz, grid, HUD) é montada por código em `BuildScene()`/`Start()`,
não por cena/prefab autorado — não existe Canvas nem UI Toolkit no projeto ainda.
`TerrainVisuals.cs` gera cor chapada por `TerrainType`, sem textura. A divisão de
trabalho do projeto ([[unity-editor-work-is-the-users]]) é: Claude escreve `.cs`,
Matheus faz o que exige interface do Unity — cenas, prefabs, `ScriptableObject`. Ver
`proposal.md` para o porquê.

## Goals / Non-Goals

**Goals:**
- Resolver os 7 pontos do pedido com o mínimo de dependência de trabalho manual no
  Editor, para não travar em ida-e-volta.
- Manter tudo dentro do padrão já estabelecido: conteúdo como `ScriptableObject`
  (`ContentAsset`), cena montada por código.
- Deixar clima, buff/debuff de célula e clique em terreno reagindo de forma unificada.

**Non-Goals:**
- Migrar o HUD inteiro para uGUI/UI Toolkit agora — é a "UI de verdade" que o próprio
  código já anuncia como tarefa própria futura; esta mudança melhora o IMGUI existente,
  não o substitui.
- Balancear cartas ou economia — o pedido é só destravar o que está quebrado (alvo em
  célula comprável) e garantir que carta sem arte funcione.
- Modelos 3D de alta fidelidade — arte gratuita de baixo-poli é aceitável e esperada.

## Decisions

### HUD continua IMGUI, ganha textura, tooltip e animação por cima
Trocar para uGUI agora significaria autorar Canvas e prefabs no Editor para cada
elemento (ícone, tooltip, popup, caixa de log) — trabalho grande do Matheus, e o
código já declara essa troca como tarefa própria e futura. Em vez disso, esta mudança
estende o IMGUI atual com o que falta: `GUI.DrawTexture` para ícone de recurso/clima,
um helper de tooltip (posição do mouse + timer de permanência, desenhado por último
para ficar por cima), `GUI.color` com alpha para a caixa de log transparente, e um
popup de `Rect` central para o menu de construção. Fica mais simples que o normal
porque não introduz um segundo paradigma de UI no meio do projeto.

**Alternativa descartada**: migrar para uGUI já. Rejeitada por inflar o escopo com
autoria de prefab quando o pedido é "quero jogar logo", e por duplicar trabalho que a
migração futura já vai refazer do zero de qualquer forma.

### Arte de terreno e construção via campo de asset no ScriptableObject
`TerrainType` continua enum no Core (sem mudar). A camada Game ganha um mapeamento
tipo → `Sprite`/prefab leve, num asset novo (`TerrainVisualSet` ou campo direto em
`GameDatabase`). Claude escreve o código que lê esse campo e usa o material com
textura se ele estiver preenchido, e cai na cor chapada atual se estiver vazio —
assim nada quebra enquanto os assets não chegam. Baixar os pacotes gratuitos
(Kenney.nl e afins) e arrastar cada peça no campo certo é `[Editor]`, mas é atribuição
simples num Inspector já existente, não autoria de cena nova.

Mesmo padrão para `BuildingDefinition`/`BuildingAsset`: campo de sprite/prefab por
construção, usado no menu central novo.

### Bug de alvo em célula comprável: clique agora seleciona, popup compra
A causa raiz (ver conversa) é `HandleClick` desviar direto para `TryBuy` em vez de
passar por `_gridView.Select()`. A correção: célula fantasma (comprável) também vira
seleção normal. O popup central de construção (`building-placement`) passa a exibir,
para célula comprável e não possuída, uma opção "Comprar" no lugar da lista de
edifícios — resolvendo o clique-direto-compra do jeito antigo sem esconder a ação.
Isso também é o que destrava cartas com `TargetRequirement.PurchasableTile`: elas
finalmente recebem `_gridView.Selected` como alvo válido.

### Delta do relógio de ameaça reaproveita a lógica de `FloatingNumbers`
`FloatingNumbers.cs` já sabe animar um número subindo e sumindo, ancorado em
posição de mundo projetada pra tela. Para o número verde/vermelho do cabeçalho,
adiciona-se um modo ancorado direto em coordenada de tela (o cabeçalho, fixo), em vez
de duplicar a lógica de easing num sistema paralelo.

### Partículas de clima e de buff/debuff são criadas por código, sem prefab
Seguindo o padrão de `BuildScene()` (câmera, luz e grid já são montados via
`AddComponent` em runtime, não por cena autorada), o `ParticleSystem` de cada clima e
de cada célula com bônus/penalidade é criado e configurado por código
(`ParticleSystem.MainModule`, `EmissionModule`, etc.), sem depender de prefab
desenhado no Editor. O visual fica mais simples que um prefab tunado à mão, e é um
trade-off aceito — ajuste fino de partícula fica para uma leva de polimento futura,
não é meta desta mudança.

## Risks / Trade-offs

- **IMGUI estendido fica mais feio que uGUI nativo** → aceito conscientemente: o
  código já declara a migração para UI de verdade como tarefa própria futura; esta
  mudança prioriza destravar o playtest agora.
- **Assets gratuitos dependem de busca na internet durante a implementação e de
  importação manual** → mitigado por campo opcional com fallback (cor chapada /
  sem partícula) enquanto o asset não for atribuído, então nada trava build ou teste
  por falta de arte.
- **Partícula por código tem controle visual grosseiro** → aceito como trade-off desta
  leva; fica como candidato a polimento futuro se o resultado incomodar no playtest.
- **Mudar o clique em célula comprável é comportamento visível** → já marcado
  **BREAKING** na proposta; mitigado por ficar resolvido no mesmo popup que já abre
  para célula possuída, então não é uma tela nova para aprender.
