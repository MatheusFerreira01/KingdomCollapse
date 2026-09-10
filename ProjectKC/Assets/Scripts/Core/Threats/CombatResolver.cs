using System;
using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Resultado discriminado de um ataque. A spec exige que o jogador veja forca,
    /// defesa e perdas separadas: sem isso ele nao consegue saber se a derrota veio
    /// de defesa insuficiente ou de escalada.
    /// </summary>
    public sealed class AttackReport
    {
        public AttackReport(ThreatKind kind, int day, int force, int defense)
        {
            Kind = kind;
            Day = day;
            Force = force;
            Defense = defense;
            TilesDestroyed = new List<Coord>();
            ResourcesLost = new ResourceAmounts();
        }

        public ThreatKind Kind { get; }

        public int Day { get; }

        public int Force { get; }

        public int Defense { get; }

        /// <summary>Forca que passou pela defesa. Zero significa ataque anulado.</summary>
        public int Overflow { get; internal set; }

        public List<Coord> TilesDestroyed { get; }

        public int BaseDamage { get; internal set; }

        /// <summary>
        /// Forca que o rival economico saqueou sem passar pela defesa. Repelir nao
        /// zera isto: e o que o estoque existe para absorver.
        /// </summary>
        public int RaidForce { get; internal set; }

        /// <summary>Recursos levados pelo ataque, saque e excedente somados.</summary>
        public ResourceAmounts ResourcesLost { get; }

        /// <summary>
        /// Conta para a campanha quando a defesa segura a forca, qualquer que seja o
        /// rival. Um ataque repelido ainda pode ter levado recurso por saque.
        /// </summary>
        public bool Repelled => Overflow <= 0;

        public bool CausedLoss =>
            TilesDestroyed.Count > 0 || BaseDamage > 0 || !ResourcesLost.IsEmpty;

        public override string ToString()
        {
            string lost = ResourcesLost.IsEmpty ? string.Empty : ", levou " + ResourcesLost;
            return Kind + " dia " + Day + ": forca " + Force + " vs defesa " + Defense +
                   " => " + TilesDestroyed.Count + " celula(s), " + BaseDamage + " de dano" + lost;
        }
    }

    public static class CombatResolver
    {
        /// <summary>Quanta forca excedente e absorvida por cada celula arrasada.</summary>
        public const int ForcePerTile = 6;

        /// <summary>
        /// Confronta forca contra defesa. O excedente come o territorio a partir da
        /// borda; o que ainda sobrar fere a base.
        /// </summary>
        public static AttackReport Resolve(RunState run, ScheduledThreat threat)
        {
            int defense = run.TotalDefense();
            AttackReport report = new AttackReport(threat.Kind, run.Day, threat.Force, defense);

            run.ConsumePendingDefense();

            int overflow = Math.Max(0, threat.Force - defense);
            report.Overflow = overflow;

            RivalIdentity identity = threat.Rival?.Identity;
            if (identity != null)
            {
                ResolveByAxis(run, report, identity, threat.Force, overflow);
                return report;
            }

            if (overflow <= 0)
            {
                return report;
            }

            // Incendio e colapso estrutural ferem a base direto; a horda come
            // territorio primeiro. A diferenca existe para que os tipos exijam
            // preparos diferentes, e nao so numeros diferentes.
            if (threat.Kind == ThreatKind.Horde)
            {
                overflow = DestroyBorderTiles(run, report, overflow);
            }

            if (overflow > 0)
            {
                report.BaseDamage = run.Damage(overflow);
            }

            return report;
        }

        /// <summary>
        /// Ataque de rival: o eixo da identidade decide onde a perda cai (design D4).
        /// Assalto so fere o que a defesa deixou passar; o rival economico saqueia
        /// sempre, e o excedente, se houver, soma ao saque.
        /// </summary>
        private static void ResolveByAxis(
            RunState run, AttackReport report, RivalIdentity identity, int force, int overflow)
        {
            switch (identity.Axis)
            {
                case PressureAxis.Territory:
                    overflow = DestroyBorderTiles(run, report, overflow);
                    if (overflow > 0)
                    {
                        report.BaseDamage = run.Damage(overflow);
                    }

                    return;

                case PressureAxis.Integrity:
                    if (overflow > 0)
                    {
                        report.BaseDamage = run.Damage(overflow);
                    }

                    return;

                case PressureAxis.Population:
                case PressureAxis.Resource:
                    Raid(run, report, identity, force, overflow);
                    return;
            }
        }

        private static void Raid(RunState run, AttackReport report, RivalIdentity identity, int force, int overflow)
        {
            if (identity.LostResource == null)
            {
                return;
            }

            report.RaidForce = (int)Math.Round(force * identity.RaidFraction, MidpointRounding.AwayFromZero);

            int loss = (int)Math.Round(
                (report.RaidForce + overflow) * identity.LossPerForce, MidpointRounding.AwayFromZero);
            if (loss <= 0)
            {
                return;
            }

            // Leva o que houver: saque sobre celeiro vazio nao vira divida nem dano.
            // Quem nao estocou perde tudo, e isso ja e a punicao.
            ResourceKind kind = identity.LostResource.Value;
            report.ResourcesLost[kind] = run.Remove(kind, loss);
        }

        private static int DestroyBorderTiles(RunState run, AttackReport report, int overflow)
        {
            List<Tile> border = run.Grid.BorderTiles();

            for (int i = 0; i < border.Count && overflow >= ForcePerTile; i++)
            {
                Tile tile = border[i];
                if (tile.Destroyed)
                {
                    continue;
                }

                // O Salao e a ultima coisa a cair: se ele fosse comido junto com a
                // borda, a run acabaria por posicao e nao por pressao.
                if (tile.Coord == run.Grid.HallCoord)
                {
                    continue;
                }

                run.Grid.Destroy(tile.Coord);
                report.TilesDestroyed.Add(tile.Coord);
                overflow -= ForcePerTile;
            }

            return overflow;
        }
    }
}
