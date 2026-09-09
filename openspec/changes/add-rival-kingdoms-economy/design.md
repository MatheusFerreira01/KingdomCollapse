## Context

A fatia vertical anterior está arquivada e jogável: núcleo puro com 197 testes, run completa, clima, cartas, eventos e simulador de balanceamento. Ver `proposal.md - Why` para o que ela expôs. Restrições que continuam valendo:

- **Dev solo**, e o objetivo do projeto mudou: deixou de ser "jogo pequeno para gerar renda rápido" e passou a ser **o jogo completo, bem feito**. O critério para decidir escopo é qualidade, não prazo. Ver `docs/roadmap.md`.
- **Fatiar continua valendo.** "Completo" é o destino, não o tamanho de cada entrega: uma mudança de 110 tarefas não é mais ambiciosa que duas de 55, é só menos revisável.
- **Núcleo em C# puro** (`KingdomCollapse.Core`, sem `UnityEngine`), imposto pelo asmdef. É o que permite simular milhares de runs.
- **Conteúdo em ScriptableObjects**, autorado no Inspector, gerado pelo `ContentSeeder`.
- **Trabalho de editor é do Matheus**; o código C# é gerado aqui.

O que muda o desenho agora é que o jogo passou a ter duas camadas que não existiam: uma economia com recursos que se limitam mutuamente, e uma campanha com adversários nomeados e fim.

## Goals / Non-Goals

**Goals:**
- Substituir o recurso único por uma economia onde a população seja simultaneamente motor e custo, para que o teto de expansão nasça da economia.
- Substituir a ameaça anônima e infinita por rivais com identidade e campanha finita, para que a run tenha linha de chegada e contra-jogo.
- Dar ao jogador motivo para voltar: vitória, escada de dificuldade e árvore de meta com poder permanente.
- Manter o simulador como instrumento confiável: ele precisa raciocinar sobre cinco recursos e sobre a campanha, ou volta a medir o jogo errado.

**Non-Goals:**
- Contra-ataque, invasão ou qualquer iniciativa militar do jogador sobre o rival.
- Raças com sistemas próprios (ver D11): esta mudança mantém a raça única.
- Arte final, Steamworks, localização.
- Animação de personagem: o retorno visual é feito de números flutuantes, tween e destaque, não de rig.
- Cadeias de produção com transformação (madeira vira tábua vira móvel). Recurso é produzido e consumido, sem intermediários.

## Decisions

### D1 — População como recurso duplo, e não como mais um número
População é gasta como capacidade de trabalho e consome comida por existir. Cada edifício declara quantos trabalhadores exige; sem eles, não produz nem defende.

*Por quê:* é o que faz a decisão existir. Com ouro único, a única pergunta era "tenho ouro?". Com população, toda escolha compete com outra: guarnecer a torre tira gente do campo, e o campo é o que alimenta quem guarnece. O jogo passa a ter um trade-off interno que não depende de o inimigo estar chegando.

*Consequência que resolve dívida:* o `threat_per_tile` de 2,5 que calibramos era uma penalidade artificial por expandir, inventada porque nada no jogo impedia crescer para sempre. Com população, o limite é orgânico e essa penalidade sai — a spec de `threat-clock` passa a proibi-la explicitamente.

*Alternativa:* manter defesa como número solto e só adicionar madeira e pedra. Adicionaria contabilidade sem adicionar decisão, porque nada competiria pelo mesmo recurso.

### D2 — Recursos como vetor tipado, não como campos soltos no estado da run
Um `ResourcePool` indexado por um enum `ResourceKind`, com operações de crédito, débito e verificação de disponibilidade que reportam qual recurso faltou.

*Por quê:* todo efeito, carta, evento, clima e edifício vai precisar falar de recurso. Cinco campos soltos em `RunState` obrigariam cada um deles a um `switch`, e adicionar um sexto recurso depois viraria uma varredura pelo código inteiro. Também é o que permite a mensagem "faltou madeira" ser gerada num lugar só.

*Alternativa:* propriedades separadas (`Gold`, `Wood`, ...). Mais legível numa primeira leitura, pior em toda extensão futura.

