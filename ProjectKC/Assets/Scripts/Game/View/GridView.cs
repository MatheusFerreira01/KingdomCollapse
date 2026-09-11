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
        private readonly Dictionary<Coord, List<GameObject>> _terrainProps = new Dictionary<Coord, List<GameObject>>();
        private readonly Dictionary<Coord, TerrainType> _terrainPropKind = new Dictionary<Coord, TerrainType>();
        private readonly Dictionary<(Coord, Coord), GameObject> _connectors = new Dictionary<(Coord, Coord), GameObject>();
        private Material _roadMaterial;
        private Material _bridgeMaterial;

        /// <summary>
        /// Cantos da celula onde a decoracao de terreno pode nascer — o meio fica
        /// sempre livre, para o predio caber sem a arvore/rocha grudada nele (pedido
        /// do Matheus: floresta tem arvore na borda, o centro e onde o predio senta).
        /// </summary>
        private static readonly Vector2[] PropCorners =
        {
            new Vector2(0.32f, 0.32f),
            new Vector2(-0.32f, 0.32f),
            new Vector2(0.32f, -0.32f),
            new Vector2(-0.32f, -0.32f),
        };
        private readonly HashSet<Coord> _alive = new HashSet<Coord>();

        private readonly HashSet<Coord> _idle = new HashSet<Coord>();

        private RunState _run;
        private Transform _root;
        private Material _buildingMaterial;
        private Material _hallMaterial;
        private Material _idleMaterial;
        private TerrainVisualSet _terrainVisuals;
        private Dictionary<string, GameObject> _buildingPrefabs;
        private readonly Dictionary<Coord, string> _buildingMarkerKind = new Dictionary<Coord, string>();
        private readonly Dictionary<Coord, Color[]> _buildingOriginalColors = new Dictionary<Coord, Color[]>();
        private readonly Dictionary<Coord, float> _buildingBaseScale = new Dictionary<Coord, float>();

        /// <summary>Pegada alvo (metros de tabuleiro) de um marcador de predio com
        /// prefab — menor que 1 tile pra sobrar respiro entre vizinhos.</summary>
        private const float BuildingFootprint = 0.75f;

        /// <summary>Pegada alvo de cada instancia de decoracao de terreno — bem menor
        /// que o predio, porque sao 3 por celula, nos cantos.</summary>
        private const float PropFootprint = 0.34f;

        private WeatherDefinition _weather;
        private readonly Dictionary<Coord, GameObject> _tileEffects = new Dictionary<Coord, GameObject>();
        private readonly Dictionary<Coord, int> _tileEffectSign = new Dictionary<Coord, int>();

        public Coord? Selected { get; private set; }

        /// <summary>Clima do dia, usado para saber qual celula esta bufada ou
        /// penalizada (spec weather-scenery). Nao redesenha sozinho: quem muda o
        /// clima chama Sync() em seguida.</summary>
        public void SetWeather(WeatherDefinition weather)
        {
            _weather = weather;
        }

        public void Initialize(RunState run, TerrainVisualSet terrainVisuals = null,
            Dictionary<string, GameObject> buildingPrefabs = null)
        {
            _run = run;
            _terrainVisuals = terrainVisuals;
            _buildingPrefabs = buildingPrefabs ?? new Dictionary<string, GameObject>();
            _root = new GameObject("Reino").transform;
            _root.SetParent(transform, false);
            _buildingMaterial = TerrainVisuals.CreateMaterial(TerrainVisuals.BuildingColor);
            _hallMaterial = TerrainVisuals.CreateMaterial(TerrainVisuals.HallColor);
            _idleMaterial = TerrainVisuals.CreateMaterial(TerrainVisuals.IdleMarkerColor);
            _roadMaterial = TerrainVisuals.CreateMaterial(new Color(0.55f, 0.47f, 0.35f));
            _bridgeMaterial = TerrainVisuals.CreateMaterial(new Color(0.45f, 0.36f, 0.24f));
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
            SyncConnectors();
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

            Material tileMaterial = _materials[tile.Coord];
            TerrainVisuals.SetColor(tileMaterial, ColorFor(tile, ghost));

            // So mostra a textura real em celula revelada: fantasma incognito
            // continua no HiddenTint, sem entregar o tipo de terreno de graca.
            if (_terrainVisuals != null && (!ghost || tile.Revealed))
            {
                TerrainVisuals.SetTexture(tileMaterial, _terrainVisuals.TextureFor(tile.Terrain));
            }

            SyncBuilding(tile, ghost);
            SyncTerrainProp(tile, ghost);
            SyncWeatherEffect(tile, ghost);
        }

        /// <summary>
        /// Particula dourada sobre celula bufada pelo clima, preta sobre penalizada
        /// (spec weather-scenery). O sinal vem de WeatherDefinition.ProductionFor no
        /// terreno da celula — o mesmo numero que ja decide a producao do dia.
        /// </summary>
        private void SyncWeatherEffect(Tile tile, bool ghost)
        {
            int delta = (!ghost && !tile.Destroyed && _weather != null) ? _weather.ProductionFor(tile.Terrain) : 0;
            int sign = delta > 0 ? 1 : (delta < 0 ? -1 : 0);

            if (sign == 0)
            {
                if (_tileEffects.TryGetValue(tile.Coord, out GameObject stale) && stale != null)
                {
                    Destroy(stale);
                }

                _tileEffects.Remove(tile.Coord);
                _tileEffectSign.Remove(tile.Coord);
                return;
            }

            bool needsRebuild = !_tileEffects.TryGetValue(tile.Coord, out GameObject existing)
                || existing == null
                || !_tileEffectSign.TryGetValue(tile.Coord, out int existingSign)
                || existingSign != sign;

            if (needsRebuild)
            {
                if (_tileEffects.TryGetValue(tile.Coord, out GameObject old) && old != null)
                {
                    Destroy(old);
                }

                GameObject host = new GameObject("Efeito " + tile.Coord);
                host.transform.SetParent(_root, false);

                ParticleSystem particles = host.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = particles.main;
                main.startLifetime = 0.9f;
                main.startSpeed = 0.25f;
                main.startSize = 0.05f;
                main.startColor = sign > 0
                    ? new Color(1f, 0.85f, 0.2f, 0.9f)
                    : new Color(0.05f, 0.05f, 0.05f, 0.9f);
                main.gravityModifier = -0.05f;

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.rateOverTime = 5f;

                ParticleSystem.ShapeModule shape = particles.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.3f;

                _tileEffects[tile.Coord] = host;
                _tileEffectSign[tile.Coord] = sign;
            }

            float height = TerrainVisuals.HeightFor(tile.Terrain);
            _tileEffects[tile.Coord].transform.localPosition = new Vector3(
                tile.Coord.X * TerrainVisuals.TileStep,
                height + 0.3f,
                tile.Coord.Y * TerrainVisuals.TileStep);
        }

        /// <summary>
        /// Decoracao de terreno (arvore, rocha etc.): 3 instancias pequenas nos
        /// cantos da celula, nunca no meio — e o que deixa o predio caber no centro
        /// sem ficar com a arvore em cima dele. So reconstroi quando o tipo de
        /// terreno muda; o padrao de cantos e rotacao e deterministico por coord,
        /// pra nao "piscar" a cada Sync nem ficar identico em toda celula.
        /// </summary>
        private void SyncTerrainProp(Tile tile, bool ghost)
        {
            GameObject prefab = _terrainVisuals != null && !ghost && !tile.Destroyed
                ? _terrainVisuals.PropFor(tile.Terrain)
                : null;

            if (prefab == null)
            {
                DestroyTerrainProps(tile.Coord);
                return;
            }

            bool needsRebuild = !_terrainProps.TryGetValue(tile.Coord, out List<GameObject> existing)
                || existing == null || existing.Count == 0 || existing[0] == null
                || !_terrainPropKind.TryGetValue(tile.Coord, out TerrainType kind)
                || kind != tile.Terrain;

            if (!needsRebuild)
            {
                return;
            }

            DestroyTerrainProps(tile.Coord);

            float height = TerrainVisuals.HeightFor(tile.Terrain);
            Vector3 center = new Vector3(
                tile.Coord.X * TerrainVisuals.TileStep, height, tile.Coord.Y * TerrainVisuals.TileStep);

            int seed = tile.Coord.X * 73856093 ^ tile.Coord.Y * 19349663;
            System.Random rng = new System.Random(seed);
            int skip = rng.Next(PropCorners.Length);

            List<GameObject> instances = new List<GameObject>();
            for (int i = 0; i < PropCorners.Length; i++)
            {
                if (i == skip)
                {
                    // Um canto fica livre de proposito: simetria perfeita nos 4
                    // cantos lê como padrão repetido, não como decoração natural.
                    continue;
                }

                GameObject instance = Instantiate(prefab, _root);
                instance.name = "Prop " + tile.Coord + " " + i;
                instance.transform.localPosition = center + new Vector3(PropCorners[i].x, 0f, PropCorners[i].y);
                instance.transform.localRotation = Quaternion.Euler(0f, rng.Next(360), 0f);

                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
                Bounds bounds = default;
                for (int r = 0; r < renderers.Length; r++)
                {
                    if (r == 0)
                    {
                        bounds = renderers[r].bounds;
                    }
                    else
                    {
                        bounds.Encapsulate(renderers[r].bounds);
                    }
                }

                instance.transform.localScale = Vector3.one *
                    (renderers.Length > 0 ? TerrainVisuals.FitScale(bounds, PropFootprint) : 1f);

                foreach (Collider collider in instance.GetComponentsInChildren<Collider>())
                {
                    Destroy(collider);
                }

                instances.Add(instance);
            }

            _terrainProps[tile.Coord] = instances;
            _terrainPropKind[tile.Coord] = tile.Terrain;
        }

        private void DestroyTerrainProps(Coord coord)
        {
            if (_terrainProps.TryGetValue(coord, out List<GameObject> stale))
            {
                for (int i = 0; i < stale.Count; i++)
                {
                    if (stale[i] != null)
                    {
                        Destroy(stale[i]);
                    }
                }
            }

            _terrainProps.Remove(coord);
            _terrainPropKind.Remove(coord);
        }

        /// <summary>
        /// Uma faixa entre cada par de celulas possuidas vizinhas — estrada, ou ponte
        /// se a conexao cruzar rio. Sem prefab atribuido, vira uma faixa procedural
        /// (mesmo padrao de fallback do resto do HUD). Roda uma vez por Sync, nao por
        /// celula, porque cada conexao pertence a duas celulas ao mesmo tempo.
        /// </summary>
        private void SyncConnectors()
        {
            HashSet<(Coord, Coord)> alive = new HashSet<(Coord, Coord)>();

            foreach (Tile tile in _run.Grid.OwnedTilesOrdered())
            {
                if (tile.Destroyed)
                {
                    continue;
                }

                foreach (Coord neighbor in tile.Coord.Neighbors())
                {
                    // So desenha uma vez por par: a ordem canonica evita desenhar
                    // A-B e B-A como duas conexoes separadas.
                    bool canonical = neighbor.X > tile.Coord.X
                        || (neighbor.X == tile.Coord.X && neighbor.Y > tile.Coord.Y);
                    if (!canonical)
                    {
                        continue;
                    }

                    Tile neighborTile = _run.Grid.TileAt(neighbor);
                    if (!neighborTile.Owned || neighborTile.Destroyed)
                    {
                        continue;
                    }

                    (Coord, Coord) key = (tile.Coord, neighbor);
                    alive.Add(key);
                    SyncConnector(key, tile, neighborTile);
                }
            }

            List<(Coord, Coord)> toRemove = new List<(Coord, Coord)>();
            foreach (KeyValuePair<(Coord, Coord), GameObject> pair in _connectors)
            {
                if (!alive.Contains(pair.Key))
                {
                    toRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                if (_connectors.TryGetValue(toRemove[i], out GameObject stale) && stale != null)
                {
                    Destroy(stale);
                }

                _connectors.Remove(toRemove[i]);
            }
        }

        private void SyncConnector((Coord, Coord) key, Tile a, Tile b)
        {
            bool isWater = a.Terrain == TerrainType.River || b.Terrain == TerrainType.River;
            GameObject prefab = _terrainVisuals != null
                ? (isWater ? _terrainVisuals.BridgeProp : _terrainVisuals.RoadProp)
                : null;

            bool alongX = a.Coord.X != b.Coord.X;

            if (!_connectors.TryGetValue(key, out GameObject connector) || connector == null)
            {
                if (prefab != null)
                {
                    connector = Instantiate(prefab, _root);
                }
                else
                {
                    connector = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    connector.GetComponent<Renderer>().sharedMaterial = isWater ? _bridgeMaterial : _roadMaterial;
                }

                connector.name = "Conector " + a.Coord + "-" + b.Coord;

                foreach (Collider collider in connector.GetComponentsInChildren<Collider>())
                {
                    Destroy(collider);
                }

                if (prefab == null)
                {
                    connector.transform.localScale = alongX
                        ? new Vector3(TerrainVisuals.TileStep * 0.5f, 0.06f, 0.16f)
                        : new Vector3(0.16f, 0.06f, TerrainVisuals.TileStep * 0.5f);
                }

                _connectors[key] = connector;
            }

            float heightA = TerrainVisuals.HeightFor(a.Terrain);
            float heightB = TerrainVisuals.HeightFor(b.Terrain);
            Vector3 posA = new Vector3(a.Coord.X * TerrainVisuals.TileStep, heightA, a.Coord.Y * TerrainVisuals.TileStep);
            Vector3 posB = new Vector3(b.Coord.X * TerrainVisuals.TileStep, heightB, b.Coord.Y * TerrainVisuals.TileStep);

            connector.transform.localPosition = (posA + posB) * 0.5f + Vector3.up * 0.03f;
            connector.transform.localRotation = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
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

            if (_idle.Contains(tile.Coord))
            {
                return Color.Lerp(TerrainVisuals.ColorFor(tile.Terrain), TerrainVisuals.IdleTint, 0.65f);
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
                _buildingMarkerKind.Remove(tile.Coord);
                _buildingOriginalColors.Remove(tile.Coord);
                _buildingBaseScale.Remove(tile.Coord);
                return;
            }

            bool isHall = tile.Coord == _run.Grid.HallCoord;
            bool hasPrefab = _buildingPrefabs.TryGetValue(tile.Building.Id, out GameObject prefab) && prefab != null;

            // Chave de identidade do marcador: "__cube__" para o fallback, ou o Id do
            // edificio para o prefab. Muda quando demolir e construir outra coisa na
            // mesma celula, e forca reconstruir em vez de reaproveitar o marcador.
            string kind = hasPrefab ? tile.Building.Id : "__cube__";

            bool needsRebuild = !_buildings.TryGetValue(tile.Coord, out GameObject marker)
                || marker == null
                || !_buildingMarkerKind.TryGetValue(tile.Coord, out string existingKind)
                || existingKind != kind;

            if (needsRebuild)
            {
                if (_buildings.TryGetValue(tile.Coord, out GameObject old) && old != null)
                {
                    Destroy(old);
                }

                marker = hasPrefab
                    ? Instantiate(prefab, _root)
                    : GameObject.CreatePrimitive(PrimitiveType.Cube);

                marker.name = "Building " + tile.Coord;
                marker.transform.SetParent(_root, false);

                // O marcador do edificio nao deve roubar o clique da celula.
                foreach (Collider collider in marker.GetComponentsInChildren<Collider>())
                {
                    Destroy(collider);
                }

                if (hasPrefab)
                {
                    // Guarda a cor original de cada renderer antes de qualquer tinte,
                    // porque ler sharedMaterial depois da primeira instancia devolve
                    // a copia ja tingida, nao o original do prefab.
                    Renderer[] renderers = marker.GetComponentsInChildren<Renderer>();
                    Color[] originals = new Color[renderers.Length];
                    Bounds bounds = default;

                    for (int r = 0; r < renderers.Length; r++)
                    {
                        originals[r] = TerrainVisuals.GetColor(renderers[r].sharedMaterial);

                        if (r == 0)
                        {
                            bounds = renderers[r].bounds;
                        }
                        else
                        {
                            bounds.Encapsulate(renderers[r].bounds);
                        }
                    }

                    _buildingOriginalColors[tile.Coord] = originals;

                    // Escala ainda em 1 aqui: o bounds do renderer reflete o tamanho
                    // real do import, antes de qualquer ajuste — e o que permite
                    // calcular o fator certo pra caber no tile, seja qual for a
                    // escala nativa do pacote de origem.
                    _buildingBaseScale[tile.Coord] = renderers.Length > 0
                        ? TerrainVisuals.FitScale(bounds, BuildingFootprint)
                        : 1f;
                }

                _buildings[tile.Coord] = marker;
                _buildingMarkerKind[tile.Coord] = kind;
            }

            bool idle = _idle.Contains(tile.Coord);
            float terrainHeight = TerrainVisuals.HeightFor(tile.Terrain);

            if (hasPrefab)
            {
                marker.transform.localPosition = new Vector3(
                    tile.Coord.X * TerrainVisuals.TileStep, terrainHeight, tile.Coord.Y * TerrainVisuals.TileStep);

                float baseScale = _buildingBaseScale.TryGetValue(tile.Coord, out float stored) ? stored : 1f;
                marker.transform.localScale = Vector3.one * baseScale * (idle ? 0.7f : 1f);

                Renderer[] renderers = marker.GetComponentsInChildren<Renderer>();
                Color[] originals = _buildingOriginalColors.TryGetValue(tile.Coord, out Color[] storedColors)
                    ? storedColors
                    : null;

                for (int r = 0; r < renderers.Length && originals != null && r < originals.Length; r++)
                {
                    Color final = idle ? Color.Lerp(originals[r], TerrainVisuals.IdleMarkerColor, 0.6f) : originals[r];
                    TerrainVisuals.SetColor(renderers[r].material, final);
                }

                return;
            }

            float markerHeight = isHall ? 0.55f : 0.35f;
            float width = isHall ? 0.5f : 0.4f;

            marker.transform.localScale = new Vector3(width, markerHeight, width);
            marker.transform.localPosition = new Vector3(
                tile.Coord.X * TerrainVisuals.TileStep,
                terrainHeight + markerHeight * 0.5f,
                tile.Coord.Y * TerrainVisuals.TileStep);

            // O edificio ocioso encolhe e escurece: a silhueta do reino passa a
            // mostrar quanto dele parou, sem exigir leitura de painel.
            if (idle)
            {
                marker.transform.localScale = new Vector3(width * 0.6f, markerHeight * 0.5f, width * 0.6f);
                marker.transform.localPosition = new Vector3(
                    tile.Coord.X * TerrainVisuals.TileStep,
                    terrainHeight + markerHeight * 0.25f,
                    tile.Coord.Y * TerrainVisuals.TileStep);
            }

            marker.GetComponent<Renderer>().sharedMaterial =
                idle ? _idleMaterial : (isHall ? _hallMaterial : _buildingMaterial);
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

                DestroyTerrainProps(coord);

                if (_tileEffects.TryGetValue(coord, out GameObject effect) && effect != null)
                {
                    Destroy(effect);
                }

                _tiles.Remove(coord);
                _buildings.Remove(coord);
                _materials.Remove(coord);
                _buildingMarkerKind.Remove(coord);
                _buildingOriginalColors.Remove(coord);
                _buildingBaseScale.Remove(coord);
                _tileEffects.Remove(coord);
                _tileEffectSign.Remove(coord);
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
