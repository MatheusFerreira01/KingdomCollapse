using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Para onde a população vai quando não dá para operar tudo. É a decisão central
    /// da economia: a mesma gente não guarnece a torre e trabalha no campo.
    /// </summary>
    public enum WorkerStance
    {
        /// <summary>Ordem estável do território, sem preferência.</summary>
        Balanced = 0,

        /// <summary>Guarnece antes de produzir. Custa produção, segura o ataque.</summary>
        DefenseFirst = 1,

        /// <summary>Produz antes de guarnecer. Rende mais, e deixa as muralhas vazias.</summary>
        ProductionFirst = 2
    }

    /// <summary>
    /// Quem está operando o quê hoje. É derivada do estado — população, edifícios e
    /// postura — e não guardada: recalcular a partir do modelo é idempotente, e
    /// acumular alocação como estado divergiria em silêncio na primeira falha.
    /// </summary>
    public sealed class WorkerAllocation
    {
        private readonly HashSet<Coord> _staffed = new HashSet<Coord>();

        private WorkerAllocation(WorkerStance stance)
        {
            Stance = stance;
        }

        public WorkerStance Stance { get; }

        /// <summary>Trabalhadores efetivamente empregados.</summary>
        public int Assigned { get; private set; }

        /// <summary>Trabalhadores que os edifícios do reino exigiriam para operar tudo.</summary>
        public int Required { get; private set; }

        /// <summary>População sem posto de trabalho.</summary>
        public int Unassigned { get; private set; }

        /// <summary>Edifícios que ficaram sem gente.</summary>
        public int IdleBuildings { get; private set; }

        public bool IsFullyStaffed => Assigned >= Required;

        public bool IsStaffed(Coord coord) => _staffed.Contains(coord);

        /// <summary>
        /// Distribui a população pelos edifícios. Um edifício que não exige gente
        /// opera sempre — é o caso do conteúdo que não usa trabalhadores.
        /// </summary>
        public static WorkerAllocation For(KingdomGrid grid, int population, WorkerStance stance)
        {
            WorkerAllocation allocation = new WorkerAllocation(stance);
            List<Tile> tiles = grid.OwnedTilesOrdered();
            SortByStance(tiles, stance);

            int available = population;

            for (int i = 0; i < tiles.Count; i++)
            {
                Tile tile = tiles[i];

                if (tile.Destroyed || !tile.HasBuilding)
                {
                    continue;
                }

                int needed = tile.Building.WorkersRequired;
                allocation.Required += needed;

                if (needed == 0)
                {
                    allocation._staffed.Add(tile.Coord);
                    continue;
                }

                if (available >= needed)
                {
                    available -= needed;
                    allocation.Assigned += needed;
                    allocation._staffed.Add(tile.Coord);
                    continue;
                }

                // Sem gente para o posto inteiro, o edifício fica ocioso. Meio
                // edifício operando seria pior que nenhum: esconderia do jogador
                // que ele não tem população para o que construiu.
                allocation.IdleBuildings++;
            }

            allocation.Unassigned = available;
            return allocation;
        }

        private static void SortByStance(List<Tile> tiles, WorkerStance stance)
        {
            if (stance == WorkerStance.Balanced)
            {
                return;
            }

            bool defenseFirst = stance == WorkerStance.DefenseFirst;

            tiles.Sort((a, b) =>
            {
                int rankA = Rank(a, defenseFirst);
                int rankB = Rank(b, defenseFirst);

                if (rankA != rankB)
                {
                    return rankA.CompareTo(rankB);
                }

                // Empate resolvido pela ordem do território, para que a mesma
                // situação produza sempre a mesma alocação.
                int byY = a.Coord.Y.CompareTo(b.Coord.Y);
                return byY != 0 ? byY : a.Coord.X.CompareTo(b.Coord.X);
            });
        }

        private static int Rank(Tile tile, bool defenseFirst)
        {
            if (!tile.HasBuilding)
            {
                return 2;
            }

            bool defensive = tile.Building.IsDefensive;
            return defensive == defenseFirst ? 0 : 1;
        }

        public override string ToString()
        {
            return Assigned + "/" + Required + " trabalhando, " + Unassigned + " sem posto, " +
                   IdleBuildings + " edificio(s) ocioso(s)";
        }
    }
}
