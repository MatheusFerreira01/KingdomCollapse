using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Um degrau da escada de dificuldade (spec difficulty-ladder). Declara os rivais
    /// da run, quando cada um entra, o que o nivel endurece e o teto de poder que a
    /// arvore de meta pode conceder ate aqui (design D6).
    /// </summary>
    public sealed class DifficultyLevel
    {
        public DifficultyLevel(
            string id,
            int rank,
            string displayName,
            string hardening,
            IReadOnlyList<RivalDefinition> rivals,
            IReadOnlyList<int> entryDays = null,
            double startingResourceFactor = 1.0,
            int metaPowerCap = 0)
        {
            Id = id;
            Rank = Math.Max(1, rank);
            DisplayName = displayName;
            Hardening = hardening ?? string.Empty;
            Rivals = rivals ?? new List<RivalDefinition>();
            EntryDays = entryDays;
            StartingResourceFactor = Math.Max(0, startingResourceFactor);
            MetaPowerCap = Math.Max(0, metaPowerCap);
        }

        public string Id { get; }

        /// <summary>Posicao na escada, a partir de 1.</summary>
        public int Rank { get; }

        public string DisplayName { get; }

        /// <summary>
        /// O que este nivel endurece em relacao ao anterior, escrito por quem autora.
        /// A tela de selecao mostra este texto, e nao um resumo gerado em codigo: o
        /// jogador precisa ler a intencao, nao uma lista de numeros.
        /// </summary>
        public string Hardening { get; }

        /// <summary>Rivais do nivel, na ordem em que entram em guerra.</summary>
        public IReadOnlyList<RivalDefinition> Rivals { get; }

        /// <summary>Dia de entrada de cada rival. Nulo usa o espacamento padrao do relogio.</summary>
        public IReadOnlyList<int> EntryDays { get; }

        /// <summary>
        /// Multiplica o saldo inicial de todo recurso menos populacao. 1 = normal,
        /// 0,8 = comeca com 20% a menos. Populacao fica de fora porque e quem opera
        /// o reino: cortar gente no dia 1 trava o comeco em vez de aperta-lo.
        /// </summary>
        public double StartingResourceFactor { get; }

        /// <summary>Poder acumulado que a arvore de meta pode conceder ate este nivel.</summary>
        public int MetaPowerCap { get; }

        /// <summary>Dia em que o rival da posicao dada entra, com o padrao do relogio quando nao declarado.</summary>
        public int EntryDayAt(int index)
        {
            if (index <= 0)
            {
                return 1;
            }

            return EntryDays != null && index < EntryDays.Count
                ? Math.Max(1, EntryDays[index])
                : index * ThreatClock.DefaultEntrySpacingDays;
        }

        /// <summary>
        /// Forca total que o jogador precisa segurar para vencer sem falhar nenhum
        /// ataque: a soma de toda campanha do nivel. E a medida de quanto as
        /// campanhas exigem, usada para comparar niveis.
        /// </summary>
        public int CampaignDemand()
        {
            int total = 0;
            for (int r = 0; r < Rivals.Count; r++)
            {
                RivalDefinition rival = Rivals[r];
                if (rival?.Campaign == null)
                {
                    continue;
                }

                for (int i = 0; i < rival.AttacksToDefeat; i++)
                {
                    total += rival.Campaign.ForceFor(i);
                }
            }

            return total;
        }

        public override string ToString() => Id + " (nivel " + Rank + ")";
    }

    /// <summary>
    /// A escada inteira, em ordem. Sabe o que um perfil pode jogar; nao sabe como a
    /// run e montada, isso fica no RunBuilder.
    /// </summary>
    public sealed class DifficultyLadder
    {
        private readonly List<DifficultyLevel> _levels;

        public DifficultyLadder(IEnumerable<DifficultyLevel> levels)
        {
            _levels = new List<DifficultyLevel>(levels ?? new DifficultyLevel[0]);
            _levels.Sort((a, b) => a.Rank.CompareTo(b.Rank));
        }

        public IReadOnlyList<DifficultyLevel> Levels => _levels;

        public DifficultyLevel LevelOfRank(int rank)
        {
            for (int i = 0; i < _levels.Count; i++)
            {
                if (_levels[i].Rank == rank)
                {
                    return _levels[i];
                }
            }

            return null;
        }

        /// <summary>Niveis que o perfil pode escolher: do primeiro ate o mais alto que ja liberou.</summary>
        public List<DifficultyLevel> Available(MetaProfile profile)
        {
            int unlocked = profile != null ? profile.HighestUnlockedLevel : 1;
            List<DifficultyLevel> available = new List<DifficultyLevel>();
            for (int i = 0; i < _levels.Count; i++)
            {
                if (_levels[i].Rank <= unlocked)
                {
                    available.Add(_levels[i]);
                }
            }

            return available;
        }

        public bool CanStart(MetaProfile profile, int rank)
        {
            return LevelOfRank(rank) != null && rank <= (profile != null ? profile.HighestUnlockedLevel : 1);
        }

        /// <summary>
        /// O que a tela de selecao mostra de cada nivel: nome, se esta liberado e o
        /// texto de endurecimento, tirado do dado do nivel.
        /// </summary>
        public List<LevelChoice> Choices(MetaProfile profile)
        {
            List<LevelChoice> choices = new List<LevelChoice>();
            for (int i = 0; i < _levels.Count; i++)
            {
                choices.Add(new LevelChoice(_levels[i], CanStart(profile, _levels[i].Rank)));
            }

            return choices;
        }
    }

    /// <summary>Uma linha da tela de selecao de nivel.</summary>
    public sealed class LevelChoice
    {
        public LevelChoice(DifficultyLevel level, bool unlocked)
        {
            Level = level;
            Unlocked = unlocked;
        }

        public DifficultyLevel Level { get; }

        public bool Unlocked { get; }

        public string Title => Level.DisplayName;

        /// <summary>O que o nivel endurece, exatamente como autorado.</summary>
        public string Hardening => Level.Hardening;
    }
}
