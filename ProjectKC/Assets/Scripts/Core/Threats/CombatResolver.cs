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
        }

        public ThreatKind Kind { get; }

        public int Day { get; }

        public int Force { get; }

        public int Defense { get; }

        /// <summary>Forca que passou pela defesa. Zero significa ataque anulado.</summary>
        public int Overflow { get; internal set; }

        public List<Coord> TilesDestroyed { get; }

        public int BaseDamage { get; internal set; }

        /// <summary>Populacao perdida por um rival de eixo Population. Nao depende de
        /// defesa (design D4) — cobrado mesmo quando o ataque e repelido.</summary>
        public int PopulationLost { get; internal set; }

        /// <summary>Recurso perdido por um rival de eixo Resource, e qual recurso.
        /// Mesma regra do Population: independente de defesa.</summary>
        public ResourceKind? ResourceLostKind { get; internal set; }

        public int ResourceLostAmount { get; internal set; }

        /// <summary>
        /// Se a defesa bastou pra segurar o ataque. Decide o progresso da campanha
        /// (spec rival-kingdoms) igual para qualquer eixo — mas eixo Population/
        /// Resource ainda cobra pedagio mesmo quando Repelled e true.
        /// </summary>
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
        /// Fracao da forca cobrada como pedagio por rival de eixo Population/Resource,
        /// sempre — defesa alta nao reduz isto (design D4, "nao resolvido por defesa").
        /// </summary>
        public const double EconomicTollFraction = 0.5;

        /// <summary>
        /// Confronta forca contra defesa. Sem identidade de rival (ameaca anonima da
        /// curva antiga), o comportamento e o de sempre: o excedente come o
        /// territorio a partir da borda, e o que sobrar fere a base. Com identidade,
        /// o eixo dela decide: Territory/Integrity seguem a mesma logica de excedente;
        /// Population/Resource cobram pedagio proporcional a forca, defesa ou nao.
        /// </summary>
        public static AttackReport Resolve(RunState run, ScheduledThreat threat)
        {
            int defense = run.TotalDefense();
            AttackReport report = new AttackReport(threat.Kind, run.Day, threat.Force, defense);

            run.ConsumePendingDefense();

            int overflow = threat.Force - defense;
            report.Overflow = Math.Max(0, overflow);

            RivalAxis axis = threat.Identity?.Axis ?? RivalAxis.Territory;

            switch (axis)
            {
                case RivalAxis.Population:
                    report.PopulationLost = ApplyEconomicToll(run, ResourceKind.Population, threat.Force);
                    break;

                case RivalAxis.Resource:
                    ResourceKind kind = threat.Identity.Resource;
                    report.ResourceLostKind = kind;
                    report.ResourceLostAmount = ApplyEconomicToll(run, kind, threat.Force);
                    break;

                default:
                    if (overflow > 0)
                    {
                        ResolveDefensiveAxis(run, threat, report, overflow, axis);
                    }

                    break;
            }

            return report;
        }

        /// <summary>Territory come celula da borda; Integrity fere a base direto — a
        /// mesma distincao que Horde/Fire ja faziam antes de existir rival.</summary>
        private static void ResolveDefensiveAxis(
            RunState run, ScheduledThreat threat, AttackReport report, int overflow, RivalAxis axis)
        {
            bool eatsTerritory = threat.Identity != null
                ? axis == RivalAxis.Territory
                : threat.Kind == ThreatKind.Horde;

            if (eatsTerritory)
            {
                overflow = DestroyBorderTiles(run, report, overflow);
            }

            if (overflow > 0)
            {
                report.BaseDamage = run.Damage(overflow);
            }
        }

        /// <summary>Sempre tira algo quando Force > 0, defesa nao entra na conta —
        /// e o que torna este eixo "nao resolvido por defesa".</summary>
        private static int ApplyEconomicToll(RunState run, ResourceKind kind, int force)
        {
            int toll = Math.Max(1, (int)Math.Ceiling(force * EconomicTollFraction));
            return run.Remove(kind, toll);
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
