using System.Runtime.CompilerServices;

// Os testes precisam montar estados que o jogo nunca produz por comando: um dia 30
// sem jogar 29 dias, um placar com marcos ja alcancados. Abrir o internal so para a
// suite mantem esses setters fechados para a camada Game, que deve mudar o estado
// da run apenas pelo RunEngine.
[assembly: InternalsVisibleTo("KingdomCollapse.Tests.EditMode")]
