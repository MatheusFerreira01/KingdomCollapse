using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Progresso que sobrevive entre runs. Carrega um numero de versao desde o
    /// primeiro dia: depois do lancamento nao havera como migrar um save que nao
    /// diga qual formato usa.
    /// </summary>
    public sealed class MetaProfile
    {
        /// <summary>
        /// 2 acrescentou o nivel de dificuldade liberado. Um save da versao 1 ainda e
        /// lido, e comeca no nivel 1, que e o que ele teria de qualquer forma.
        /// </summary>
        public const int CurrentVersion = 2;

        private readonly HashSet<string> _purchasedNodes = new HashSet<string>();

        public MetaProfile(int version = CurrentVersion)
        {
            Version = version;
        }

        public int Version { get; internal set; }

        public int Currency { get; private set; }

        public int RunsPlayed { get; internal set; }

        public int BestDayReached { get; internal set; }

        public int BestScore { get; internal set; }

        /// <summary>Nivel de dificuldade mais alto que o perfil ja pode jogar. Comeca em 1.</summary>
        public int HighestUnlockedLevel { get; internal set; } = 1;

        public IReadOnlyCollection<string> PurchasedNodes => _purchasedNodes;

        public bool HasNode(string nodeId) => _purchasedNodes.Contains(nodeId);

        public void AddCurrency(int amount)
        {
            if (amount > 0)
            {
                Currency += amount;
            }
        }

        public bool SpendCurrency(int amount)
        {
            if (amount < 0 || Currency < amount)
            {
                return false;
            }

            Currency -= amount;
            return true;
        }

        internal void SetCurrency(int amount) => Currency = amount < 0 ? 0 : amount;

        internal void MarkPurchased(string nodeId) => _purchasedNodes.Add(nodeId);

        /// <summary>Registra o resultado de uma run encerrada.</summary>
        public void RecordRun(RunScore score, int daysSurvived)
        {
            RunsPlayed++;
            AddCurrency(score.MetaCurrency);

            if (daysSurvived > BestDayReached)
            {
                BestDayReached = daysSurvived;
            }

            if (score.TotalPoints > BestScore)
            {
                BestScore = score.TotalPoints;
            }

            // So a Vitoria abre o degrau seguinte; placar alto num Colapso nao abre
            // nada (spec difficulty-ladder). O teto real e a escada, que ignora
            // niveis alem do ultimo.
            if (score.Outcome == RunOutcome.Victory && score.LevelRank + 1 > HighestUnlockedLevel)
            {
                HighestUnlockedLevel = score.LevelRank + 1;
            }
        }

        /// <summary>Perfil novo: saldo zero e nenhum no comprado.</summary>
        public static MetaProfile NewProfile() => new MetaProfile();
    }
}
