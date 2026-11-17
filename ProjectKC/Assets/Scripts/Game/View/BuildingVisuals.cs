using System.Collections.Generic;
using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Silhueta de cada edificio, montada com cubos. Continua sendo arte provisoria,
    /// mas ja cumpre o que importa: olhar o tabuleiro e saber o que e fazenda, o que
    /// e torre e o que e casa, sem selecionar nada.
    /// </summary>
    public static class BuildingVisuals
    {
        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();

        private static readonly Color Wood = new Color(0.50f, 0.34f, 0.20f);
        private static readonly Color DarkWood = new Color(0.34f, 0.23f, 0.14f);
        private static readonly Color Stone = new Color(0.66f, 0.67f, 0.70f);
        private static readonly Color DarkStone = new Color(0.42f, 0.43f, 0.47f);
        private static readonly Color Plaster = new Color(0.90f, 0.85f, 0.72f);
        private static readonly Color Roof = new Color(0.72f, 0.28f, 0.22f);
        private static readonly Color Wheat = new Color(0.90f, 0.78f, 0.36f);
        private static readonly Color Cloth = new Color(0.56f, 0.36f, 0.72f);
        private static readonly Color Banner = new Color(0.86f, 0.22f, 0.20f);
        private static readonly Color Leaf = new Color(0.16f, 0.36f, 0.20f);
        private static readonly Color Ash = new Color(0.16f, 0.13f, 0.12f);

        private static string _raceId = StarterContent.HumansId;

        /// <summary>Raca que esta jogando: decide qual conjunto de estruturas e montado.</summary>
        public static void SetRace(string raceId)
        {
            _raceId = raceId ?? StarterContent.HumansId;
        }

        public static Material MaterialFor(Color color)
        {
            if (!Materials.TryGetValue(color, out Material material) || material == null)
            {
                material = TerrainVisuals.CreateMaterial(color);
                Materials[color] = material;
            }

            return material;
        }

        /// <summary>
        /// Cria o objeto do edificio com a base no chao do proprio objeto (y = 0).
        /// Ocioso sai acinzentado e mais baixo: a silhueta do reino mostra quanto
        /// dele parou.
        /// </summary>
        public static GameObject Create(string buildingId, bool idle)
        {
            GameObject root = new GameObject("Building " + buildingId);

            if ((_raceId == StarterContent.OrcsId && BuildOrc(root, buildingId, idle)) ||
                (_raceId == StarterContent.ElvesId && BuildElf(root, buildingId, idle)))
            {
                return root;
            }

            switch (buildingId)
            {
                case StarterContent.Hall:
                    Part(root, 0f, 0f, 0.56f, 0.40f, 0.56f, Plaster, idle);
                    Part(root, 0f, 0.40f, 0.44f, 0.20f, 0.44f, Roof, idle);
                    Part(root, 0.18f, 0.60f, 0.04f, 0.34f, 0.04f, DarkWood, idle);
                    Part(root, 0.25f, 0.84f, 0.14f, 0.09f, 0.03f, Banner, idle);
                    break;

                case StarterContent.Farm:
                    Part(root, 0f, 0f, 0.84f, 0.05f, 0.84f, Wheat, idle);
                    Part(root, -0.26f, 0.05f, 0.24f, 0.22f, 0.24f, Roof, idle);
                    Part(root, 0.12f, 0.05f, 0.5f, 0.05f, 0.08f, new Color(0.74f, 0.62f, 0.26f), idle, 0.2f);
                    Part(root, 0.12f, 0.05f, 0.5f, 0.05f, 0.08f, new Color(0.74f, 0.62f, 0.26f), idle, -0.15f);
                    break;

                case StarterContent.Sawmill:
                    Part(root, -0.08f, 0f, 0.46f, 0.28f, 0.36f, Wood, idle);
                    Part(root, -0.08f, 0.28f, 0.50f, 0.08f, 0.40f, DarkWood, idle);
                    Part(root, 0.28f, 0f, 0.14f, 0.10f, 0.44f, new Color(0.66f, 0.48f, 0.28f), idle);
                    break;

                case StarterContent.Quarry:
                    Part(root, 0f, 0f, 0.44f, 0.22f, 0.44f, DarkStone, idle);
                    Part(root, 0.26f, 0f, 0.18f, 0.16f, 0.18f, Stone, idle, 0.2f);
                    Part(root, -0.24f, 0f, 0.14f, 0.12f, 0.16f, Stone, idle, -0.24f);
                    Part(root, 0f, 0.22f, 0.10f, 0.26f, 0.10f, DarkWood, idle);
                    break;

                case StarterContent.Docks:
                    Part(root, 0f, 0f, 0.80f, 0.06f, 0.28f, Wood, idle);
                    Part(root, -0.22f, 0.06f, 0.26f, 0.22f, 0.24f, Plaster, idle);
                    Part(root, -0.22f, 0.28f, 0.30f, 0.08f, 0.28f, DarkWood, idle);
                    break;

                case StarterContent.House:
                    Part(root, -0.18f, 0f, 0.28f, 0.22f, 0.28f, Plaster, idle, -0.14f);
                    Part(root, -0.18f, 0.22f, 0.32f, 0.10f, 0.32f, Roof, idle, -0.14f);
                    Part(root, 0.18f, 0f, 0.26f, 0.18f, 0.26f, Plaster, idle, 0.16f);
                    Part(root, 0.18f, 0.18f, 0.30f, 0.10f, 0.30f, Roof, idle, 0.16f);
                    break;

                case StarterContent.Outpost:
                    Part(root, 0f, 0f, 0.70f, 0.20f, 0.10f, DarkWood, idle, 0.30f);
                    Part(root, 0f, 0f, 0.70f, 0.20f, 0.10f, DarkWood, idle, -0.30f);
                    Part(root, 0.30f, 0f, 0.10f, 0.20f, 0.70f, DarkWood, idle);
                    Part(root, -0.30f, 0f, 0.10f, 0.20f, 0.70f, DarkWood, idle);
                    Part(root, 0f, 0f, 0.18f, 0.36f, 0.18f, Wood, idle);
                    break;

                case StarterContent.Watchtower:
                    Part(root, 0f, 0f, 0.28f, 0.70f, 0.28f, Stone, idle);
                    Part(root, 0f, 0.70f, 0.38f, 0.12f, 0.38f, DarkStone, idle);
                    Part(root, 0f, 0.82f, 0.05f, 0.18f, 0.05f, DarkWood, idle);
                    Part(root, 0.07f, 0.92f, 0.12f, 0.07f, 0.03f, Banner, idle);
                    break;

                case StarterContent.Wall:
                    Part(root, 0f, 0f, 0.92f, 0.28f, 0.22f, Stone, idle);
                    Part(root, -0.32f, 0.28f, 0.16f, 0.10f, 0.22f, DarkStone, idle);
                    Part(root, 0f, 0.28f, 0.16f, 0.10f, 0.22f, DarkStone, idle);
                    Part(root, 0.32f, 0.28f, 0.16f, 0.10f, 0.22f, DarkStone, idle);
                    break;

                case StarterContent.Market:
                    Part(root, 0f, 0f, 0.50f, 0.18f, 0.50f, Wood, idle);
                    Part(root, 0f, 0.30f, 0.66f, 0.05f, 0.66f, Cloth, idle);
                    Part(root, 0.26f, 0.18f, 0.04f, 0.12f, 0.04f, DarkWood, idle, 0.26f);
                    Part(root, -0.26f, 0.18f, 0.04f, 0.12f, 0.04f, DarkWood, idle, -0.26f);
                    break;

                case StarterContent.Barracks:
                    Part(root, 0f, 0f, 0.74f, 0.30f, 0.50f, DarkStone, idle);
                    Part(root, 0f, 0.30f, 0.78f, 0.08f, 0.54f, Roof, idle);
                    Part(root, 0.30f, 0.38f, 0.05f, 0.30f, 0.05f, DarkWood, idle);
                    Part(root, 0.38f, 0.58f, 0.14f, 0.09f, 0.03f, Banner, idle);
                    break;

                default:
                    Part(root, 0f, 0f, 0.42f, 0.34f, 0.42f, TerrainVisuals.BuildingColor, idle);
                    break;
            }

            if (idle)
            {
                root.transform.localScale = new Vector3(1f, 0.7f, 1f);
            }

            return root;
        }

        /// <summary>Enfeite de terreno vazio: arvores, pedras, colunas quebradas ou entulho.</summary>
        public static GameObject CreateDecor(TerrainType terrain, bool destroyed, Coord coord)
        {
            // Variacao estavel por coordenada: o mesmo quadrado sempre parece igual.
            float jitter = ((coord.X * 73856093) ^ (coord.Y * 19349663)) % 7 / 7f * 0.08f;
            GameObject root = new GameObject("Decor " + coord);
            _terrainPass = true;

            if (destroyed)
            {
                Part(root, -0.2f, 0f, 0.22f, 0.10f, 0.20f, Ash, false, 0.15f);
                Part(root, 0.18f, 0f, 0.26f, 0.14f, 0.18f, Ash, false, -0.1f);
                Part(root, 0.02f, 0f, 0.12f, 0.22f, 0.10f, new Color(0.24f, 0.18f, 0.15f), false, 0.26f);
                _terrainPass = false;
                return root;
            }

            switch (terrain)
            {
                case TerrainType.Forest:
                    Tree(root, -0.24f + jitter, 0.22f, 0.36f);
                    Tree(root, 0.20f, -0.18f + jitter, 0.44f);
                    Tree(root, 0.16f - jitter, 0.26f, 0.30f);
                    Tree(root, -0.20f, -0.24f, 0.34f);
                    break;

                case TerrainType.Mine:
                    Part(root, -0.18f + jitter, 0f, 0.26f, 0.20f, 0.24f, Stone, false, 0.14f);
                    Part(root, 0.20f, 0f, 0.20f, 0.30f, 0.20f, DarkStone, false, -0.16f + jitter);
                    Part(root, 0.02f, 0f, 0.14f, 0.12f, 0.14f, Stone, false, -0.30f);
                    break;

                case TerrainType.Ruin:
                    Part(root, -0.22f, 0f, 0.12f, 0.34f - jitter, 0.12f, Plaster, false, 0.2f);
                    Part(root, 0.22f, 0f, 0.12f, 0.18f + jitter, 0.12f, Plaster, false, 0.2f);
                    Part(root, 0f, 0f, 0.40f, 0.07f, 0.12f, DarkStone, false, -0.22f);
                    break;

                case TerrainType.River:
                    // Vitorias-regias e uma pedra no meio da corrente.
                    Part(root, -0.18f + jitter, 0.02f, 0.14f, 0.015f, 0.14f, new Color(0.30f, 0.62f, 0.34f), false, 0.12f);
                    Part(root, 0.14f, 0.02f, 0.10f, 0.015f, 0.10f, new Color(0.36f, 0.68f, 0.38f), false, -0.16f + jitter);
                    Part(root, 0.16f, 0.02f, 0.03f, 0.03f, 0.03f, new Color(0.96f, 0.80f, 0.86f), false, -0.16f + jitter);
                    Part(root, 0.05f, 0f, 0.14f, 0.08f, 0.12f, Stone, false, 0.24f);
                    break;

                default:
                    // Planicie vazia: capim alto, uma pedra e um arbusto.
                    Part(root, -0.14f + jitter, 0f, 0.05f, 0.14f, 0.05f, new Color(0.40f, 0.62f, 0.26f), false, 0.10f);
                    Part(root, -0.08f, 0f, 0.05f, 0.18f, 0.05f, new Color(0.46f, 0.68f, 0.30f), false, 0.16f - jitter);
                    Part(root, -0.02f + jitter, 0f, 0.05f, 0.12f, 0.05f, new Color(0.40f, 0.62f, 0.26f), false, 0.08f);
                    Part(root, 0.16f, 0f, 0.16f, 0.09f, 0.13f, Stone, false, -0.14f + jitter);
                    Part(root, 0.10f - jitter, 0f, 0.16f, 0.11f, 0.16f, new Color(0.30f, 0.52f, 0.24f), false, 0.22f);
                    break;
            }

            _terrainPass = false;
            return root;
        }

        // --- Superficie do terreno ---

        private static bool _terrainPass;

        private static float Hash(Coord coord, int salt)
        {
            int h = (coord.X * 73856093) ^ (coord.Y * 19349663) ^ (salt * 83492791);
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }

        /// <summary>Posicao perto da borda da celula, onde um edificio no centro nao cobre.</summary>
        private static Vector2 Edge(Coord coord, int salt)
        {
            float along = Hash(coord, salt) * 0.84f - 0.42f;
            float side = 0.36f + Hash(coord, salt + 50) * 0.08f;
            switch ((int)(Hash(coord, salt + 100) * 4f) % 4)
            {
                case 0:
                    return new Vector2(along, side);
                case 1:
                    return new Vector2(along, -side);
                case 2:
                    return new Vector2(side, along);
                default:
                    return new Vector2(-side, along);
            }
        }

        /// <summary>
        /// O que faz a celula parecer o terreno que e, e nao so um bloco pintado:
        /// uma base de terra, um tampo com a textura do terreno e miudezas na borda
        /// (capim, seixos, juncos, lajes). Fica com ou sem edificio em cima.
        /// </summary>
        public static GameObject CreateSurface(TerrainType terrain, Coord coord, float tileHeight)
        {
            GameObject root = new GameObject("Superficie " + coord);
            _terrainPass = true;

            // Base de terra aparecendo por baixo do bloco.
            Part(root, 0f, -tileHeight, 1.05f, 0.07f, 1.05f, new Color(0.30f, 0.23f, 0.17f), false);

            switch (terrain)
            {
                case TerrainType.Forest:
                    Part(root, 0f, 0f, 0.82f, 0.015f, 0.82f, new Color(0.17f, 0.38f, 0.22f), false);
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 at = Edge(coord, i);
                        Part(root, at.x, 0.015f, 0.13f, 0.08f + Hash(coord, i + 9) * 0.05f, 0.13f,
                            new Color(0.14f, 0.32f, 0.18f), false, at.y);
                    }

                    for (int i = 4; i < 8; i++)
                    {
                        Vector2 at = Edge(coord, i);
                        Part(root, at.x, 0.015f, 0.05f, 0.012f, 0.05f, new Color(0.78f, 0.52f, 0.20f), false, at.y);
                    }

                    break;

                case TerrainType.Mine:
                    Part(root, 0f, 0f, 0.82f, 0.015f, 0.82f, new Color(0.52f, 0.51f, 0.56f), false);
                    for (int i = 0; i < 6; i++)
                    {
                        Vector2 at = Edge(coord, i);
                        float size = 0.06f + Hash(coord, i + 9) * 0.06f;
                        Part(root, at.x, 0.015f, size, size * 0.7f, size, i % 2 == 0 ? Stone : DarkStone, false, at.y);
                    }

                    for (int i = 6; i < 8; i++)
                    {
                        Vector2 at = Edge(coord, i);
                        Part(root, at.x, 0.015f, 0.045f, 0.045f, 0.045f, new Color(0.95f, 0.76f, 0.24f), false, at.y);
                    }

                    break;

                case TerrainType.River:
                    // Margens de areia, agua mais clara no meio e ondulacoes.
                    Part(root, 0f, 0f, 0.84f, 0.03f, 0.10f, new Color(0.80f, 0.72f, 0.50f), false, 0.37f);
                    Part(root, 0f, 0f, 0.84f, 0.03f, 0.10f, new Color(0.80f, 0.72f, 0.50f), false, -0.37f);
                    Part(root, 0f, 0f, 0.84f, 0.012f, 0.64f, new Color(0.36f, 0.66f, 0.88f), false);
                    for (int i = 0; i < 3; i++)
                    {
                        float x = Hash(coord, i) * 0.6f - 0.3f;
                        float z = -0.24f + i * 0.22f;
                        Part(root, x, 0.012f, 0.26f, 0.006f, 0.035f, new Color(0.72f, 0.88f, 0.98f), false, z);
                    }

                    for (int i = 0; i < 3; i++)
                    {
                        float x = -0.40f + i * 0.06f;
                        float height = 0.14f + Hash(coord, i + 20) * 0.08f;
                        Part(root, x, 0.03f, 0.025f, height, 0.025f, new Color(0.36f, 0.56f, 0.24f), false, 0.37f);
                        Part(root, x, 0.03f + height, 0.035f, 0.05f, 0.035f, new Color(0.42f, 0.28f, 0.16f), false, 0.37f);
                    }

                    break;

                case TerrainType.Ruin:
                    // Lajes antigas, cada uma de um cinza, com mato nas frestas.
                    for (int i = 0; i < 4; i++)
                    {
                        float x = (i % 2 == 0 ? -0.20f : 0.20f);
                        float z = (i < 2 ? -0.20f : 0.20f);
                        float tone = 0.50f + Hash(coord, i) * 0.14f;
                        Part(root, x, 0f, 0.36f, 0.02f + Hash(coord, i + 5) * 0.015f, 0.36f,
                            new Color(tone, tone * 0.95f, tone * 0.88f), false, z);
                    }

                    for (int i = 0; i < 3; i++)
                    {
                        Vector2 at = Edge(coord, i);
                        Part(root, at.x, 0.02f, 0.05f, 0.07f, 0.05f, new Color(0.40f, 0.52f, 0.26f), false, at.y);
                    }

                    break;

                default:
                    Part(root, 0f, 0f, 0.82f, 0.015f, 0.82f, new Color(0.56f, 0.76f, 0.38f), false);
                    for (int i = 0; i < 7; i++)
                    {
                        Vector2 at = Edge(coord, i);
                        Part(root, at.x, 0.015f, 0.045f, 0.05f + Hash(coord, i + 9) * 0.05f, 0.045f,
                            i % 2 == 0 ? new Color(0.38f, 0.60f, 0.24f) : new Color(0.46f, 0.70f, 0.30f), false, at.y);
                    }

                    for (int i = 7; i < 10; i++)
                    {
                        Vector2 at = Edge(coord, i);
                        Part(root, at.x, 0.015f, 0.04f, 0.035f, 0.04f,
                            i % 2 == 0 ? new Color(0.98f, 0.86f, 0.30f) : new Color(0.96f, 0.96f, 0.92f), false, at.y);
                    }

                    break;
            }

            _terrainPass = false;
            return root;
        }

        private static void Tree(GameObject root, float x, float z, float height)
        {
            Part(root, x, 0f, 0.05f, height * 0.4f, 0.05f, DarkWood, false, z);
            Part(root, x, height * 0.4f, 0.20f, height * 0.6f, 0.20f, Leaf, false, z);
        }

        // --- Orcs: couro, osso e ferro. Baixo, largo e cheio de espinho. ---

        private static readonly Color Hide = new Color(0.47f, 0.31f, 0.20f);
        private static readonly Color Bone = new Color(0.88f, 0.84f, 0.72f);
        private static readonly Color Iron = new Color(0.24f, 0.24f, 0.27f);
        private static readonly Color Blood = new Color(0.62f, 0.12f, 0.10f);
        private static readonly Color Mud = new Color(0.34f, 0.25f, 0.18f);

        private static void Tent(GameObject root, float x, float z, float size, bool idle)
        {
            Part(root, x, 0f, size, size * 0.45f, size, Hide, idle, z);
            Part(root, x, size * 0.45f, size * 0.68f, size * 0.38f, size * 0.68f, Hide, idle, z);
            Part(root, x, size * 0.83f, size * 0.34f, size * 0.30f, size * 0.34f, DarkWood, idle, z);
        }

        private static bool BuildOrc(GameObject root, string buildingId, bool idle)
        {
            switch (buildingId)
            {
                case StarterContent.Hall:
                    Part(root, 0f, 0f, 0.68f, 0.30f, 0.58f, Hide, idle);
                    Part(root, 0f, 0.30f, 0.76f, 0.09f, 0.66f, Iron, idle);
                    Part(root, 0.34f, 0f, 0.07f, 0.56f, 0.07f, Bone, idle, 0.29f);
                    Part(root, -0.34f, 0f, 0.07f, 0.56f, 0.07f, Bone, idle, 0.29f);
                    Part(root, 0.34f, 0f, 0.07f, 0.56f, 0.07f, Bone, idle, -0.29f);
                    Part(root, -0.34f, 0f, 0.07f, 0.56f, 0.07f, Bone, idle, -0.29f);
                    Part(root, 0f, 0.39f, 0.05f, 0.40f, 0.05f, DarkWood, idle);
                    Part(root, 0.09f, 0.62f, 0.18f, 0.14f, 0.03f, Blood, idle);
                    return true;

                case StarterContent.Farm:
                    Part(root, 0f, 0f, 0.84f, 0.04f, 0.84f, Mud, idle);
                    Part(root, 0f, 0.04f, 0.84f, 0.10f, 0.04f, DarkWood, idle, 0.40f);
                    Part(root, 0f, 0.04f, 0.84f, 0.10f, 0.04f, DarkWood, idle, -0.40f);
                    Part(root, 0.40f, 0.04f, 0.04f, 0.10f, 0.84f, DarkWood, idle);
                    Part(root, -0.40f, 0.04f, 0.04f, 0.10f, 0.84f, DarkWood, idle);
                    Part(root, 0.12f, 0.04f, 0.20f, 0.12f, 0.14f, new Color(0.80f, 0.56f, 0.52f), idle, 0.10f);
                    Part(root, -0.14f, 0.04f, 0.16f, 0.10f, 0.12f, new Color(0.80f, 0.56f, 0.52f), idle, -0.16f);
                    return true;

                case StarterContent.Sawmill:
                    Part(root, -0.10f, 0f, 0.56f, 0.12f, 0.14f, Wood, idle, 0.12f);
                    Part(root, -0.10f, 0f, 0.56f, 0.12f, 0.14f, Wood, idle, -0.06f);
                    Part(root, -0.10f, 0.12f, 0.56f, 0.12f, 0.14f, DarkWood, idle, 0.03f);
                    Part(root, 0.30f, 0f, 0.16f, 0.14f, 0.16f, DarkWood, idle, -0.24f);
                    Part(root, 0.30f, 0.14f, 0.04f, 0.22f, 0.12f, Iron, idle, -0.24f);
                    return true;

                case StarterContent.Quarry:
                    Part(root, 0f, 0f, 0.46f, 0.20f, 0.46f, DarkStone, idle);
                    Part(root, 0.26f, 0f, 0.20f, 0.30f, 0.18f, Iron, idle, 0.20f);
                    Part(root, -0.26f, 0f, 0.06f, 0.36f, 0.06f, Bone, idle, -0.24f);
                    return true;

                case StarterContent.House:
                    Tent(root, -0.18f, -0.14f, 0.34f, idle);
                    Tent(root, 0.20f, 0.16f, 0.28f, idle);
                    return true;

                case StarterContent.Outpost:
                    Part(root, 0f, 0f, 0.70f, 0.18f, 0.08f, DarkWood, idle, 0.31f);
                    Part(root, 0f, 0f, 0.70f, 0.18f, 0.08f, DarkWood, idle, -0.31f);
                    Part(root, 0.31f, 0f, 0.08f, 0.18f, 0.70f, DarkWood, idle);
                    Part(root, -0.31f, 0f, 0.08f, 0.18f, 0.70f, DarkWood, idle);
                    Part(root, 0.31f, 0f, 0.07f, 0.44f, 0.07f, Bone, idle, 0.31f);
                    Part(root, -0.31f, 0f, 0.07f, 0.44f, 0.07f, Bone, idle, 0.31f);
                    Part(root, 0.31f, 0f, 0.07f, 0.44f, 0.07f, Bone, idle, -0.31f);
                    Part(root, -0.31f, 0f, 0.07f, 0.44f, 0.07f, Bone, idle, -0.31f);
                    return true;

                case StarterContent.Watchtower:
                    Part(root, 0.13f, 0f, 0.06f, 0.62f, 0.06f, DarkWood, idle, 0.13f);
                    Part(root, -0.13f, 0f, 0.06f, 0.62f, 0.06f, DarkWood, idle, 0.13f);
                    Part(root, 0.13f, 0f, 0.06f, 0.62f, 0.06f, DarkWood, idle, -0.13f);
                    Part(root, -0.13f, 0f, 0.06f, 0.62f, 0.06f, DarkWood, idle, -0.13f);
                    Part(root, 0f, 0.62f, 0.42f, 0.08f, 0.42f, Hide, idle);
                    Part(root, 0f, 0.70f, 0.30f, 0.14f, 0.30f, Iron, idle);
                    Part(root, 0.12f, 0.84f, 0.16f, 0.12f, 0.03f, Blood, idle);
                    return true;

                case StarterContent.Wall:
                    Part(root, 0f, 0f, 0.92f, 0.30f, 0.20f, DarkWood, idle);
                    Part(root, 0f, 0.12f, 0.94f, 0.06f, 0.22f, Iron, idle);
                    Part(root, -0.34f, 0.30f, 0.06f, 0.18f, 0.06f, Bone, idle);
                    Part(root, -0.11f, 0.30f, 0.06f, 0.18f, 0.06f, Bone, idle);
                    Part(root, 0.11f, 0.30f, 0.06f, 0.18f, 0.06f, Bone, idle);
                    Part(root, 0.34f, 0.30f, 0.06f, 0.18f, 0.06f, Bone, idle);
                    return true;

                case StarterContent.Barracks:
                    Part(root, 0f, 0f, 0.78f, 0.26f, 0.50f, Hide, idle);
                    Part(root, 0f, 0.26f, 0.84f, 0.08f, 0.56f, Iron, idle);
                    Part(root, 0.36f, 0f, 0.07f, 0.52f, 0.07f, Bone, idle, 0.26f);
                    Part(root, -0.36f, 0f, 0.07f, 0.52f, 0.07f, Bone, idle, 0.26f);
                    Part(root, 0f, 0.34f, 0.20f, 0.14f, 0.03f, Blood, idle, 0.29f);
                    return true;

                default:
                    return false;
            }
        }

        // --- Elfos: tronco, folha e prata. Fino, alto e crescido em vez de erguido. ---

        private static readonly Color Bark = new Color(0.42f, 0.32f, 0.22f);
        private static readonly Color Canopy = new Color(0.22f, 0.58f, 0.34f);
        private static readonly Color LightLeaf = new Color(0.52f, 0.80f, 0.44f);
        private static readonly Color Silver = new Color(0.86f, 0.90f, 0.93f);
        private static readonly Color Crystal = new Color(0.42f, 0.86f, 0.92f);
        private static readonly Color Petal = new Color(0.92f, 0.68f, 0.84f);

        private static void Sapling(GameObject root, float x, float z, Color crown, bool idle)
        {
            Part(root, x, 0.04f, 0.05f, 0.14f, 0.05f, Bark, idle, z);
            Part(root, x, 0.18f, 0.20f, 0.14f, 0.20f, crown, idle, z);
        }

        private static void TreeHouse(GameObject root, float x, float z, float height, bool idle)
        {
            Part(root, x, 0f, 0.08f, height, 0.08f, Bark, idle, z);
            Part(root, x, height, 0.28f, 0.16f, 0.28f, Silver, idle, z);
            Part(root, x, height + 0.16f, 0.34f, 0.08f, 0.34f, Canopy, idle, z);
        }

        private static bool BuildElf(GameObject root, string buildingId, bool idle)
        {
            switch (buildingId)
            {
                case StarterContent.Hall:
                    Part(root, 0f, 0f, 0.24f, 0.56f, 0.24f, Bark, idle);
                    Part(root, 0f, 0.20f, 0.44f, 0.14f, 0.44f, Silver, idle);
                    Part(root, 0f, 0.56f, 0.72f, 0.20f, 0.72f, Canopy, idle);
                    Part(root, 0f, 0.76f, 0.48f, 0.16f, 0.48f, LightLeaf, idle);
                    Part(root, 0f, 0.92f, 0.10f, 0.18f, 0.10f, Crystal, idle);
                    return true;

                case StarterContent.Farm:
                    Part(root, 0f, 0f, 0.84f, 0.04f, 0.84f, new Color(0.44f, 0.66f, 0.36f), idle);
                    Sapling(root, -0.24f, -0.24f, Canopy, idle);
                    Sapling(root, 0.24f, -0.22f, Petal, idle);
                    Sapling(root, -0.22f, 0.24f, Petal, idle);
                    Sapling(root, 0.24f, 0.24f, Canopy, idle);
                    return true;

                case StarterContent.Quarry:
                    Part(root, 0f, 0f, 0.40f, 0.16f, 0.40f, Silver, idle);
                    Part(root, 0.24f, 0f, 0.16f, 0.12f, 0.16f, DarkStone, idle, 0.20f);
                    Part(root, 0f, 0.16f, 0.12f, 0.40f, 0.12f, Crystal, idle);
                    Part(root, -0.22f, 0f, 0.08f, 0.26f, 0.08f, Crystal, idle, -0.20f);
                    return true;

                case StarterContent.Docks:
                    Part(root, 0f, 0f, 0.84f, 0.05f, 0.20f, Silver, idle);
                    Part(root, -0.30f, 0.05f, 0.04f, 0.26f, 0.04f, Silver, idle, 0.08f);
                    Part(root, -0.10f, 0.05f, 0.04f, 0.26f, 0.04f, Silver, idle, -0.08f);
                    Part(root, -0.20f, 0.31f, 0.32f, 0.07f, 0.30f, Canopy, idle);
                    Part(root, 0.30f, 0.05f, 0.14f, 0.08f, 0.12f, Petal, idle);
                    return true;

                case StarterContent.House:
                    TreeHouse(root, -0.18f, -0.14f, 0.26f, idle);
                    TreeHouse(root, 0.20f, 0.16f, 0.16f, idle);
                    return true;

                case StarterContent.Outpost:
                    Part(root, 0f, 0f, 0.74f, 0.22f, 0.12f, Canopy, idle, 0.31f);
                    Part(root, 0f, 0f, 0.74f, 0.22f, 0.12f, Canopy, idle, -0.31f);
                    Part(root, 0.31f, 0f, 0.12f, 0.22f, 0.74f, Canopy, idle);
                    Part(root, -0.31f, 0f, 0.12f, 0.22f, 0.74f, Canopy, idle);
                    Part(root, 0f, 0f, 0.07f, 0.40f, 0.07f, Bark, idle);
                    Part(root, 0f, 0.40f, 0.10f, 0.12f, 0.10f, Crystal, idle);
                    return true;

                case StarterContent.Watchtower:
                    Part(root, 0f, 0f, 0.16f, 0.86f, 0.16f, Silver, idle);
                    Part(root, 0f, 0.56f, 0.34f, 0.07f, 0.34f, Canopy, idle);
                    Part(root, 0f, 0.86f, 0.24f, 0.06f, 0.24f, LightLeaf, idle);
                    Part(root, 0f, 0.92f, 0.09f, 0.16f, 0.09f, Crystal, idle);
                    return true;

                case StarterContent.Wall:
                    Part(root, 0f, 0f, 0.92f, 0.26f, 0.24f, Canopy, idle);
                    Part(root, -0.34f, 0f, 0.07f, 0.36f, 0.07f, Bark, idle);
                    Part(root, 0f, 0f, 0.07f, 0.36f, 0.07f, Bark, idle);
                    Part(root, 0.34f, 0f, 0.07f, 0.36f, 0.07f, Bark, idle);
                    Part(root, 0f, 0.26f, 0.60f, 0.06f, 0.26f, LightLeaf, idle);
                    return true;

                case StarterContent.Market:
                    Part(root, 0.24f, 0f, 0.04f, 0.30f, 0.04f, Silver, idle, 0.24f);
                    Part(root, -0.24f, 0f, 0.04f, 0.30f, 0.04f, Silver, idle, 0.24f);
                    Part(root, 0.24f, 0f, 0.04f, 0.30f, 0.04f, Silver, idle, -0.24f);
                    Part(root, -0.24f, 0f, 0.04f, 0.30f, 0.04f, Silver, idle, -0.24f);
                    Part(root, 0f, 0.30f, 0.64f, 0.06f, 0.64f, Petal, idle);
                    Part(root, 0f, 0f, 0.30f, 0.12f, 0.30f, Bark, idle);
                    return true;

                case StarterContent.Barracks:
                    Part(root, 0f, 0f, 0.72f, 0.26f, 0.46f, Silver, idle);
                    Part(root, 0f, 0.26f, 0.80f, 0.09f, 0.54f, Canopy, idle);
                    Part(root, 0.30f, 0.35f, 0.08f, 0.26f, 0.08f, Crystal, idle);
                    Part(root, -0.30f, 0.35f, 0.08f, 0.26f, 0.08f, Crystal, idle);
                    return true;

                default:
                    return false;
            }
        }

        private static void Part(
            GameObject root, float x, float baseY, float width, float height, float depth,
            Color color, bool idle, float z = 0f)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.transform.SetParent(root.transform, false);
            part.transform.localScale = new Vector3(width, height, depth);
            part.transform.localPosition = new Vector3(x, baseY + height * 0.5f, z);

            // Nenhuma peca rouba o clique da celula embaixo dela.
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            // Anoes ainda usam as pecas dos Humanos, puxadas para pedra e bronze.
            if (_raceId == StarterContent.DwarvesId && !_terrainPass)
            {
                float warm = color.r - color.b;
                color = Color.Lerp(color, warm > 0.15f ? new Color(0.80f, 0.56f, 0.20f) : new Color(0.52f, 0.54f, 0.60f), 0.55f);
            }

            Color final = idle ? Color.Lerp(color, new Color(0.42f, 0.40f, 0.38f), 0.75f) : color;
            part.GetComponent<Renderer>().sharedMaterial = MaterialFor(final);
        }
    }
}
