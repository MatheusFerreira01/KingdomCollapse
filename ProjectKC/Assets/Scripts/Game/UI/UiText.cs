using System.Collections.Generic;
using System.Text;
using KingdomCollapse.Core;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Texto que o jogador le. O Core descreve regras em ASCII, sem acento, porque
    /// e o que os testes comparam; aqui esse texto ganha acento e nome proprio
    /// antes de ir para a tela.
    /// </summary>
    public static class UiText
    {
        public static string Resource(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Gold:
                    return "Ouro";
                case ResourceKind.Wood:
                    return "Madeira";
                case ResourceKind.Stone:
                    return "Pedra";
                case ResourceKind.Food:
                    return "Comida";
                case ResourceKind.Population:
                    return "Gente";
                default:
                    return kind.ToString();
            }
        }

        public static string Terrain(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Plain:
                    return "Planície";
                case TerrainType.Forest:
                    return "Floresta";
                case TerrainType.Mine:
                    return "Rochedo";
                case TerrainType.River:
                    return "Rio";
                case TerrainType.Ruin:
                    return "Ruína";
                default:
                    return terrain.ToString();
            }
        }

        public static string TerrainHint(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Plain:
                    return "Boa para Fazenda e Mercado.";
                case TerrainType.Forest:
                    return "Serraria rende madeira aqui, e mais com floresta em volta.";
                case TerrainType.Mine:
                    return "Pedreira rende pedra aqui.";
                case TerrainType.River:
                    return "Só aceita Ancoradouro. Fazendas vizinhas rendem mais.";
                case TerrainType.Ruin:
                    return "Terreno pobre: serve para casas e defesas.";
                default:
                    return string.Empty;
            }
        }

        public static string Stance(WorkerStance stance)
        {
            switch (stance)
            {
                case WorkerStance.ProductionFirst:
                    return "Campos primeiro";
                case WorkerStance.DefenseFirst:
                    return "Muralhas primeiro";
                default:
                    return "Equilibrada";
            }
        }

        /// <summary>"10 Ouro, 6 Madeira", com cada recurso na sua cor.</summary>
        public static string Amounts(ResourceAmounts amounts, bool signed = false)
        {
            if (amounts == null || amounts.IsEmpty)
            {
                return "nada";
            }

            StringBuilder builder = new StringBuilder();
            foreach (ResourceKind kind in amounts.NonZero())
            {
                if (builder.Length > 0)
                {
                    builder.Append("  ");
                }

                int value = amounts[kind];
                string number = (signed && value > 0 ? "+" : string.Empty) + value;
                builder.Append(UiTheme.Tint(number + " " + Resource(kind), UiTheme.ResourceColor(kind)));
            }

            return builder.ToString();
        }

        /// <summary>Custo com o que falta marcado em vermelho.</summary>
        public static string Cost(ResourceAmounts cost, RunState run)
        {
            if (cost == null || cost.IsEmpty)
            {
                return "de graça";
            }

            StringBuilder builder = new StringBuilder();
            foreach (ResourceKind kind in cost.NonZero())
            {
                if (builder.Length > 0)
                {
                    builder.Append("  ");
                }

                bool enough = run == null || run[kind] >= cost[kind];
                builder.Append(UiTheme.Tint(
                    cost[kind] + " " + Resource(kind),
                    enough ? UiTheme.ResourceColor(kind) : UiTheme.Bad));
            }

            return builder.ToString();
        }

        public static string Days(int days)
        {
            return days <= 0 ? "HOJE" : days == 1 ? "amanhã" : "em " + days + " dias";
        }

        private static readonly KeyValuePair<string, string>[] Accents =
        {
            Pair("populacao", "população"), Pair("producao", "produção"), Pair("celulas", "células"),
            Pair("celula", "célula"), Pair("edificios", "edifícios"), Pair("edificio", "edifício"),
            Pair("territorio", "território"), Pair("forca", "força"), Pair("nao", "não"),
            Pair("proximo", "próximo"), Pair("graca", "graça"), Pair("Constroi", "Constrói"),
            Pair("Poe", "Põe"), Pair("ate", "até"), Pair("mao", "mão"), Pair("ja", "já"),
            Pair("alem", "além"), Pair("so", "só"), Pair("ninguem", "ninguém"),
            Pair("adjacencia", "vizinhança"), Pair("guarnicao", "guarnição"),
            Pair("Plain", "Planície"), Pair("Forest", "Floresta"), Pair("Mine", "Rochedo"),
            Pair("River", "Rio"), Pair("Ruin", "Ruína"), Pair("comeco", "começo"),
            Pair("inicio", "início"), Pair("voce", "você"), Pair("dano", "dano")
        };

        private static KeyValuePair<string, string> Pair(string from, string to)
        {
            return new KeyValuePair<string, string>(from, to);
        }

        /// <summary>
        /// Poe acento no texto de regra que vem do Core. Troca so palavra inteira:
        /// "so" nao pode virar acento dentro de "isso".
        /// </summary>
        public static string Polish(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(text.Length + 8);
            int i = 0;
            while (i < text.Length)
            {
                bool atWordStart = i == 0 || !char.IsLetter(text[i - 1]);
                bool replaced = false;

                if (atWordStart)
                {
                    for (int a = 0; a < Accents.Length; a++)
                    {
                        string from = Accents[a].Key;
                        if (string.CompareOrdinal(text, i, from, 0, from.Length) != 0)
                        {
                            continue;
                        }

                        int end = i + from.Length;
                        bool boundary = end >= text.Length || !char.IsLetter(text[end]);
                        if (!boundary)
                        {
                            continue;
                        }

                        builder.Append(Accents[a].Value);
                        i = end;
                        replaced = true;
                        break;
                    }
                }

                if (!replaced)
                {
                    builder.Append(text[i]);
                    i++;
                }
            }

            return builder.ToString();
        }

        public static string CardRules(CardDefinition card)
        {
            return Polish(CardText.RulesFor(card));
        }

        public static string Phase(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Planning:
                    return "Planejamento";
                case DayPhase.Collapsed:
                    return "Colapso";
                case DayPhase.Won:
                    return "Vitória";
                default:
                    return "Resolução";
            }
        }
    }
}
