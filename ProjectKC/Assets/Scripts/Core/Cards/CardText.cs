using System.Collections.Generic;
using System.Text;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Texto de regra de uma carta, montado dos efeitos que ela de fato aplica. Nao e
    /// autorado a parte de proposito: texto escrito a mao desatualiza no primeiro
    /// rebalanceamento, e uma carta que diz uma coisa e faz outra e pior que carta
    /// sem texto.
    /// </summary>
    public static class CardText
    {
        public static string RulesFor(CardDefinition card)
        {
            if (card == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < card.Effects.Count; i++)
            {
                string line = card.Effects[i]?.Describe();
                if (!string.IsNullOrEmpty(line))
                {
                    parts.Add(line);
                }
            }

            StringBuilder builder = new StringBuilder(string.Join(". ", parts));
            if (builder.Length > 0)
            {
                builder.Append('.');
            }

            string target = TargetText(card.Target);
            if (target != null)
            {
                builder.Append(builder.Length > 0 ? " " : string.Empty).Append("Alvo: ").Append(target).Append('.');
            }

            if (card.Retained)
            {
                builder.Append(builder.Length > 0 ? " " : string.Empty).Append("Fica na mao no fim do dia.");
            }

            return builder.ToString();
        }

        public static string TargetText(TargetRequirement target)
        {
            switch (target)
            {
                case TargetRequirement.OwnedTile:
                    return "celula sua";
                case TargetRequirement.BuildableTile:
                    return "celula sua vazia";
                case TargetRequirement.TileWithBuilding:
                    return "celula com edificio";
                case TargetRequirement.DestroyedTile:
                    return "celula arrasada";
                case TargetRequirement.PurchasableTile:
                    return "celula vizinha ao reino";
                case TargetRequirement.AnyTile:
                    return "qualquer celula";
                default:
                    return null;
            }
        }
    }
}
