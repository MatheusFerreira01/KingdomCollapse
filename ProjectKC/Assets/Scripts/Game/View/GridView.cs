using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Desenha o reino. Reage aos eventos de dominio para saber QUANDO redesenhar e
    /// para encenar o que aconteceu, mas le o estado do grid para saber O QUE
    /// desenhar: reconstruir a partir do modelo e idempotente, enquanto acumular
    /// mutacoes por evento diverge silenciosamente na primeira falha.
    /// </summary>
    public sealed class GridView : MonoBehaviour
    {
        private readonly Dictionary<Coord, GameObject> _tiles = new Dictionary<Coord, GameObject>();
        private readonly Dictionary<Coord, GameObject> _buildings = new Dictionary<Coord, GameObject>();
        private readonly Dictionary<Coord, Material> _materials = new Dictionary<Coord, Material>();
        private readonly HashSet<Coord> _alive = new HashSet<Coord>();

        private RunState _run;
        private Transform _root;
        private Material _buildingMaterial;
        private Material _hallMaterial;

        public Coord? Selected { get; private set; }

        public void Initialize(RunState run)
        {
            _run = run;
            _root = new GameObject("Reino").transform;
            _root.SetParent(transform, false);
            _buildingMaterial = TerrainVisuals.CreateMaterial(TerrainVisuals.BuildingColor);
            _hallMaterial = TerrainVisuals.CreateMaterial(TerrainVisuals.HallColor);
            Sync();
        }

        public void Select(Coord? coord)
        {
            Selected = coord;
            Sync();
        }

        /// <summary>
        /// Redesenha a partir do estado atual. Cria o que falta, atualiza o que mudou
        /// e remove o que deixou de existir.
        /// </summary>
        public void Sync()
        {
            if (_run == null)
            {
                return;
            }

            _alive.Clear();

            foreach (Tile tile in _run.Grid.OwnedTilesOrdered())
            {
                _alive.Add(tile.Coord);
                SyncTile(tile, false);
            }

            // Celulas compraveis aparecem como fantasmas: e o realce que a spec pede
            // para o jogador enxergar onde pode expandir.
            List<Coord> purchasable = _run.Grid.PurchasableCoords(_run.Rules);
            for (int i = 0; i < purchasable.Count; i++)
            {
                _alive.Add(purchasable[i]);
                SyncTile(_run.Grid.TileAt(purchasable[i]), true);
            }

            RemoveStale();
        }

        private void SyncTile(Tile tile, bool ghost)
        {
            if (!_tiles.TryGetValue(tile.Coord, out GameObject block) || block == null)
            {
                block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "Tile " + tile.Coord;
                block.transform.SetParent(_root, false);

                TileView view = block.AddComponent<TileView>();
                view.Coord = tile.Coord;

                Material material = TerrainVisuals.CreateMaterial(Color.white);
                block.GetComponent<Renderer>().sharedMaterial = material;

                _tiles[tile.Coord] = block;
                _materials[tile.Coord] = material;
            }

            TileView tileView = block.GetComponent<TileView>();
            tileView.IsGhost = ghost;

            float height = ghost ? 0.10f : TerrainVisuals.HeightFor(tile.Terrain);
            block.transform.localScale = new Vector3(TerrainVisuals.TileSize, height, TerrainVisuals.TileSize);
            block.transform.localPosition = TerrainVisuals.WorldPosition(tile.Coord, height);

            TerrainVisuals.SetColor(_materials[tile.Coord], ColorFor(tile, ghost));
            SyncBuilding(tile, ghost);
        }

        private Color ColorFor(Tile tile, bool ghost)
        {
            if (Selected.HasValue && Selected.Value == tile.Coord)
            {
                return TerrainVisuals.SelectionColor;
            }

            if (ghost)
            {
                // Terreno so aparece se ja foi revelado; o resto continua incognito.
                return tile.Revealed
                    ? Color.Lerp(TerrainVisuals.ColorFor(tile.Terrain), TerrainVisuals.PurchasableColor, 0.45f)
                    : TerrainVisuals.HiddenTint;
            }

            if (tile.Destroyed)
            {
                return TerrainVisuals.DestroyedTint;
            }

            return TerrainVisuals.ColorFor(tile.Terrain);
        }

        private void SyncBuilding(Tile tile, bool ghost)
        {
            bool shouldHaveBuilding = !ghost && tile.HasBuilding && !tile.Destroyed;

            if (!shouldHaveBuilding)
            {
                if (_buildings.TryGetValue(tile.Coord, out GameObject stale) && stale != null)
                {
                    Destroy(stale);
                }

                _buildings.Remove(tile.Coord);
                return;
            }

            if (!_buildings.TryGetValue(tile.Coord, out GameObject marker) || marker == null)
            {
                marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = "Building " + tile.Coord;
                marker.transform.SetParent(_root, false);

                // O bloco do edificio nao deve roubar o clique da celula.
                Collider collider = marker.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                _buildings[tile.Coord] = marker;
            }

            bool isHall = tile.Coord == _run.Grid.HallCoord;
            float terrainHeight = TerrainVisuals.HeightFor(tile.Terrain);
            float markerHeight = isHall ? 0.55f : 0.35f;
            float width = isHall ? 0.5f : 0.4f;

            marker.transform.localScale = new Vector3(width, markerHeight, width);
            marker.transform.localPosition = new Vector3(
                tile.Coord.X * TerrainVisuals.TileStep,
                terrainHeight + markerHeight * 0.5f,
                tile.Coord.Y * TerrainVisuals.TileStep);

            marker.GetComponent<Renderer>().sharedMaterial = isHall ? _hallMaterial : _buildingMaterial;
        }

        private void RemoveStale()
        {
            List<Coord> toRemove = new List<Coord>();

            foreach (KeyValuePair<Coord, GameObject> pair in _tiles)
            {
                if (!_alive.Contains(pair.Key))
                {
                    toRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                Coord coord = toRemove[i];

                if (_tiles.TryGetValue(coord, out GameObject block) && block != null)
                {
                    Destroy(block);
                }

                if (_buildings.TryGetValue(coord, out GameObject marker) && marker != null)
                {
                    Destroy(marker);
                }

                _tiles.Remove(coord);
                _buildings.Remove(coord);
                _materials.Remove(coord);
            }
        }

        /// <summary>
        /// Posicao no mundo de cada celula compravel. O HUD usa para desenhar o preco
        /// em cima do bloco: sem o custo a vista, o jogador clica e leva "ouro
        /// insuficiente" sem saber quanto falta.
        /// </summary>
        public List<KeyValuePair<Coord, Vector3>> GhostAnchors()
        {
            List<KeyValuePair<Coord, Vector3>> anchors = new List<KeyValuePair<Coord, Vector3>>();

            foreach (KeyValuePair<Coord, GameObject> pair in _tiles)
            {
                GameObject block = pair.Value;
                if (block == null)
                {
                    continue;
                }

                TileView view = block.GetComponent<TileView>();
                if (view == null || !view.IsGhost)
                {
                    continue;
                }

                anchors.Add(new KeyValuePair<Coord, Vector3>(pair.Key, block.transform.position));
            }

            return anchors;
        }

        /// <summary>Bounds do que esta desenhado. A camera usa para enquadrar o reino.</summary>
        public Bounds WorldBounds()
        {
            if (_run == null || _tiles.Count == 0)
            {
                return new Bounds(Vector3.zero, new Vector3(4f, 1f, 4f));
            }

            bool first = true;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

            foreach (KeyValuePair<Coord, GameObject> pair in _tiles)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                if (first)
                {
                    bounds = new Bounds(pair.Value.transform.position, Vector3.one * TerrainVisuals.TileStep);
                    first = false;
                    continue;
                }

                bounds.Encapsulate(pair.Value.transform.position);
            }

            bounds.Expand(TerrainVisuals.TileStep);
            return bounds;
        }
    }
}
