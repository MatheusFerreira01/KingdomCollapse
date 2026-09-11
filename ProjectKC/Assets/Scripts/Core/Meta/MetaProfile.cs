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
        public const int CurrentVersion = 1;

        private readonly HashSet<string> _purchasedNodes = new HashSet<string>();
        private readonly HashSet<string> _unlockedDifficultyLevels = new HashSet<string>();

        public MetaProfile(int version = CurrentVersion)
        {
            Version = version;
        }

        public int Version { get; internal set; }

        public int Currency { get; private set; }

        public int RunsPlayed { get; internal set; }

        public int BestDayReached { get; internal set; }

        public int BestScore { get; internal set; }

        public IReadOnlyCollection<string> PurchasedNodes => _purchasedNodes;

        public bool HasNode(string nodeId) => _purchasedNodes.Contains(nodeId);

        public IReadOnlyCollection<string> UnlockedDifficultyLevels => _unlockedDifficultyLevels;

        /// <summary>
        /// O primeiro nivel da escada esta sempre disponivel — um perfil novo nao
        /// pode comecar sem ter o que jogar (task 5.1). Os demais exigem desbloqueio
        /// explicito por Vitoria.
        /// </summary>
        public bool IsDifficultyUnlocked(string levelId, IReadOnlyList<DifficultyLevel> ladder)
        {
            if (ladder != null && ladder.Count > 0 && ladder[0].Id == levelId)
            {
                return true;
            }

            return _unlockedDifficultyLevels.Contains(levelId);
        }

        internal void UnlockDifficultyLevel(string levelId)
        {
            if (!string.IsNullOrEmpty(levelId))
            {
                _unlockedDifficultyLevels.Add(levelId);
            }
        }

        /// <summary>
        /// Desbloqueia o nivel seguinte ao vencido. So Vitoria chama isto — Colapso
        /// nao desbloqueia nada (spec difficulty-ladder).
        /// </summary>
        public void UnlockNextDifficulty(string completedLevelId, IReadOnlyList<DifficultyLevel> ladder)
        {
            if (ladder == null)
            {
                return;
            }

            for (int i = 0; i < ladder.Count - 1; i++)
            {
                if (ladder[i].Id == completedLevelId)
                {
                    UnlockDifficultyLevel(ladder[i + 1].Id);
                    return;
                }
            }
        }

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
        }

        /// <summary>Perfil novo: saldo zero e nenhum no comprado.</summary>
        public static MetaProfile NewProfile() => new MetaProfile();
    }
}
