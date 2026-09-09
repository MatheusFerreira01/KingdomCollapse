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
        }

        public ThreatKind Kind { get; }

        public int Day { get; }

        public int Force { get; }

        public int Defense { get; }

        /// <summary>Forca que passou pela defesa. Zero significa ataque anulado.</summary>
        public int Overflow { get; internal set; }

        public List<Coord> TilesDestroyed { get; }

        public int BaseDamage { get; internal set; }

        public bool Repelled => Overflow <= 0;

        public override string ToString()
        {
            return Kind + " dia " + Day + ": forca " + Force + " vs defesa " + Defense +
                   " => " + TilesDestroyed.Count + " celula(s), " + BaseDamage + " de dano";
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

            int overflow = threat.Force - defense;
            if (overflow <= 0)
            {
                report.Overflow = 0;
                return report;
            }

            report.Overflow = overflow;

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
