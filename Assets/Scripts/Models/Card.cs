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

        /// <summary>跨战斗持久化的卡组实例 Id（写入存档；与 <see cref="BattleInstanceId"/> 独立）。</summary>
        public string DeckInstanceId = string.Empty;
    }
}

