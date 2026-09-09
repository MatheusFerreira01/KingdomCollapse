# Memórias do Claude Code

Estes arquivos **não são usados de dentro do repositório**. A memória do Claude Code
vive fora dele, na pasta do usuário. Eles estão aqui só para viajarem junto com o
projeto entre máquinas.

Só as memórias deste projeto foram copiadas. As do ProjectMM ficaram de fora, porque
o KingdomCollapse é repositório próprio.

`unity-editor-work-is-the-users.md` veio junto por ser referenciada pela memória
principal e por valer para os dois projetos: é a divisão de trabalho entre quem
escreve `.cs` e quem mexe no Editor.

## Restaurar numa máquina nova

Copie o conteúdo desta pasta para:

```
C:\Users\<seu-usuario>\.claude\projects\D--Projetos-Meu-Jogo\memory\
```

Se a pasta não existir, crie. E acrescente as linhas correspondentes ao `MEMORY.md`
que estiver lá, que é o índice carregado a cada sessão:

```
- [Projeto KingdomCollapse](kingdom-collapse-projeto.md) — segundo jogo, repo separado; virou "jogo completo bem feito" em 2026-09-09, MM em pausa
- [Trabalho de editor é do usuário](unity-editor-work-is-the-users.md) — Claude escreve .cs, Matheus faz cenas/prefabs/assets no Unity
```

## Se não restaurar

Não é bloqueante. As decisões de projeto foram escritas de propósito dentro do
repositório — `docs/roadmap.md` e `openspec/changes/*/design.md` carregam as 13
decisões com o motivo de cada uma. A memória adiciona contexto, não é a fonte.

O que não volta de jeito nenhum é o histórico da conversa: numa sessão nova o Claude
começa sem ele, e é por isso que o *porquê* de cada decisão está versionado.
