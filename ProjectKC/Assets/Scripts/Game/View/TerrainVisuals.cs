using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Aparencia provisoria do reino: blocos primitivos com cor chapada. Serve para
    /// provar o loop antes de existir arte (design D9 e a mitigacao de risco de arte
    /// no design). Quando os prefabs baixo-poli chegarem, o GridView troca a fonte
    /// dos objetos e esta classe sai.
    /// </summary>
    public static class TerrainVisuals
    {
        public const float TileSize = 1f;

        /// <summary>Folga entre blocos, para a silhueta do territorio ficar legivel.</summary>
        public const float TileGap = 0.06f;

        public const float TileStep = TileSize + TileGap;

        public static Color ColorFor(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Plain:
                    return new Color(0.55f, 0.72f, 0.36f);
                case TerrainType.Forest:
                    return new Color(0.22f, 0.47f, 0.28f);
                case TerrainType.Mine:
                    return new Color(0.45f, 0.44f, 0.50f);
                case TerrainType.River:
                    return new Color(0.30f, 0.56f, 0.78f);
                case TerrainType.Ruin:
                    return new Color(0.52f, 0.44f, 0.36f);
                default:
                    return Color.magenta;
            }
        }

        /// <summary>Altura do bloco. Da relevo ao diorama sem precisar de malha.</summary>
        public static float HeightFor(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.River:
                    return 0.18f;
                case TerrainType.Ruin:
                    return 0.26f;
                case TerrainType.Mine:
                    return 0.45f;
                case TerrainType.Forest:
                    return 0.38f;
                default:
                    return 0.32f;
            }
        }

        public static readonly Color HiddenTint = new Color(0.20f, 0.20f, 0.22f);
        public static readonly Color DestroyedTint = new Color(0.28f, 0.20f, 0.18f);
        public static readonly Color BuildingColor = new Color(0.88f, 0.78f, 0.52f);
        public static readonly Color HallColor = new Color(0.92f, 0.62f, 0.30f);
        public static readonly Color SelectionColor = new Color(1f, 0.95f, 0.55f);

        /// <summary>
        /// Celula possuida cujo edificio esta sem gente. Precisa ser identificavel de
        /// relance: se o jogador tiver que selecionar cada celula para descobrir que
        /// metade do reino parou, ele nao descobre (spec game-feel).
        /// </summary>
        public static readonly Color IdleTint = new Color(0.42f, 0.38f, 0.30f);

        public static readonly Color IdleMarkerColor = new Color(0.55f, 0.50f, 0.42f);
        public static readonly Color PurchasableColor = new Color(0.95f, 0.85f, 0.40f);

        public static Vector3 WorldPosition(Coord coord, float height)
        {
            return new Vector3(coord.X * TileStep, height * 0.5f, coord.Y * TileStep);
        }

        /// <summary>
        /// Material do pipeline em uso. Procura o shader do URP e cai no padrao se o
        /// projeto nao estiver em URP, para a cena nunca aparecer toda magenta.
        /// </summary>
        public static Material CreateMaterial(Color color, bool transparent = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader) { hideFlags = HideFlags.DontSave };
            SetColor(material, color);

            if (transparent)
            {
                MakeTransparent(material);
                SetColor(material, color);
            }

            return material;
        }

        public static void SetColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        /// <summary>
        /// Fator de escala para um prefab caber num alvo de tamanho conhecido, pela
        /// pegada no chao (X/Z, nao altura — torre alta e moinho largo devem caber
        /// igual). Cada pacote de asset externo vem numa escala diferente (o moinho
        /// do Kenney Fantasy Town e muito maior que a arvore do Nature Kit); sem
        /// isso, cada prefab novo quebraria o tabuleiro de um jeito diferente.
        /// </summary>
        public static float FitScale(Bounds bounds, float targetFootprint)
        {
            float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
            if (footprint <= 0.001f)
            {
                return 1f;
            }

            return Mathf.Clamp(targetFootprint / footprint, 0.01f, 25f);
        }

        /// <summary>Le a cor base do material, para guardar antes de tingir (ex.:
        /// escurecer marcador de construcao ocioso) e poder restaurar depois.</summary>
        public static Color GetColor(Material material)
        {
            if (material == null)
            {
                return Color.white;
            }

            if (material.HasProperty("_BaseColor"))
            {
                return material.GetColor("_BaseColor");
            }

            if (material.HasProperty("_Color"))
            {
                return material.GetColor("_Color");
            }

            return Color.white;
        }

        /// <summary>
        /// Aplica a textura do terreno sobre o material, se atribuida. A cor definida
        /// por <see cref="SetColor"/> continua multiplicando por cima (tinte de
        /// selecao, ociosidade, fantasma etc.), entao a textura nao quebra nenhum dos
        /// realces ja existentes.
        /// </summary>
        public static void SetTexture(Material material, Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
        }

        /// <summary>Nome e explicacao curta, para o tooltip ao pairar o mouse sobre a
        /// celula (spec terrain-art).</summary>
        public static string DescriptionFor(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Plain:
                    return "Planicie: terreno comum, aceita a maioria das construcoes.";
                case TerrainType.Forest:
                    return "Floresta: rende madeira extra; so algumas construcoes cabem aqui.";
                case TerrainType.Mine:
                    return "Mina: rende pedra; terreno rigido, poucas construcoes cabem.";
                case TerrainType.River:
                    return "Rio: producao de comida sensivel ao clima — chuva ajuda aqui.";
                case TerrainType.Ruin:
                    return "Ruina: terreno raro e danificado; poucas construcoes cabem.";
                default:
                    return terrain.ToString();
            }
        }

        private static void MakeTransparent(Material material)
        {
            // Chaves do URP/Lit para modo transparente. Sem elas o alpha e ignorado.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
        }
    }
}
