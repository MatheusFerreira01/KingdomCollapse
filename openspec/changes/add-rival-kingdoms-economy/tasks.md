> Tarefas marcadas **[Editor]** são trabalho do Matheus dentro do Unity. As demais são código C# gerado aqui.
>
> A ordem importa: o jogo precisa compilar e passar nos testes ao fim de cada grupo, e **nenhuma calibração vale antes do grupo 6** (o bot precisa saber jogar a economia nova, ou mede o jogo errado — já erramos assim duas vezes).

## 1. Economia de recursos

- [ ] 1.1 Implementar `ResourceKind` (ouro, madeira, pedra, comida, população) e `ResourcePool` com crédito, débito e verificação que reporta qual recurso faltou, verificando por teste que debitar além do saldo é recusado sem alterar nada
- [ ] 1.2 Migrar `RunState` do ouro único para o `ResourcePool`, verificando por teste que a run inicia com os valores declarados pela raça em cada recurso
- [ ] 1.3 Estender `BuildingDefinition` com custo por recurso, produção por recurso e trabalhadores exigidos, verificando por teste que construir debita todos os materiais e é recusado quando falta qualquer um (spec `kingdom-grid`)
- [ ] 1.4 Implementar produção por terreno e por recurso no `ProductionCalculator`, com detalhamento por recurso, verificando por teste que floresta rende madeira e mina rende pedra, e nenhuma rende o recurso da outra (spec `resources`)
- [ ] 1.5 Implementar consumo diário de comida proporcional à população, aplicado depois da produção, verificando por teste que o saldo do dia é discriminado e que dobrar a população dobra o consumo
- [ ] 1.6 Implementar escassez de comida com queda de população e aviso antecipado, verificando por teste que a fome não encerra a run e que o aviso aparece no Planejamento anterior
- [ ] 1.7 Implementar crescimento de população por excedente, limitado por capacidade dos edifícios, verificando por teste que o crescimento para ao atingir a capacidade mesmo com comida sobrando
- [ ] 1.8 Implementar alocação de trabalhadores com edifícios ociosos quando falta gente, verificando por teste que um edifício sem trabalhadores não produz e não é destruído
- [ ] 1.9 Implementar defesa por guarnição, verificando por teste que uma torre sem trabalhadores soma zero de defesa e que guarnecer reduz a produção (spec `resources`)
- [ ] 1.10 Remover o termo por célula da curva de ameaça e o remendo associado, verificando por teste que dois reinos de tamanhos diferentes enfrentam a mesma força no mesmo ataque (spec `threat-clock`)

## 2. Efeitos e conteúdo sobre recursos

- [ ] 2.1 Generalizar os efeitos de ouro para qualquer recurso (`GainResource`, `LoseResource`), mantendo os demais efeitos, verificando por teste que cada recurso é creditado e debitado isoladamente
- [ ] 2.2 Implementar efeitos de população — recrutar, realocar entre produção e guarnição — verificando por teste que realocar sobe a defesa e derruba a produção prevista (spec `card-system`)
- [ ] 2.3 Atualizar o orçamento de severidade de eventos para cobrir população e cada recurso, verificando por teste que um evento não zera a população e respeita o teto da classe (spec `day-events`)
- [ ] 2.4 Atualizar o clima para agir por recurso e poder pressionar a comida, verificando por teste que chuva aumenta a comida do rio e que o efeito aparece na previsão (spec `weather`)
- [ ] 2.5 Atualizar `EffectEntry` e os assets de conteúdo para os efeitos novos, verificando que o menu Create expõe os campos de recurso

## 3. Reinos rivais

- [ ] 3.1 Implementar `RivalDefinition` (nome, identidade, número de ataques, curva da campanha, eixo pressionado) e `RivalIdentity`, verificando por teste que toda identidade do catálogo declara o eixo que pressiona
- [ ] 3.2 Implementar a campanha do rival preenchendo o relógio de ameaça, verificando por teste que todo ataque continua sendo anunciado com a antecedência mínima e que o relógio indica de qual rival vem (spec `rival-kingdoms`)
- [ ] 3.3 Implementar a contagem de ataques repelidos e a derrota do rival, verificando por teste que repelir todos derrota, que falhar não perde a campanha, e que derrotar limpa os ataques restantes daquela campanha
- [ ] 3.4 Implementar a sucessão de rivais com intervalo de respiro e campanha mais forte, verificando por teste que o próximo rival é anunciado e que existe ao menos um dia sem ataque entre campanhas
- [ ] 3.5 Implementar a resolução de ataque por eixo de identidade, verificando por teste que um rival econômico causa perda mesmo com defesa alta e que um rival de assalto é anulado por defesa suficiente (design D4)
- [ ] 3.6 Implementar Vitória como desfecho da run, com placar próprio, verificando por teste que derrotar o último rival encerra imediatamente e que Vitória rende mais moeda que Colapso (spec `run-loop`)
- [ ] 3.7 Adicionar verificação automatizada do catálogo de rivais que falha se nenhum rival pressionar um eixo diferente de defesa

