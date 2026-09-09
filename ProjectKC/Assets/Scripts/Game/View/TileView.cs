using KingdomCollapse.Core;
using UnityEngine;

namespace KingdomCollapse.Game
{
    /// <summary>
    /// Bloco visual de uma celula. Guarda o vinculo com a coordenada do modelo, que
    /// e o que o clique usa para voltar do mundo 3D para o grid.
    /// Fica em arquivo proprio porque o Unity so serializa um MonoBehaviour quando o
    /// nome da classe bate com o do arquivo.
    /// </summary>
    public sealed class TileView : MonoBehaviour
    {
        public Coord Coord;

        /// <summary>Celula ainda nao possuida, desenhada como oferta de compra.</summary>
        public bool IsGhost;
    }
}
