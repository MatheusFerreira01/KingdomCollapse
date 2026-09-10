---
name: kingdom-collapse-onde-paramos
description: "Estado do KingdomCollapse em 2026-09-10 — grupos 4 e 5 feitos, layout IMGUI refeito, bug das cartas de terreno pendente"
metadata: 
  node_type: memory
  type: project
  originSessionId: 2ef28d58-5bde-4272-9d30-8a2bf542f51b
  modified: 2026-09-10T16:12:20.768Z
---

Pausa em 2026-09-10 (Matheus parou para recuperar tokens). Mudança ativa: `add-rival-kingdoms-economy`.

**Feito nesta sessão (commitado):**
- Grupo 4 completo, já no modelo de **entrada escalonada** (design D12): rivais entram em dias declarados e se somam; cada janela sorteia o atacante; janelas 4/3/2 dias por nº de rivais; 1 ataque/dia no máximo. O modelo sequencial foi implementado e revertido a pedido do Matheus.
- Rival econômico: defesa decide se vence (repelir conta), estoque decide se aguenta (saque sempre) — decisão dele, registrada no design D4.
- Grupo 5: 5.1–5.3 feitos; 5.4 aberta (texto vem do dado, falta a tela de seleção da 10.4).
- 3.7 (momentos que param o dia) implementada; perfil passou a ser salvo pelo GameBootstrap.
- Clima do dia 1 nunca bloqueia ação (spec weather atualizada).
- Layout IMGUI refeito em regiões (topo, painel com rolagem, cartas com texto de regra gerado por `IEffect.Describe()`, log recolhível, escala 1600x900). Matheus aprovou: "o layout agora está muito bom".

**Próximo passo:** investigar o bug que ele achou — **cartas de terreno parecem não funcionar** (provável `SetTerrainEffect`/alvo; ver `BasicEffects.cs` e `TryPlayCard` no GameBootstrap). Depois: fechar 3.7 e 10.1/10.2/10.3/10.5 com verificação dele em jogo, e seguir para o grupo 6.

**Pendências do Matheus no Editor:** criar os 3 rivais (9.4; ele começou a pasta `Assets/Data/Rivais`) e depois os níveis (9.5).

**Ferramenta útil:** a camada Game compila fora do Unity com um csproj no scratchpad referenciando `D:/Program Files/Unity/Hub/Editor/6000.3.8f1/Editor/Data/Managed/UnityEngine/*.dll` + `Library/ScriptAssemblies/Unity.InputSystem.dll` + `tools/CoreLib` (net8.0). Pega erro antes de ele abrir o Unity.

**Why:** a sessão foi longa e a próxima começa sem o histórico da conversa.
**How to apply:** ao retomar, começar pelo bug das cartas de terreno, antes de qualquer grupo novo. Ver [[kingdom-collapse-projeto]] e [[unity-editor-work-is-the-users]].
