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

        /// <summary>仅对战运行时用于区分同 Id 多实例（不参与存档序列化）。</summary>
        [NonSerialized]
        public string BattleInstanceId;
    }
}
