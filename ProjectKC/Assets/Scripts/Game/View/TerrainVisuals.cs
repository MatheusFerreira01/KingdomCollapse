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
                    return new Color(0.50f, 0.70f, 0.34f);
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

        public static readonly Color HiddenTint = new Color(0.26f, 0.27f, 0.31f);
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

        /// <summary>Celula que aceita a carta em curso.</summary>
        public static readonly Color TargetColor = new Color(0.40f, 0.90f, 1.00f);

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
        /// O chao em volta do reino: um plano escuro com a grade das celulas marcada
        /// bem de leve. Da onde o reino pisar e onde a sombra cair, e mostra para
        /// onde o territorio pode crescer, sem competir com as celulas.
        /// </summary>
        public static GameObject CreateGround(Transform parent)
        {
            const float Extent = 400f;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Chao";
            ground.transform.SetParent(parent, false);
            ground.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            ground.transform.localScale = new Vector3(Extent / 10f, 1f, Extent / 10f);

            // O clique no mundo procura celulas; o chao nao pode entrar na frente.
            Collider collider = ground.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            const int Size = 64;
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
                hideFlags = HideFlags.DontSave
            };

            Color fill = new Color(0.150f, 0.172f, 0.205f);
            Color line = new Color(0.205f, 0.232f, 0.275f);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    bool edge = x == 0 || y == 0 || x == Size - 1 || y == Size - 1;
                    texture.SetPixel(x, y, edge ? line : fill);
                }
            }

            texture.Apply(true);

            Material material = CreateMaterial(Color.white);
            material.mainTexture = texture;

            // Uma repeticao por celula, com a linha caindo na divisa entre elas.
            float repeats = Extent / TileStep;
            float offset = 0.5f - (Extent * 0.5f) / TileStep;
            offset -= Mathf.Floor(offset);
            material.mainTextureScale = new Vector2(repeats, repeats);
            material.mainTextureOffset = new Vector2(offset, offset);

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0f);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0f);
            }

            Renderer renderer = ground.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ground;
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
