---
name: kingdom-collapse-projeto
description: "KingdomCollapse — roguelite de gestão de reino, repo próprio; escopo virou \"jogo completo bem feito\" em 2026-09-09, MM pausado"
metadata: 
  node_type: memory
  type: project
  originSessionId: d8855839-f456-41fb-a670-4b3c9f467a6b
  modified: 2026-09-09T19:19:49.533Z
---

Em 2026-09-09 o Matheus pausou o projeto MM (ver [[meu-jogo-onde-paramos]]) e iniciou **KingdomCollapse** (nome provisório): roguelite + estratégia de gestão de reino. Fica em `D:\Projetos\Meu Jogo\KingdomCollapse\`, com repositório git próprio, ignorado pelo `.gitignore` do repo MM.

**Mudança de rumo, mesma data:** começou como "jogo pequeno para aprender monetização na Steam e gerar renda rápido". Depois de jogar a fatia vertical, o Matheus decidiu **fazer o jogo completo, bem feito, mesmo demorando mais**. O critério de decisão deixou de ser "isso atrasa o lançamento?" e passou a ser "isso deixa o jogo melhor?". A meta de renda no curto prazo saiu; aprender o pipeline da Steam continua, e pode ser feito cedo subindo a página enquanto desenvolve.

Decisões travadas (não re-perguntar):
- Unity, 3D baixo-poli, câmera ortográfica isométrica fixa
- Cinco recursos: ouro, madeira, pedra, comida, população — população come e guarnece, e é o teto natural de expansão
- Reinos rivais nomeados com campanha finita; repelir todos os ataques derrota o rival; derrotar todos vence a run
- Escada de dificuldade destravada por vitória; é ela que autoriza buffs permanentes na árvore de meta
- Raças como **verbo**, não como modificador numérico (Orcs vivem de saque, Elfos não constroem em floresta)
- Retorno visual é requisito, não polimento: sem ele não dá para saber se o jogo é divertido

Roteiro em `KingdomCollapse/docs/roadmap.md`. Planejamento em `openspec/`. Divisão de trabalho segue [[unity-editor-work-is-the-users]].

**Why:** o projeto mudou de natureza depois do primeiro playtest, e tratá-lo como "o jogo pequeno" levaria a recomendar cortes que ele já recusou.
**How to apply:** ao propor escopo, otimizar por qualidade e não por prazo; ainda assim fatiar em mudanças pequenas e revisáveis, porque "completo" é o destino, não o tamanho de cada passo.
