using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    public interface ITerrainGenerator
    {
        TerrainType TerrainAt(Coord coord);
    }

    /// <summary>
    /// Terreno derivado da semente e da coordenada, sem estado. A consequencia
    /// desejada e que o mapa nao dependa da ordem de compra: a mesma semente produz
    /// o mesmo terreno em cada celula, expanda o jogador para onde expandir.
    /// </summary>
    public sealed class SeededTerrainGenerator : ITerrainGenerator
    {
        private readonly RunRandom _random;
        private readonly TerrainType[] _terrains;
        private readonly double[] _weights;
        private readonly Coord _hallCoord;

        public SeededTerrainGenerator(RunRandom random, Coord hallCoord, IReadOnlyDictionary<TerrainType, double> weights = null)
        {
            _random = random;
            _hallCoord = hallCoord;

            IReadOnlyDictionary<TerrainType, double> source = weights ?? DefaultWeights();
            _terrains = new TerrainType[source.Count];
            _weights = new double[source.Count];

            int i = 0;
            foreach (KeyValuePair<TerrainType, double> pair in source)
            {
                _terrains[i] = pair.Key;
                _weights[i] = pair.Value;
                i++;
            }
        }

        public static Dictionary<TerrainType, double> DefaultWeights()
        {
            return new Dictionary<TerrainType, double>
            {
                { TerrainType.Plain, 0.34 },
                { TerrainType.Forest, 0.24 },
                { TerrainType.Mine, 0.16 },
                { TerrainType.River, 0.16 },
                { TerrainType.Ruin, 0.10 }
            };
        }

        public TerrainType TerrainAt(Coord coord)
        {
            // O Salao sempre nasce em planicie: a primeira celula nao pode ser um
            // terreno que impeca as construcoes iniciais da run.
            if (coord == _hallCoord)
            {
                return TerrainType.Plain;
            }

            double roll = _random.StableValueAt(RandomChannel.Terrain, coord);
            double total = 0;
            for (int i = 0; i < _weights.Length; i++)
            {
                total += _weights[i];
            }

            double target = roll * total;
            for (int i = 0; i < _terrains.Length; i++)
            {
                target -= _weights[i];
                if (target < 0)
                {
                    return _terrains[i];
                }
            }

            return _terrains[_terrains.Length - 1];
        }
    }

    /// <summary>Gerador fixo. Existe para os testes controlarem o mapa.</summary>
    public sealed class FixedTerrainGenerator : ITerrainGenerator
    {
        private readonly Dictionary<Coord, TerrainType> _explicitTerrain;
        private readonly TerrainType _fallback;

        public FixedTerrainGenerator(TerrainType fallback, Dictionary<Coord, TerrainType> explicitTerrain = null)
        {
            _fallback = fallback;
            _explicitTerrain = explicitTerrain ?? new Dictionary<Coord, TerrainType>();
        }

        public void Set(Coord coord, TerrainType terrain) => _explicitTerrain[coord] = terrain;

        public TerrainType TerrainAt(Coord coord)
        {
            return _explicitTerrain.TryGetValue(coord, out TerrainType terrain) ? terrain : _fallback;
        }
    }
}
