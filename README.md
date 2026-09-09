# KingdomCollapse

Roguelite de estratégia: você administra um reino, compra território célula a célula,
joga cartas para agir dentro do dia, e enfrenta reinos rivais nomeados até derrotá-los
ou ver a base cair.

Nome provisório. Ver `docs/steam.md`.

> **Estado atual:** a economia de cinco recursos, o retorno visual e o ciclo de dia
> estão implementados e jogáveis. Os reinos rivais e a escada de dificuldade ainda
> não. Progresso em `openspec/changes/add-rival-kingdoms-economy/tasks.md`.

## Máquina nova — checklist

1. **Unity Hub** e o editor **6000.3.8f1** (mesma versão do ProjectMM)
2. **.NET SDK** (8 ou mais novo) — para o harness de testes fora do Unity
3. **OpenSpec CLI**, que não vem no repositório:
   ```
   npm install -g @fission-ai/openspec@1.11.0
   ```
4. Clonar os dois repositórios. O KingdomCollapse fica **dentro** da pasta do
   ProjectMM, mas é ignorado por ele — são repositórios independentes, e clonar um
   não traz o outro:
   ```
   mkdir "D:/Projetos/Meu Jogo"
   cd "D:/Projetos/Meu Jogo"
   git clone https://github.com/MatheusFerreira01/ProjectMM.git .
   git clone https://github.com/MatheusFerreira01/KingdomCollapse.git KingdomCollapse
   ```
5. Confirmar que veio tudo, **sem esperar o Unity importar**:
   ```
   cd KingdomCollapse/tools/CoreTests
   dotnet test
   ```
   Esperado: **276 testes verdes**, em poucos segundos.
6. Restaurar as memórias do Claude Code, se quiser o contexto completo. Ver
   `.claude-memory/README.md`.

A primeira abertura do projeto no Unity demora: `Library/` não é versionado, de
propósito, e o Editor reimporta tudo.

## Como o repositório é organizado

```
ProjectKC/                    projeto Unity
  Assets/Scripts/Core/        regras do jogo, sem dependência de engine
  Assets/Scripts/Game/        camada Unity: view, HUD, ScriptableObjects
  Assets/Scripts/Editor/      ferramentas: simulador, gerador de conteúdo
  Assets/Scripts/Tests/       testes de EditMode
  Assets/Data/                conteúdo autorado no Inspector
tools/CoreLib/                compila o Core fora do Unity
tools/CoreTests/              roda os mesmos testes fora do Unity
openspec/                     specs e planejamento
docs/                         roteiro, Steam, rascunhos de conteúdo
```

### O Core não conhece o Unity

`KingdomCollapse.Core` declara `noEngineReferences` no seu asmdef, então o Editor
**impede** um `using UnityEngine` lá dentro. Não é convenção, é regra de compilador.

É isso que permite resolver uma run inteira dentro de um teste e simular mil runs em
menos de um segundo — que é como o balanceamento é feito aqui, medindo em vez de
adivinhar.

### Os mesmos testes rodam em dois lugares

`tools/CoreTests` compila os arquivos de `Assets/Scripts/Tests` contra o Core e roda
fora do Unity. O harness espelha a fronteira de assembly do Editor de propósito: sem
isso ele aprovaria acesso a `internal` que o Unity recusa, e já aprovou uma vez.

## Ferramentas no Editor

Menu **Kingdom Collapse**:

| Item | O que faz |
|---|---|
| Simulador de Balanceamento | roda N runs com uma IA heurística e reporta a distribuição, mais um diagnóstico estrutural que aponta conteúdo faltando |
| Gerar conteudo inicial | cria cartas, climas e eventos como assets, e valida o catálogo |
| Organizar pasta Data | move os assets para as subpastas por tipo |

O diagnóstico do simulador existe porque medir "quantos dias o jogador sobrevive"
respondia a pergunta errada: ele acusa defesa constante, ouro parado e zero variação
entre sementes, que são sinais de conteúdo ausente e não de curva mal ajustada.

## Trabalhando no projeto

Planejamento e execução usam OpenSpec:

```
openspec status --change add-rival-kingdoms-economy    # onde paramos
openspec validate add-rival-kingdoms-economy --strict  # specs coerentes?
```

No Claude Code, `/opsx:apply` retoma a implementação de onde parou — o progresso vive
nos checkboxes de `tasks.md`, que são versionados.

**Divisão de trabalho:** o código C# é escrito pelo Claude; cenas, prefabs, assets e
preenchimento de ScriptableObjects são feitos por mim no Editor. As tarefas marcadas
`[Editor]` em `tasks.md` são as minhas.

## Onde estão as decisões

Não estão na cabeça de ninguém nem no histórico de uma conversa:

- `docs/roadmap.md` — a sequência de mudanças e o que corre em paralelo
- `openspec/changes/*/design.md` — cada decisão técnica com o porquê e a alternativa descartada
- `openspec/specs/` — o que o sistema precisa fazer, como contrato
- `docs/steam.md` — o pipeline comercial

Duas reversões conscientes estão registradas: a run passou a poder ser **vencida**, e
a árvore de meta voltou a conceder **poder permanente** — esta última apenas porque a
escada de dificuldade absorve o poder, com teto verificado por teste.

## Licença

Projeto pessoal, sem licença aberta. Arte externa em `Assets/Art/Externo` segue as
licenças dos respectivos pacotes.
