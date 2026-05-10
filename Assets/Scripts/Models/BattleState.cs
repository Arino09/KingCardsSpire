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

        /// <summary>已完成的出牌回合数。</summary>
        public int Round;

        /// <summary>本局回合上限（§2.2.5），暖风时为 0 表示不使用上限。</summary>
        public int MaxRound;

        public bool NoRoundLimit;

        /// <summary>本场用于等级计算的天气快照。</summary>
        public WeatherType BattleWeather = WeatherType.WarmWind;

        /// <summary>战斗界面顶部展示的敌方名称（BOSS 来自塔配置等）。</summary>
        public string OpponentDisplayName = string.Empty;

        public string[] TurnHistory = Array.Empty<string>();
    }
}
