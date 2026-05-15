using System;

namespace KingCardsSpire.Models
{
    /// <summary>为跨战斗识别的卡牌实例分配并补全 <see cref="Card.DeckInstanceId"/>。</summary>
    public static class CardDeckIdentity
    {
        public static void EnsureDeckInstanceId(Card card)
        {
            if (card == null || !string.IsNullOrEmpty(card.DeckInstanceId))
                return;
            card.DeckInstanceId = Guid.NewGuid().ToString("N");
        }
    }
}