### D3 — Rival como dado, campanha como fila derivada
Um `RivalDefinition` traz nome, identidade, número de ataques, curva de força da campanha e o eixo que a identidade pressiona. O relógio de ameaça continua sendo a fila visível; passa a ser preenchido pela campanha do rival vigente em vez de por uma curva global.

*Por quê:* preserva tudo que já funciona e foi testado na telegrafia — antecedência mínima, força prevista, alteração pelo clima — e troca só a origem dos ataques. Também deixa o conteúdo de rival autorável no Inspector, como o resto.

*Trade-off:* a força deixa de ser uma função pura de `(dia, células)` e passa a depender do estado da campanha, o que torna o balanceamento menos analítico e mais dependente de simulação. Aceitável: o simulador já existe e é rápido.

### D4 — Identidade do rival escolhe o eixo do dano, não só o número
Cada identidade declara onde o excedente de um ataque bem-sucedido cai: território, integridade, população ou um recurso específico.

*Por quê:* é o que cria contra-jogo. Se todo rival ferisse a base, a resposta ótima seria sempre "mais defesa", e a economia de cinco recursos viraria contabilidade decorativa. Um rival que rouba comida precisa ser respondido com estoque e redundância, e é isso que faz builds diferentes existirem.

*Consequência:* a spec exige que ao menos um rival não seja resolvido por defesa. Sem essa exigência, o desenho degenera de volta para o jogo de hoje.

### D5 — Vitória como desfecho de primeira classe, não como "sobreviveu o bastante"
`RunOutcome` passa a ser `Victory` ou `Collapse`, e a pontuação considera o desfecho, os rivais derrotados e o nível.

*Por quê:* a ideia original dizia que o reino sempre cai, e o playtest mostrou que sem um momento de triunfo não há razão para voltar. Manter o Colapso como derrota preserva a fantasia; adicionar a Vitória dá o fecho que a progressão precisa para significar algo.

*Registro:* esta é uma reversão consciente de uma decisão anterior, pedida pelo usuário depois de jogar.

### D6 — Buffs permanentes só existem porque a escada existe
A árvore de meta passa a conceder poder numérico permanente, e cada nível de dificuldade declara um teto de poder acumulado que a validação de catálogo verifica.

*Por quê:* a proibição anterior existia para evitar grind-para-vencer, e a objeção era correta enquanto não havia escada. Com níveis que endurecem a cada vitória, o poder acumulado é absorvido pelo degrau seguinte — é o modelo do Hades e do Slay the Spire. O teto por nível é o que impede a absorção de virar promessa vazia.

*Risco:* se a escada parar de ser calibrada junto com a árvore, o jogo vira trivial. Por isso o teto é verificado por teste, e não confiado ao cuidado de quem autora.

### D7 — Recalibrar do zero, com o alvo medido em campanha
As curvas atuais foram medidas sobre a economia de ouro único e não sobrevivem. A medição recomeça, e o alvo deixa de ser "dia mediano de Colapso" e passa a ser "fração de runs que vencem a campanha do nível 1" e a duração dessa campanha.

*Por quê:* medir dias sobrevividos num jogo com vitória mede a coisa errada — uma run que vence no dia 26 e outra que colapsa no dia 26 não são o mesmo resultado.

### D8 — O bot precisa aprender a economia antes de a medição valer
A `GreedyPolicy` atual só sabe raciocinar sobre ouro e defesa. Ela precisa passar a alocar trabalhadores, vigiar o saldo de comida e reagir à identidade do rival vigente.

*Por quê:* já aconteceu duas vezes nesta sessão de o bot medir o jogo errado — ele ignorava o relógio de ameaça e desperdiçava carta de defesa em dia calmo, e as duas vezes fizeram a curva parecer mais letal do que era. Com uma economia mais rica, um bot ingênuo erraria mais, não menos.

*Consequência de processo:* nenhuma calibração desta mudança vale antes de o bot saber jogar a economia nova.

### D9 — Retorno visual é requisito desta mudança, não polimento posterior
A camada de feedback entra junto com a economia, e não depois dela.

*Por quê:* a build atual não consegue responder à única pergunta que importa — "isto é divertido?" — porque não tem retorno nenhum. O clímax de cada quatro dias, que é o ataque, é uma linha de texto num log cinza. Continuar adiando apresentação significa continuar projetando às cegas, e cada decisão de design tomada assim é chute caro.