## 4. Escada de dificuldade

- [ ] 4.1 Implementar `DifficultyLevel` (rivais do nível, endurecimentos declarados, teto de poder da meta) e a seleção de nível, verificando por teste que um perfil novo só tem o primeiro nível
- [ ] 4.2 Implementar o desbloqueio por Vitória, persistido no perfil, verificando por teste que colapsar não desbloqueia e que o desbloqueio sobrevive a serializar e desserializar
- [ ] 4.3 Verificar por teste que os níveis são monotonicamente mais duros em ao menos um eixo e mais fáceis em nenhum
- [ ] 4.4 Exibir o que cada nível endurece na seleção, verificando que o texto vem do dado do nível e não de código

## 5. Meta com poder permanente

- [ ] 5.1 Estender `MetaNodeDefinition` para conceder bônus numérico permanente ao estado inicial, verificando por teste que a run seguinte começa com o valor aumentado
- [ ] 5.2 Substituir a validação que proíbe poder por um teto de poder acumulado por nível, verificando por teste que uma árvore dentro do teto passa e uma acima é reprovada apontando o nível e o excedente (spec `difficulty-ladder`, design D6)
- [ ] 5.3 [Editor] Criar a árvore inicial — tronco geral com desbloqueios e buffs, mais o galho dos Humanos — verificando em jogo que a compra afeta a run seguinte e que a validação passa

## 6. Simulador e bot

- [ ] 6.1 Ensinar a `GreedyPolicy` a alocar trabalhadores entre produção e guarnição conforme a ameaça iminente, verificando por teste que ela guarnece antes de um ataque e devolve gente à produção depois
- [ ] 6.2 Ensinar o bot a vigiar o saldo de comida e priorizar produção de comida antes de passar fome, verificando por teste que ele não deixa a população cair por descuido em condições folgadas
- [ ] 6.3 Ensinar o bot a reagir à identidade do rival vigente, verificando por teste que contra um rival econômico ele estoca em vez de erguer torre (design D8)
- [ ] 6.4 Estender o diagnóstico estrutural para os recursos novos — recurso parado, população ociosa, edifício sem gente, comida no limite — verificando que cada aviso aparece quando a condição existe e some quando ela deixa de existir
- [ ] 6.5 Trocar a métrica alvo do relatório de "dia mediano de Colapso" para "fração de runs que vencem a campanha" e duração da campanha vencida, verificando pelo relatório (design D7)

## 7. Conteúdo

- [ ] 7.1 Atualizar o `ContentSeeder` para gerar edifícios com custo, produção e trabalhadores nos recursos novos, verificando que o catálogo gerado passa na validação
- [ ] 7.2 Atualizar as cartas para custar e conceder recursos, incluindo cartas de população, verificando em jogo que cada uma resolve e é recusada corretamente
- [ ] 7.3 Atualizar os eventos e ofertas para operar sobre recursos, verificando que as ofertas continuam tendo saída sem custo
- [ ] 7.4 [Editor] Criar os 3 rivais do nível 1 — um de assalto, um de cerco pesado, um econômico — verificando em jogo que cada um exige uma resposta diferente
- [ ] 7.5 [Editor] Criar os 3 primeiros níveis de dificuldade com seus endurecimentos declarados, verificando em jogo que vencer o nível 1 desbloqueia o 2

## 8. Apresentação

- [ ] 8.1 Exibir os cinco recursos com produção e consumo previstos do dia, verificando em jogo que o saldo previsto bate com o que acontece ao encerrar o dia
- [ ] 8.2 Exibir população total, alocada e ociosa, e permitir realocar, verificando em jogo que realocar muda defesa e produção na hora
- [ ] 8.3 Exibir o rival vigente, sua identidade e o progresso da campanha, verificando em jogo que repelir um ataque avança o progresso visivelmente
- [ ] 8.4 Implementar as telas de Vitória e de seleção de dificuldade, verificando em jogo que vencer desbloqueia o nível seguinte
- [ ] 8.5 Exibir clima de hoje e previsão de amanhã, com o impacto na comida, verificando em jogo que a previsão bate com o dia seguinte

## 9. Balanceamento

- [ ] 9.1 Espelhar o conteúdo novo no harness de linha de comando, verificando que a mediana medida localmente bate com a do simulador no Editor
- [ ] 9.2 Calibrar economia e campanhas até que a fração de vitórias no nível 1 fique entre 40% e 60% para o bot, verificando pelo relatório do simulador
- [ ] 9.3 Verificar por simulação que os três rivais exigem respostas diferentes: um bot que só ergue torres deve perder para o rival econômico
- [ ] 9.4 Jogar 5 runs completas e registrar em `docs/playtest.md` duração real, momentos de tédio e decisões que pareceram falsas
- [ ] 9.5 Corrigir o que o playtest apontar e confirmar por uma segunda rodada registrada no mesmo documento
- [ ] 9.6 Verificar que a suíte completa passa e que o jogo roda uma campanha inteira, do dia 1 à Vitória, sem erro no Console
