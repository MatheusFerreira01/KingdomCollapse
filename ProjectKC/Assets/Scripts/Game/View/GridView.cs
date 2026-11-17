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
        private readonly Dictionary<Coord, string> _buildingKeys = new Dictionary<Coord, string>();
        private readonly Dictionary<Coord, GameObject> _decor = new Dictionary<Coord, GameObject>();
        private readonly Dictionary<Coord, string> _decorKeys = new Dictionary<Coord, string>();
        private readonly Dictionary<Coord, GameObject> _surfaces = new Dictionary<Coord, GameObject>();
        private readonly Dictionary<Coord, string> _surfaceKeys = new Dictionary<Coord, string>();
        private readonly Dictionary<Coord, Material> _materials = new Dictionary<Coord, Material>();
        private readonly Dictionary<Coord, Color> _baseColors = new Dictionary<Coord, Color>();
        private readonly HashSet<Coord> _alive = new HashSet<Coord>();
        private readonly HashSet<Coord> _idle = new HashSet<Coord>();
        private readonly HashSet<Coord> _highlights = new HashSet<Coord>();

        private RunState _run;
        private Transform _root;
        private Color _highlightColor = TerrainVisuals.TargetColor;

        public Coord? Selected { get; private set; }

        /// <summary>Moldura em volta da celula selecionada. A cor sozinha se perdia em terreno claro.</summary>
        private GameObject _selectionFrame;

        /// <summary>Celula sob o cursor. So muda a cor; nao muda estado nenhum.</summary>
        public Coord? Hovered { get; private set; }

        public void Initialize(RunState run)
        {
            Clear();
            _run = run;
            _root = new GameObject("Reino").transform;
            _root.SetParent(transform, false);
            Sync();
        }

        /// <summary>Desmonta tudo. Usado ao trocar de run ou voltar ao menu.</summary>
        public void Clear()
        {
            if (_root != null)
            {
                Destroy(_root.gameObject);
                _root = null;
            }

            _tiles.Clear();
            _buildings.Clear();
            _buildingKeys.Clear();
            _decor.Clear();
            _decorKeys.Clear();
            _surfaces.Clear();
            _surfaceKeys.Clear();
            _materials.Clear();
            _baseColors.Clear();
            _highlights.Clear();
            _idle.Clear();
            Selected = null;
            Hovered = null;
            _run = null;

            if (_selectionFrame != null)
            {
                _selectionFrame.SetActive(false);
            }

            ClearRaid();
        }

        public void Select(Coord? coord)
        {
            Selected = coord;
            Sync();
        }

        public void SetHover(Coord? coord)
        {
            if (Hovered.HasValue == coord.HasValue && (!coord.HasValue || Hovered.Value == coord.Value))
            {
                return;
            }

            Hovered = coord;
            Recolor();
        }

        /// <summary>
        /// Marca as celulas que aceitam a acao em curso (alvo de carta). Vazio limpa.
        /// </summary>
        public void SetHighlights(IEnumerable<Coord> coords, Color? color = null)
        {
            _highlights.Clear();
            if (coords != null)
            {
                foreach (Coord coord in coords)
                {
                    _highlights.Add(coord);
                }
            }

            _highlightColor = color ?? TerrainVisuals.TargetColor;
            Recolor();
        }

        public bool IsHighlighted(Coord coord) => _highlights.Contains(coord);

        /// <summary>
        /// Redesenha a partir do estado atual. Cria o que falta, atualiza o que mudou
        /// e remove o que deixou de existir.
        /// </summary>
        public void Sync()
        {
            if (_run == null || _root == null)
            {
                return;
            }

            _alive.Clear();

            // Quem esta sem gente hoje. Lido uma vez por sincronizacao, e nao por
            // celula, para que a leitura seja consistente dentro do mesmo quadro.
            _idle.Clear();
            WorkerAllocation allocation = _run.Allocation();

            foreach (Tile tile in _run.Grid.OwnedTilesOrdered())
            {
                _alive.Add(tile.Coord);

                if (tile.HasBuilding && !tile.Destroyed && !allocation.IsStaffed(tile.Coord))
                {
                    _idle.Add(tile.Coord);
                }

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
            Recolor();
            UpdateSelectionFrame();
        }

        private void UpdateSelectionFrame()
        {
            GameObject block = null;
            bool visible = Selected.HasValue && _tiles.TryGetValue(Selected.Value, out block) && block != null;

            if (!visible)
            {
                if (_selectionFrame != null)
                {
                    _selectionFrame.SetActive(false);
                }

                return;
            }

            if (_selectionFrame == null)
            {
                _selectionFrame = BuildSelectionFrame();
            }

            float top = block.transform.position.y + block.transform.localScale.y * 0.5f;
            _selectionFrame.transform.position = new Vector3(block.transform.position.x, top + 0.02f, block.transform.position.z);
            _selectionFrame.SetActive(true);
        }

        private GameObject BuildSelectionFrame()
        {
            const float Thickness = 0.07f;
            const float Height = 0.05f;
            float half = TerrainVisuals.TileSize * 0.5f - Thickness * 0.5f;

            GameObject frame = new GameObject("Selecao");
            frame.transform.SetParent(transform, false);

            Material material = TerrainVisuals.CreateMaterial(TerrainVisuals.SelectionColor);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", TerrainVisuals.SelectionColor * 0.9f);
            }

            for (int i = 0; i < 4; i++)
            {
                bool alongX = i < 2;
                float side = i % 2 == 0 ? -half : half;

                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = "Borda";
                bar.transform.SetParent(frame.transform, false);
                bar.transform.localScale = alongX
                    ? new Vector3(TerrainVisuals.TileSize, Height, Thickness)
                    : new Vector3(Thickness, Height, TerrainVisuals.TileSize);
                bar.transform.localPosition = alongX ? new Vector3(0f, 0f, side) : new Vector3(side, 0f, 0f);

                // Sem colisor: o clique no mundo tem que chegar na celula embaixo.
                Collider collider = bar.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                Renderer renderer = bar.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return frame;
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

            _baseColors[tile.Coord] = BaseColorFor(tile, ghost);

            SyncSurface(tile, ghost);
            SyncBuilding(tile, ghost);
            SyncDecor(tile, ghost);
        }

        private Color BaseColorFor(Tile tile, bool ghost)
        {
            if (ghost)
            {
                // Terreno so aparece se ja foi revelado; o resto continua incognito.
                return tile.Revealed
                    ? Color.Lerp(TerrainVisuals.ColorFor(tile.Terrain), TerrainVisuals.PurchasableColor, 0.35f)
                    : TerrainVisuals.HiddenTint;
            }

            if (tile.Destroyed)
            {
                return TerrainVisuals.DestroyedTint;
            }

            if (_idle.Contains(tile.Coord))
            {
                return Color.Lerp(TerrainVisuals.ColorFor(tile.Terrain), TerrainVisuals.IdleTint, 0.55f);
            }

            return TerrainVisuals.ColorFor(tile.Terrain);
        }

        /// <summary>Aplica selecao, alvo e cursor por cima da cor de base de cada celula.</summary>
        private void Recolor()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);

            foreach (KeyValuePair<Coord, Material> pair in _materials)
            {
                if (pair.Value == null || !_baseColors.TryGetValue(pair.Key, out Color color))
                {
                    continue;
                }

                if (_highlights.Contains(pair.Key))
                {
                    color = Color.Lerp(color, _highlightColor, 0.45f + 0.25f * pulse);
                }

                if (Selected.HasValue && Selected.Value == pair.Key)
                {
                    color = Color.Lerp(color, TerrainVisuals.SelectionColor, 0.60f);
                }

                if (Hovered.HasValue && Hovered.Value == pair.Key)
                {
                    color = Color.Lerp(color, Color.white, 0.28f);
                }

                TerrainVisuals.SetColor(pair.Value, color);
            }
        }

        private void Update()
        {
            // So ha o que animar enquanto houver alvo pulsando.
            if (_highlights.Count > 0)
            {
                Recolor();
            }

            TickRaid();
        }

        // --- Ataque encenado no tabuleiro ---

        private const float RaidSeconds = 2.2f;

        private GameObject _raid;
        private readonly List<Transform> _raiders = new List<Transform>();
        private readonly List<Vector3> _raidFrom = new List<Vector3>();
        private readonly List<Vector3> _raidTo = new List<Vector3>();
        private float _raidTime;
        private bool _raidRepelled;

        /// <summary>
        /// Mostra o ataque chegando: um bando vem de fora do reino. Repelido, bate na
        /// borda e recua; se a defesa cede, atravessa ate o Salao. E so encenacao: o
        /// resultado ja foi resolvido pelo Core.
        /// </summary>
        public void PlayRaid(bool repelled, int force)
        {
            ClearRaid();
            if (_run == null)
            {
                return;
            }

            Bounds bounds = WorldBounds();
            Vector3 hall = TerrainVisuals.WorldPosition(_run.Grid.HallCoord, 0f);
            hall.y = 0.14f;
            float reach = Mathf.Max(bounds.extents.x, bounds.extents.z);
            int count = Mathf.Clamp(force / 4, 3, 14);

            _raid = new GameObject("Ataque");
            _raid.transform.SetParent(transform, false);
            Material material = BuildingVisuals.MaterialFor(new Color(0.78f, 0.14f, 0.12f));
            _raidRepelled = repelled;
            _raidTime = 0f;

            for (int i = 0; i < count; i++)
            {
                // Chegam em leque pelo lado de tras, que e o que a camera fixa ve melhor.
                float angle = Mathf.Deg2Rad * (20f + 50f * (count == 1 ? 0.5f : i / (float)(count - 1)));
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 from = bounds.center + direction * (reach + 5f + (i % 3) * 0.5f);
                Vector3 edge = bounds.center + direction * (reach + 0.35f);
                from.y = 0.14f;
                edge.y = 0.14f;

                GameObject raider = GameObject.CreatePrimitive(PrimitiveType.Cube);
                raider.name = "Invasor";
                raider.transform.SetParent(_raid.transform, false);
                raider.transform.localScale = new Vector3(0.20f, 0.28f, 0.20f);
                raider.transform.position = from;

                Collider collider = raider.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                raider.GetComponent<Renderer>().sharedMaterial = material;
                _raiders.Add(raider.transform);
                _raidFrom.Add(from);
                _raidTo.Add(repelled ? edge : hall + direction * (0.3f + 0.12f * i));
            }
        }

        private void TickRaid()
        {
            if (_raid == null)
            {
                return;
            }

            _raidTime += Time.deltaTime;
            float t = Mathf.Clamp01(_raidTime / RaidSeconds);

            for (int i = 0; i < _raiders.Count; i++)
            {
                if (_raiders[i] == null)
                {
                    continue;
                }

                // Repelido: avanca ate a borda na primeira metade e recua na segunda.
                float along = _raidRepelled
                    ? (t < 0.55f ? t / 0.55f : 1f - (t - 0.55f) / 0.45f * 0.7f)
                    : Mathf.SmoothStep(0f, 1f, t);
                Vector3 position = Vector3.Lerp(_raidFrom[i], _raidTo[i], along);
                position.y = 0.14f + 0.06f * Mathf.Abs(Mathf.Sin((_raidTime * 9f) + i));
                _raiders[i].position = position;
            }

            if (_raidTime > RaidSeconds + 0.6f)
            {
                ClearRaid();
            }
        }

        private void ClearRaid()
        {
            if (_raid != null)
            {
                Destroy(_raid);
                _raid = null;
            }

            _raiders.Clear();
            _raidFrom.Clear();
            _raidTo.Clear();
        }

        private void SyncBuilding(Tile tile, bool ghost)
        {
            bool shouldHaveBuilding = !ghost && tile.HasBuilding && !tile.Destroyed;
            string key = shouldHaveBuilding
                ? tile.Building.Id + (_idle.Contains(tile.Coord) ? "|idle" : "|ok") + "|" + tile.Terrain
                : null;

            _buildingKeys.TryGetValue(tile.Coord, out string currentKey);
            if (currentKey == key)
            {
                return;
            }

            if (_buildings.TryGetValue(tile.Coord, out GameObject stale) && stale != null)
            {
                Destroy(stale);
            }

            _buildings.Remove(tile.Coord);
            _buildingKeys.Remove(tile.Coord);

            if (!shouldHaveBuilding)
            {
                return;
            }

            GameObject marker = BuildingVisuals.Create(tile.Building.Id, _idle.Contains(tile.Coord));
            marker.transform.SetParent(_root, false);
            marker.transform.localPosition = new Vector3(
                tile.Coord.X * TerrainVisuals.TileStep,
                TerrainVisuals.HeightFor(tile.Terrain),
                tile.Coord.Y * TerrainVisuals.TileStep);

            _buildings[tile.Coord] = marker;
            _buildingKeys[tile.Coord] = key;
        }

        /// <summary>A textura do terreno em cima do bloco. Some em celula arrasada e em fantasma.</summary>
        private void SyncSurface(Tile tile, bool ghost)
        {
            bool wants = !ghost && !tile.Destroyed;
            string key = wants ? tile.Terrain.ToString() : null;

            _surfaceKeys.TryGetValue(tile.Coord, out string currentKey);
            if (currentKey == key)
            {
                return;
            }

            if (_surfaces.TryGetValue(tile.Coord, out GameObject stale) && stale != null)
            {
                Destroy(stale);
            }

            _surfaces.Remove(tile.Coord);
            _surfaceKeys.Remove(tile.Coord);

            if (!wants)
            {
                return;
            }

            float height = TerrainVisuals.HeightFor(tile.Terrain);
            GameObject surface = BuildingVisuals.CreateSurface(tile.Terrain, tile.Coord, height);
            surface.transform.SetParent(_root, false);
            surface.transform.localPosition = new Vector3(
                tile.Coord.X * TerrainVisuals.TileStep, height, tile.Coord.Y * TerrainVisuals.TileStep);
            _surfaces[tile.Coord] = surface;
            _surfaceKeys[tile.Coord] = key;
        }

        private void SyncDecor(Tile tile, bool ghost)
        {
            bool wantsDecor = !ghost && !tile.HasBuilding;
            string key = wantsDecor ? tile.Terrain + (tile.Destroyed ? "|ruined" : "|ok") : null;

            _decorKeys.TryGetValue(tile.Coord, out string currentKey);
            if (currentKey == key)
            {
                return;
            }

            if (_decor.TryGetValue(tile.Coord, out GameObject stale) && stale != null)
            {
                Destroy(stale);
            }

            _decor.Remove(tile.Coord);
            _decorKeys.Remove(tile.Coord);

            if (!wantsDecor)
            {
                return;
            }

            _decorKeys[tile.Coord] = key;

            GameObject decor = BuildingVisuals.CreateDecor(tile.Terrain, tile.Destroyed, tile.Coord);
            if (decor == null)
            {
                return;
            }

            decor.transform.SetParent(_root, false);
            decor.transform.localPosition = new Vector3(
                tile.Coord.X * TerrainVisuals.TileStep,
                TerrainVisuals.HeightFor(tile.Terrain),
                tile.Coord.Y * TerrainVisuals.TileStep);
            _decor[tile.Coord] = decor;
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

                if (_decor.TryGetValue(coord, out GameObject decor) && decor != null)
                {
                    Destroy(decor);
                }

                _tiles.Remove(coord);
                _buildings.Remove(coord);
                _buildingKeys.Remove(coord);
                if (_surfaces.TryGetValue(coord, out GameObject surface) && surface != null)
                {
                    Destroy(surface);
                }

                _surfaces.Remove(coord);
                _surfaceKeys.Remove(coord);
                _decor.Remove(coord);
                _decorKeys.Remove(coord);
                _materials.Remove(coord);
                _baseColors.Remove(coord);
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

        /// <summary>Posicao no mundo do topo de uma celula, para ancorar numero flutuante.</summary>
        public Vector3 AnchorFor(Coord coord)
        {
            if (_tiles.TryGetValue(coord, out GameObject block) && block != null)
            {
                return block.transform.position + Vector3.up * 0.6f;
            }

            return TerrainVisuals.WorldPosition(coord, 0.4f);
        }

        public bool IsIdle(Coord coord) => _idle.Contains(coord);

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
