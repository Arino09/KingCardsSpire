using System;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Core.Battle
{
    /// <summary>
    /// 斗兽棋式单轮比较与有效等级（Doc/Plan §2.2.2、§2.3、§2.2.5）。
    /// 形态与功能牌特殊结算见 <see cref="BattleCardEffectResolver"/> / <see cref="BattleManager"/>（Phase 5）。
    /// </summary>
    public static class CardBattleRules
    {
        private const float Epsilon = 0.001f;

        public static bool IsKing(Card c) =>
            c != null && string.Equals(c.Id, WellKnownCardIds.King, StringComparison.OrdinalIgnoreCase);

        public static bool IsCommoner(Card c) =>
            c != null && string.Equals(c.Id, WellKnownCardIds.Commoner, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 烈日：所有带 .5 小数的等级 ±0.5（§2.3）；冰雹：+0.5；暖风/雨季/终焉：本场数值不变。
        /// </summary>
        public static float GetEffectiveLevel(Card card, WeatherType weather)
        {
            if (card == null)
                return 0f;
            var lv = card.Level;
            if (!HasHalfStepFraction(lv))
                return lv;
            return weather switch
            {
                WeatherType.Sunny => lv - 0.5f,
                WeatherType.Hail => lv + 0.5f,
                _ => lv
            };
        }

        /// <summary>
        /// 当前手牌「总手牌等级」：各张牌有效等级之和（§2.2.5）。
        /// </summary>
        public static float SumTotalHandLevel(System.Collections.Generic.IReadOnlyList<Card> hand,
            WeatherType weather)
        {
            if (hand == null || hand.Count == 0)
                return 0f;
            var sum = 0f;
            for (var i = 0; i < hand.Count; i++)
                sum += GetEffectiveLevel(hand[i], weather);
            return sum;
        }

        /// <summary>
        /// 单局最大回合数：⌊(双方开局手牌张数之和) / 2⌋（§2.2.5）。
        /// </summary>
        public static int ComputeMaxRounds(int initialPlayerHandCount, int initialEnemyHandCount) =>
            (initialPlayerHandCount + initialEnemyHandCount) / 2;

        public static BattleCompareResult Compare(Card a, Card b, WeatherType weather)
        {
            if (a == null && b == null)
                return BattleCompareResult.Draw;
            if (a == null)
                return BattleCompareResult.SecondWins;
            if (b == null)
                return BattleCompareResult.FirstWins;

            var la = GetEffectiveLevel(a, weather);
            var lb = GetEffectiveLevel(b, weather);
            var aKing = IsKing(a);
            var bKing = IsKing(b);
            var aCom = IsCommoner(a);
            var bCom = IsCommoner(b);

            // 平民胜国王
            if (aCom && bKing)
                return BattleCompareResult.FirstWins;
            if (aKing && bCom)
                return BattleCompareResult.SecondWins;

            // 其他情况下 1 级不能胜国王（非平民）
            if (aKing && IsPlainLevelOne(lb) && !bCom)
                return BattleCompareResult.FirstWins;
            if (bKing && IsPlainLevelOne(la) && !aCom)
                return BattleCompareResult.SecondWins;

            // 3.5 胜 1 级（文档特指 1 级档，不含 1.1 等）
            if (Approx(la, 3.5f) && Approx(lb, 1f))
                return BattleCompareResult.FirstWins;
            if (Approx(lb, 3.5f) && Approx(la, 1f))
                return BattleCompareResult.SecondWins;

            if (Approx(la, lb))
                return BattleCompareResult.Draw;

            return la > lb ? BattleCompareResult.FirstWins : BattleCompareResult.SecondWins;
        }

        /// <summary>非平民的「纯 1 级」用于国王克制例外。</summary>
        private static bool IsPlainLevelOne(float effectiveLevel) => Approx(effectiveLevel, 1f);

        private static bool Approx(float a, float b) => Mathf.Abs(a - b) < Epsilon;

        private static bool HasHalfStepFraction(float lv)
        {
            var frac = Mathf.Abs(lv - Mathf.Floor(lv));
            if (frac > 0.5f + Epsilon)
                frac = 1f - frac;
            return Mathf.Abs(frac - 0.5f) < 0.06f;
        }
    }
}
