using System.Collections.Generic;
using KingCardsSpire.Models;

namespace KingCardsSpire.Core.Battle
{
    /// <summary>
    /// 主角房对战：敌方卡组生成（当前为占位，与默认敌方测试卡组一致；算法待实现）。
    /// </summary>
    public static class HeroOpponentDeckGenerator
    {
        // TODO: 按 heroSlotId / 层数 / 难度等算法生成敌方卡组。

        public static List<Card> BuildPlaceholderDeck(string heroSlotId)
        {
            _ = heroSlotId;
            return new List<Card>
            {
                NewRuntimeCard(WellKnownCardIds.King, "敌国王", 3f),
                NewRuntimeCard("minister", "敌大臣", 2f),
                NewRuntimeCard("thief", "盗贼", 0.5f),
                NewRuntimeCard("noble", "贵族", 2.3f)
            };
        }

        private static Card NewRuntimeCard(string id, string name, float level) =>
            new Card
            {
                Id = id,
                Name = name,
                Level = level,
                Type = CardType.Basic
            };
    }
}
