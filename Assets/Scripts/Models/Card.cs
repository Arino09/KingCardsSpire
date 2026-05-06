using System;

namespace KingCardsSpire.Models
{
    /// <summary>
    /// 运行时卡牌数据（对局/背包等）。
    /// </summary>
    [Serializable]
    public class Card
    {
        public string Id;
        public string Name;
        public float Level;
        public CardType Type;
        public string EffectDesc;
        public bool IsUnique;
    }
}
