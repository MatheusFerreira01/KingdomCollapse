using System.Collections.Generic;

namespace KingdomCollapse.Core
{
    /// <summary>
    /// Deck, mao e descarte de uma run. Toda alteracao vale so para a run corrente:
    /// a run seguinte remonta o deck do zero a partir da raca e dos desbloqueios.
    /// </summary>
    public sealed class RunDeck
    {
        private readonly List<CardDefinition> _drawPile = new List<CardDefinition>();
        private readonly List<CardDefinition> _hand = new List<CardDefinition>();
        private readonly List<CardDefinition> _discardPile = new List<CardDefinition>();

        public RunDeck(IEnumerable<CardDefinition> startingCards = null)
        {
            if (startingCards == null)
            {
                return;
            }

            foreach (CardDefinition card in startingCards)
            {
                if (card != null)
                {
                    _drawPile.Add(card);
                }
            }
        }

        public IReadOnlyList<CardDefinition> Hand => _hand;

        public IReadOnlyList<CardDefinition> DrawPile => _drawPile;

        public IReadOnlyList<CardDefinition> DiscardPile => _discardPile;

        public int TotalCards => _drawPile.Count + _hand.Count + _discardPile.Count;

        public void Shuffle(IRandomSource random)
        {
            RunRandom.Shuffle(random, _drawPile);
        }

        /// <summary>
        /// Compra ate count cartas. Quando o deck esvazia, o descarte e embaralhado
        /// de volta. Deck e descarte vazios param a compra sem erro (spec card-system).
        /// </summary>
        public int Draw(int count, IRandomSource random)
        {
            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                if (_drawPile.Count == 0)
                {
                    if (_discardPile.Count == 0)
                    {
                        break;
                    }

                    RecycleDiscard(random);
                }

                int last = _drawPile.Count - 1;
                _hand.Add(_drawPile[last]);
                _drawPile.RemoveAt(last);
                drawn++;
            }

            return drawn;
        }

        /// <summary>Completa a mao ate handSize. Cartas retidas ja contam contra o limite.</summary>
        public int DrawUpTo(int handSize, IRandomSource random)
        {
            int missing = handSize - _hand.Count;
            return missing <= 0 ? 0 : Draw(missing, random);
        }

        private void RecycleDiscard(IRandomSource random)
        {
            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();
            RunRandom.Shuffle(random, _drawPile);
        }

        public bool HandContains(CardDefinition card) => _hand.Contains(card);

        /// <summary>Tira a carta da mao e manda para o descarte. Usado ao jogar.</summary>
        public bool DiscardFromHand(CardDefinition card)
        {
            if (!_hand.Remove(card))
            {
                return false;
            }

            _discardPile.Add(card);
            return true;
        }

        /// <summary>
        /// Fim do Dia: manda a mao para o descarte, menos as cartas retidas, que
        /// continuam na mao e contam contra o limite de compra do dia seguinte.
        /// </summary>
        public int DiscardHand()
        {
            int discarded = 0;
            for (int i = _hand.Count - 1; i >= 0; i--)
            {
                if (_hand[i].Retained)
                {
                    continue;
                }

                _discardPile.Add(_hand[i]);
                _hand.RemoveAt(i);
                discarded++;
            }

            return discarded;
        }

        public void AddToDiscard(CardDefinition card)
        {
            if (card != null)
            {
                _discardPile.Add(card);
            }
        }

        public void AddToDrawPile(CardDefinition card)
        {
            if (card != null)
            {
                _drawPile.Add(card);
            }
        }

        /// <summary>
        /// Remove uma carta qualquer da run. Procura no descarte, depois no deck,
        /// depois na mao: tirar da mao e o que mais atrapalha o jogador, entao fica
        /// por ultimo.
        /// </summary>
        public CardDefinition RemoveRandomCard(IRandomSource random)
        {
            List<CardDefinition> source = null;
            if (_discardPile.Count > 0)
            {
                source = _discardPile;
            }
            else if (_drawPile.Count > 0)
            {
                source = _drawPile;
            }
            else if (_hand.Count > 0)
            {
                source = _hand;
            }

            if (source == null)
            {
                return null;
            }

            int index = random.NextInt(0, source.Count);
            CardDefinition removed = source[index];
            source.RemoveAt(index);
            return removed;
        }

        /// <summary>Todas as cartas da run, em qualquer pilha. Usado por testes e placar.</summary>
        public List<CardDefinition> AllCards()
        {
            List<CardDefinition> all = new List<CardDefinition>(TotalCards);
            all.AddRange(_drawPile);
            all.AddRange(_hand);
            all.AddRange(_discardPile);
            return all;
        }
    }
}
