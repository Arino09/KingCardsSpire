using System;
using System.Collections.Generic;
using KingCardsSpire.Models;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KingCardsSpire.Core.Battle
{
    /// <summary>
    /// 常规战斗胜利：从本局敌方手牌与弃牌中筛选可奖励 CardId（排除国王/平民），去重后随机至多 3 项。
    /// </summary>
    public static class CasualVictoryRewardPicker
    {
        private const int MaxOfferCount = 3;

        /// <summary>
        /// 构建待展示奖励选项；若无可用卡牌则返回 <c>null</c> 或长度为 0 的数组（调用方统一按空处理）。
        /// </summary>
        public static string[] BuildOfferCardIds(IReadOnlyList<Card> enemyHand, IReadOnlyList<Card> enemyDiscard)
        {
            var distinct = new List<string>();
            AppendEligibleIds(enemyHand, distinct);
            AppendEligibleIds(enemyDiscard, distinct);

            if (distinct.Count == 0)
                return null;

            for (var i = 0; i < distinct.Count; i++)
            {
                var j = Random.Range(i, distinct.Count);
                (distinct[i], distinct[j]) = (distinct[j], distinct[i]);
            }

            if (distinct.Count <= MaxOfferCount)
                return distinct.ToArray();

            return new[]
            {
                distinct[0],
                distinct[1],
                distinct[2]
            };
        }

        private static void AppendEligibleIds(IReadOnlyList<Card> cards, List<string> dest)
        {
            if (cards == null || dest == null)
                return;

            for (var i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                if (c == null || string.IsNullOrEmpty(c.Id))
                    continue;
                if (IsExcludedFromCasualReward(c.Id))
                    continue;
                if (dest.Contains(c.Id))
                    continue;
                dest.Add(c.Id);
            }
        }

        private static bool IsExcludedFromCasualReward(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
                return true;
            if (string.Equals(cardId, WellKnownCardIds.King, StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(cardId, WellKnownCardIds.Commoner, StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }
    }
}
