namespace KingdomCollapse.Core
{
    public enum TerrainType
    {
        Plain = 0,
        Forest = 1,
        Mine = 2,
        River = 3,
        Ruin = 4
    }

    /// <summary>
    /// Uma celula do reino. Nasce nao possuida e nao revelada: o terreno so aparece
    /// na compra, ou antes dela se algum efeito revelar (spec kingdom-grid).
    /// </summary>
    public sealed class Tile
    {
        public Tile(Coord coord, TerrainType terrain)
        {
            Coord = coord;
            Terrain = terrain;
            Owned = false;
            Revealed = false;
            Destroyed = false;
            Building = null;
        }

        public Coord Coord { get; }

        public TerrainType Terrain { get; internal set; }

        public bool Owned { get; internal set; }

        public bool Revealed { get; internal set; }

        /// <summary>
        /// Celula arrasada por ameaca ou evento. Continua possuida e pode ser
        /// reconstruida; apenas perdeu o edificio e parou de produzir.
        /// </summary>
        public bool Destroyed { get; internal set; }

        public BuildingDefinition Building { get; internal set; }

        public bool HasBuilding => Building != null;

        /// <summary>Possuida, sem edificio e nao arrasada: alvo valido de construcao.</summary>
        public bool IsBuildable => Owned && !HasBuilding && !Destroyed;

        public override string ToString()
        {
            return Coord + " " + Terrain + (Owned ? " possuida" : " livre") +
                   (HasBuilding ? " [" + Building.Id + "]" : string.Empty) +
                   (Destroyed ? " arrasada" : string.Empty);
        }
    }
}
