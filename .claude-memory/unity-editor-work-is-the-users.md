---
name: unity-editor-work-is-the-users
description: "Divisão de trabalho no projeto Meu Jogo — Claude escreve os scripts, Matheus faz tudo que exige a GUI do Unity"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 2ed8520f-527b-4e49-a7a7-5968debdaaf7
  modified: 2026-09-03T00:04:00.279Z
---

Neste projeto, Claude escreve os arquivos `.cs` e Matheus faz todo o trabalho que exige a interface do Unity: cenas, prefabs, assets de `ScriptableObject`, montagem de UI, bake de NavMesh e Build Profiles. Claude não tenta autorar `.unity`, `.prefab` nem `.asset` na mão.

**Why:** os `.meta` com GUID só existem depois que o Unity importa os scripts, então escrever esses arquivos YAML sem o editor produz referências quebradas. E Matheus já sabe operar o editor — descrever leva menos tempo do que gerar YAML frágil.

**How to apply:** ao chegar numa task de editor, pare a implementação, entregue um passo a passo numerado com o caminho exato de menu (`GameObject → 3D Object → Capsule`) e uma tabela de campo/valor do Inspector para cada componente, mais a lista do que deve acontecer no Play para validar. Ele pediu explicitamente instruções bem explicadas porque faz tempo que não mexe no Unity. Depois espere ele confirmar antes de seguir. Ver [[meu-jogo-fatia-vertical-estado]].
