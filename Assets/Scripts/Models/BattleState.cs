using System;

namespace KingCardsSpire.Models
{
    [Serializable]
    public class BattleState
    {
        public Card[] PlayerHand = Array.Empty<Card>();
        public Card[] EnemyHand = Array.Empty<Card>();
        public Card[] PlayerDiscard = Array.Empty<Card>();
        public Card[] EnemyDiscard = Array.Empty<Card>();
        public Card[] PlayerVisible = Array.Empty<Card>();
        public Card[] EnemyVisible = Array.Empty<Card>();
        public int Round;
        public int MaxRound;
        public string[] TurnHistory = Array.Empty<string>();
    }
}
