> Tarefas marcadas **[Editor]** são trabalho do Matheus dentro do Unity. As demais são código C# gerado aqui.
>
> A ordem importa: o jogo precisa compilar e passar nos testes ao fim de cada grupo, e **nenhuma calibração vale antes do grupo 7** (o bot precisa saber jogar a economia nova, ou mede o jogo errado — já erramos assim duas vezes).

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
- [ ] 1.11 Deixar produção por terreno, guarnição e origem de recurso como pontos de extensão consultáveis por modificador de raça, verificando por teste que uma raça fictícia que tira comida de ataques repelidos funciona sem alterar o cálculo (design D11 — é o que mantém as raças viáveis depois)

## 2. Efeitos e conteúdo sobre recursos

- [ ] 2.1 Generalizar os efeitos de ouro para qualquer recurso (`GainResource`, `LoseResource`), mantendo os demais efeitos, verificando por teste que cada recurso é creditado e debitado isoladamente
- [ ] 2.2 Implementar efeitos de população — recrutar, realocar entre produção e guarnição — verificando por teste que realocar sobe a defesa e derruba a produção prevista (spec `card-system`)
- [ ] 2.3 Atualizar o orçamento de severidade de eventos para cobrir população e cada recurso, verificando por teste que um evento não zera a população e respeita o teto da classe (spec `day-events`)
- [ ] 2.4 Atualizar o clima para agir por recurso e poder pressionar a comida, verificando por teste que chuva aumenta a comida do rio e que o efeito aparece na previsão (spec `weather`)
- [ ] 2.5 Atualizar `EffectEntry` e os assets de conteúdo para os efeitos novos, verificando que o menu Create expõe os campos de recurso

## 3. Retorno visual

> Vem antes dos rivais de propósito: sem retorno, o playtest não consegue dizer se
> a economia nova é divertida, e todo design daqui pra frente vira chute (design D9).

- [ ] 3.1 Implementar números flutuantes sobre a célula de origem para ganho e perda de recurso, verificando em jogo que a produção do dia aparece sobre cada célula que produziu antes de o contador subir (spec `game-feel`)
- [ ] 3.2 Implementar a encenação ordenada da resolução do dia — produção, consumo, ameaça, evento — com pausa legível, verificando por teste que a mesma semente produz estado final idêntico com e sem encenação
- [ ] 3.3 Implementar acelerar e pular a encenação com preferência persistente, verificando em jogo que pular preserva o resultado consultável
- [ ] 3.4 Encenar o ataque como cena principal: força que chega, defesa que responde, desfecho legível sem texto, verificando em jogo que repelir e quebrar são distinguíveis sem ler números
- [ ] 3.5 Sinalizar no tabuleiro célula ociosa por falta de gente, célula arrasada e célula produtiva, verificando em jogo que o estado é identificável sem selecionar
- [ ] 3.6 Destacar o recurso que falta quando uma ação é recusada, verificando em jogo que a recusa aponta para o contador do recurso em falta
- [ ] 3.7 Apresentar derrota de rival, Vitória e desbloqueio como momentos próprios, verificando em jogo que cada um interrompe o fluxo normal do dia

## 4. Reinos rivais

- [ ] 4.1 Implementar `RivalDefinition` (nome, identidade, número de ataques, curva da campanha, eixo pressionado) e `RivalIdentity`, verificando por teste que toda identidade do catálogo declara o eixo que pressiona
- [ ] 4.2 Implementar a campanha do rival preenchendo o relógio de ameaça, verificando por teste que todo ataque continua sendo anunciado com a antecedência mínima e que o relógio indica de qual rival vem (spec `rival-kingdoms`)
- [ ] 4.3 Implementar a contagem de ataques repelidos e a derrota do rival, verificando por teste que repelir todos derrota, que falhar não perde a campanha, e que derrotar limpa os ataques restantes daquela campanha
- [ ] 4.4 Implementar a sucessão de rivais com intervalo de respiro e campanha mais forte, verificando por teste que o próximo rival é anunciado e que existe ao menos um dia sem ataque entre campanhas
- [ ] 4.5 Implementar a resolução de ataque por eixo de identidade, verificando por teste que um rival econômico causa perda mesmo com defesa alta e que um rival de assalto é anulado por defesa suficiente (design D4)
- [ ] 4.6 Implementar Vitória como desfecho da run, com placar próprio, verificando por teste que derrotar o último rival encerra imediatamente e que Vitória rende mais moeda que Colapso (spec `run-loop`)
- [ ] 4.7 Adicionar verificação automatizada do catálogo de rivais que falha se nenhum rival pressionar um eixo diferente de defesa

## 5. Escada de dificuldade

- [ ] 5.1 Implementar `DifficultyLevel` (rivais do nível, endurecimentos declarados, teto de poder da meta) e a seleção de nível, verificando por teste que um perfil novo só tem o primeiro nível
- [ ] 5.2 Implementar o desbloqueio por Vitória, persistido no perfil, verificando por teste que colapsar não desbloqueia e que o desbloqueio sobrevive a serializar e desserializar
- [ ] 5.3 Verificar por teste que os níveis são monotonicamente mais duros em ao menos um eixo e mais fáceis em nenhum
- [ ] 5.4 Exibir o que cada nível endurece na seleção, verificando que o texto vem do dado do nível e não de código

## 6. Meta com poder permanente

