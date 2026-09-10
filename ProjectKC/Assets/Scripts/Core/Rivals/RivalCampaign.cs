using System;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Estado da guerra contra um rival durante a run. O rival e dado imutavel; o que
    /// avanca dia a dia vive aqui, para que duas runs com o mesmo rival nao
    /// compartilhem progresso.
    /// </summary>
    public sealed class RivalCampaign
    {
        public RivalCampaign(RivalDefinition rival, int enteredOnDay = 1)
        {
            Rival = rival ?? throw new ArgumentNullException(nameof(rival));
            EnteredOnDay = enteredOnDay;
        }

        public RivalDefinition Rival { get; }

        /// <summary>Dia em que o rival entrou em guerra.</summary>
        public int EnteredOnDay { get; }

        /// <summary>
        /// Ataques ja agendados nesta campanha. E o indice da curva: a forca segue a
        /// ordem dos ataques, e nao o dia, para que atrasar um ataque pelo clima nao
        /// o torne mais forte.
        /// </summary>
        public int AttacksLaunched { get; private set; }

        /// <summary>
        /// Ataques repelidos. So repelir conta: o ataque que passa nao aproxima a
        /// derrota do rival, mas tambem nao a afasta (spec rival-kingdoms).
        /// </summary>
        public int AttacksRepelled { get; private set; }

        /// <summary>Ataques que passaram pela defesa. Nao encerram a campanha.</summary>
        public int AttacksSuffered { get; private set; }

        public int AttacksRemaining => Math.Max(0, Rival.AttacksToDefeat - AttacksRepelled);

        public bool IsDefeated => AttacksRepelled >= Rival.AttacksToDefeat;

        /// <summary>Forca do proximo ataque, e avanca a curva. So o relogio agenda.</summary>
        internal int LaunchNextAttack()
        {
            int force = Rival.Campaign.ForceFor(AttacksLaunched);
            AttacksLaunched++;
            return force;
        }

        internal void RecordOutcome(bool repelled)
        {
            if (IsDefeated)
            {
                return;
            }

            if (repelled)
            {
                AttacksRepelled++;
            }
            else
            {
                AttacksSuffered++;
            }
        }

        public override string ToString() =>
            Rival.Id + " (" + AttacksRepelled + "/" + Rival.AttacksToDefeat + " repelidos)";
    }
}