*O que isso não é:* não é arte. É retorno de informação — número saltando sobre a célula que produziu, resolução encenada na ordem em que o núcleo resolve, ataque com destaque. Nada disso depende de asset final.

*Trade-off:* encenação irrita na repetição, então acelerar e pular são requisitos, não conveniências.

### D10 — Encontros deixam algo para trás; eventos ajustam números
Eventos de fim de dia continuam sendo o tempero leve. Encontros são uma categoria própria: têm identidade, escolha assimétrica, e podem deixar uma presença que age pela run inteira.

*Por quê:* variedade não vem de mais números diferentes. Nossos eventos atuais — imposto, praga, caravana — são planilha com texto, e são esquecidos no dia seguinte. O que faz uma run ser lembrada é o dragão que se instalou na fronteira e cobra tributo, porque ele muda o que a run é sobre. A permanência é a diferença entre tempero e história.

*Consequência:* a presença persistente é estado novo na run, com ação periódica anunciada e custo de resolução conhecido. Sem "anunciada" e "conhecido", vira azar disfarçado de conteúdo.

### D11 — Raças com sistema próprio ficam para a mudança seguinte
As raças continuam sendo modificadores nesta mudança. O desenho de raças-como-verbo está registrado e adiado.

*Por quê:* o diagnóstico está certo — modificador numérico não cria apego, verbo cria, e Orcs que se alimentam do saque seriam outro jogo em vez de outro multiplicador. Mas é o item mais caro dos três levantados (exige pool de cartas por raça) e o que menos ajuda a responder se o loop base funciona. Fazer antes do primeiro playtest com retorno visual seria construir quatro variações de um jogo que ainda não sabemos se é bom.

*Risco de adiar:* a economia de cinco recursos precisa ser projetada com espaço para essas raças, ou elas ficam impossíveis depois. Concretamente: produção por terreno, guarnição e saque precisam ser pontos de extensão, não regras fixas no meio do cálculo. A tarefa 1.11 transforma isso num teste que falha se a porta dos Orcs for fechada.

*Registro:* com a decisão de fazer o jogo completo, isto deixou de ser um corte e virou dependência. `add-race-identities` está comprometida no roteiro.

## Risks / Trade-offs

- **Escopo: isto é quase uma reescrita da economia** → mitigado por fazer em ordem, com o jogo compilando e testado a cada etapa: recursos, depois rivais, depois escada. Cada etapa é jogável antes da seguinte começar.
- **Cinco recursos podem virar contabilidade chata** → a defesa contra isso é a competição por população; se no playtest as decisões parecerem mecânicas, o corte é reduzir para quatro recursos, e não adicionar mais.
- **Conteúdo existente precisa ser reautorado** → o `ContentSeeder` cobre a maior parte; o custo real é decidir números, não digitar.
- **Balanceamento recomeça do zero** → aceito e planejado; o simulador torna isso barato, desde que o bot seja consertado primeiro (D8).
- **Reversão da regra de poder permanente pode reintroduzir grind-para-vencer** → mitigado pelo teto por nível verificado em teste (D6); se o teste for enfraquecido, o risco volta inteiro.
- **A UI atual é andaime IMGUI e vai ficar pequena para cinco recursos e campanha** → os painéis continuam em andaime; o que sai do andaime é o tabuleiro, onde o retorno visual precisa acontecer (D9).
- **Encenação vira irritação na repetição** → acelerar e pular são requisitos da spec, e a preferência persiste entre dias.
- **Adiar as raças pode torná-las inviáveis depois** → mitigado exigindo que produção, guarnição e saque sejam pontos de extensão desde já (D11); se essa disciplina falhar, a mudança seguinte vira reescrita.

## Migration Plan

Não há usuários nem saves publicados. O save de meta existente carrega número de versão desde o primeiro dia: perfis antigos são tratados como incompatíveis e recomeçam, o que é aceitável antes do lançamento.

## Open Questions

- Quantos rivais no nível 1? A intuição é 3, mas a resposta sai da simulação depois que o bot souber jogar.
- A capacidade de população vem de um edifício dedicado ou de todos os edifícios? Decidir depois de ver se o jogador sente falta de um "alojamento" como decisão própria.
- Nome final do jogo continua aberto (herdado da mudança anterior, registrado em `docs/steam.md`).