- [ ] 6.1 Estender `MetaNodeDefinition` para conceder bônus numérico permanente ao estado inicial, verificando por teste que a run seguinte começa com o valor aumentado
- [ ] 6.2 Substituir a validação que proíbe poder por um teto de poder acumulado por nível, verificando por teste que uma árvore dentro do teto passa e uma acima é reprovada apontando o nível e o excedente (spec `difficulty-ladder`, design D6)
- [ ] 6.3 [Editor] Criar a árvore inicial — tronco geral com desbloqueios e buffs, mais o galho dos Humanos — verificando em jogo que a compra afeta a run seguinte e que a validação passa

## 7. Simulador e bot

- [ ] 7.1 Ensinar a `GreedyPolicy` a alocar trabalhadores entre produção e guarnição conforme a ameaça iminente, verificando por teste que ela guarnece antes de um ataque e devolve gente à produção depois
- [ ] 7.2 Ensinar o bot a vigiar o saldo de comida e priorizar produção de comida antes de passar fome, verificando por teste que ele não deixa a população cair por descuido em condições folgadas
- [ ] 7.3 Ensinar o bot a reagir à identidade do rival vigente, verificando por teste que contra um rival econômico ele estoca em vez de erguer torre (design D8)
- [ ] 7.4 Estender o diagnóstico estrutural para os recursos novos — recurso parado, população ociosa, edifício sem gente, comida no limite — verificando que cada aviso aparece quando a condição existe e some quando ela deixa de existir
- [ ] 7.5 Trocar a métrica alvo do relatório de "dia mediano de Colapso" para "fração de runs que vencem a campanha" e duração da campanha vencida, verificando pelo relatório (design D7)

## 8. Encontros

- [ ] 8.1 Implementar `EncounterDefinition` com identidade, opções assimétricas e possibilidade de deixar presença, verificando por teste que todo encontro do catálogo declara nome e descrição (spec `encounters`)
- [ ] 8.2 Implementar presença persistente com ação periódica anunciada e custo de resolução conhecido, verificando por teste que a presença age no intervalo declarado e some ao ser resolvida
- [ ] 8.3 Implementar encadeamento: um encontro agenda outro para um dia futuro, referenciando a escolha anterior, verificando por teste que a continuação acontece mesmo com outros encontros no intervalo
- [ ] 8.4 Aplicar o orçamento de severidade a encontros e presenças, verificando por teste que uma presença hostil nunca encerra a run e sempre tem como ser encerrada
- [ ] 8.5 Garantir que encontros não tocam a campanha do rival, verificando por teste que nenhum encontro cria, adianta ou cancela ataque
- [ ] 8.6 Adicionar verificação de catálogo que reprova encontro cuja escolha tenha opção dominante em toda situação
- [ ] 8.7 [Editor] Criar 6 encontros iniciais — minotauro ferido, dragão que se instala, herói errante, ruína amaldiçoada, caravana perdida, eremita — sendo ao menos 2 com presença e 1 com cadeia

## 9. Conteúdo

- [ ] 9.1 Atualizar o `ContentSeeder` para gerar edifícios com custo, produção e trabalhadores nos recursos novos, verificando que o catálogo gerado passa na validação
- [ ] 9.2 Atualizar as cartas para custar e conceder recursos, incluindo cartas de população, verificando em jogo que cada uma resolve e é recusada corretamente
- [ ] 9.3 Atualizar os eventos e ofertas para operar sobre recursos, verificando que as ofertas continuam tendo saída sem custo
- [ ] 9.4 [Editor] Criar os 3 rivais do nível 1 — um de assalto, um de cerco pesado, um econômico — verificando em jogo que cada um exige uma resposta diferente
- [ ] 9.5 [Editor] Criar os 3 primeiros níveis de dificuldade com seus endurecimentos declarados, verificando em jogo que vencer o nível 1 desbloqueia o 2

## 10. Painéis e telas

- [ ] 10.1 Exibir os cinco recursos com produção e consumo previstos do dia, verificando em jogo que o saldo previsto bate com o que acontece ao encerrar o dia
- [ ] 10.2 Exibir população total, alocada e ociosa, e permitir realocar, verificando em jogo que realocar muda defesa e produção na hora
- [ ] 10.3 Exibir o rival vigente, sua identidade e o progresso da campanha, verificando em jogo que repelir um ataque avança o progresso visivelmente
- [ ] 10.4 Implementar as telas de Vitória e de seleção de dificuldade, verificando em jogo que vencer desbloqueia o nível seguinte
- [ ] 10.5 Exibir clima de hoje e previsão de amanhã, com o impacto na comida, verificando em jogo que a previsão bate com o dia seguinte

## 11. Balanceamento

- [ ] 11.1 Espelhar o conteúdo novo no harness de linha de comando, verificando que a mediana medida localmente bate com a do simulador no Editor
- [ ] 11.2 Calibrar economia e campanhas até que a fração de vitórias no nível 1 fique entre 40% e 60% para o bot, verificando pelo relatório do simulador
- [ ] 11.3 Verificar por simulação que os três rivais exigem respostas diferentes: um bot que só ergue torres deve perder para o rival econômico
- [ ] 11.4 Jogar 5 runs completas e registrar em `docs/playtest.md` duração real, momentos de tédio e decisões que pareceram falsas
- [ ] 11.5 Corrigir o que o playtest apontar e confirmar por uma segunda rodada registrada no mesmo documento
- [ ] 11.6 Verificar que a suíte completa passa e que o jogo roda uma campanha inteira, do dia 1 à Vitória, sem erro no Console
