using System;
using System.Collections.Generic;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KingCardsSpire.Core.Battle
{
    /// <summary>
    /// 主角房友谊赛（槽位 1）三选一卡牌：仅基础牌、排除国王/大臣/平民、单卡等级上限、
    /// 70% 概率三卡等级均值 ≤1.5，30% 均值 ≥1.7（池子无法满足时降级并打日志）。
    /// </summary>
    public static class HeroDuelVictoryRewardPicker
    {
        private const float LowTierMeanMax = 1.5f;

        private const float HighTierMeanMin = 1.7f;

        private const float LowTierWeight = 0.7f;

        private const float MaxSingleCardLevel = 2f;

        private const float FloatEpsilon = 1e-4f;

        private const int MaxMeanSampleAttempts = 200;

        /// <summary>
        /// 从卡表构建候选池并抽取至多 3 张不重复 CardId；无候选时返回 <c>null</c>。
        /// </summary>
        public static List<string> BuildOfferCardIds(ConfigManager cfg, Func<string, bool> ownsUniqueCard,
            Func<int, int, int> randomRange = null, Func<float> random01 = null)
        {
            if (cfg == null || ownsUniqueCard == null)
                return null;

            var pool = BuildCandidatePool(cfg, ownsUniqueCard);
            return PickOfferFromPool(pool, randomRange, random01);
        }

        /// <summary>
        /// 供单元测试或自定义候选：列表应已满足基础卡、等级与排除规则。
        /// </summary>
        public static List<string> BuildOfferCardIdsFromCandidatePool(IReadOnlyList<Card> pool,
            Func<int, int, int> randomRange, Func<float> random01)
        {
            if (pool == null || randomRange == null || random01 == null)
                return null;

            return PickOfferFromPool(pool, randomRange, random01);
        }

        private static List<Card> BuildCandidatePool(ConfigManager cfg, Func<string, bool> ownsUniqueCard)
        {
            var raw = new List<Card>();
            cfg.AppendAllCardsAsRuntime(raw);

            var pool = new List<Card>(raw.Count);
            for (var i = 0; i < raw.Count; i++)
            {
                var c = raw[i];
                if (c == null || string.IsNullOrEmpty(c.Id))
                    continue;
                if (c.Type != CardType.Basic)
                    continue;
                if (IsCoreTrioId(c.Id))
                    continue;
                if (c.Level > MaxSingleCardLevel + FloatEpsilon)
                    continue;
                if (c.IsUnique && ownsUniqueCard(c.Id))
                    continue;

                pool.Add(c);
            }

            return pool;
        }

        private static bool IsCoreTrioId(string id) =>
            string.Equals(id, WellKnownCardIds.King, StringComparison.OrdinalIgnoreCase)
            || string.Equals(id, WellKnownCardIds.Minister, StringComparison.OrdinalIgnoreCase)
            || string.Equals(id, WellKnownCardIds.Commoner, StringComparison.OrdinalIgnoreCase);

        private static List<string> PickOfferFromPool(IReadOnlyList<Card> pool,
            Func<int, int, int> randomRange, Func<float> random01)
        {
            var rr = randomRange ?? ((min, max) => Random.Range(min, max));
            var rv = random01 ?? (() => Random.value);

            var n = pool.Count;
            if (n == 0)
                return null;

            if (n < 3)
                return BuildShortOfferWithoutMeanConstraint(pool, n);

            var wantLowMean = rv() < LowTierWeight;

            if (TryPickThreeMeetingMeanConstraint(pool, n, wantLowMean, rr, out var offer))
                return offer;

            if (TryPickThreeMeetingMeanConstraint(pool, n, !wantLowMean, rr, out offer))
                return offer;

            Debug.LogWarning(
                "[HeroDuelVictoryRewardPicker] 无法在有限次尝试内满足均值分档，已降级为池中任意三张不重复基础卡；请检查卡表低等级基础牌是否充足。");
            return PickAnyThreeDistinct(pool, n, rr);
        }

        private static List<string> BuildShortOfferWithoutMeanConstraint(IReadOnlyList<Card> pool, int n)
        {
            var list = new List<string>(n);
            for (var i = 0; i < n; i++)
            {
                var id = pool[i].Id;
                if (!string.IsNullOrEmpty(id))
                    list.Add(id);
            }

            return list.Count > 0 ? list : null;
        }

        private static bool TryPickThreeMeetingMeanConstraint(IReadOnlyList<Card> pool, int n, bool wantLowMean,
            Func<int, int, int> rr, out List<string> offer)
        {
            offer = null;
            for (var attempt = 0; attempt < MaxMeanSampleAttempts; attempt++)
            {
                if (!TryPickThreeDistinctIndices(n, rr, out var i0, out var i1, out var i2))
                    return false;

                var sum = pool[i0].Level + pool[i1].Level + pool[i2].Level;
                var mean = sum / 3f;

                if (wantLowMean)
                {
                    if (mean > LowTierMeanMax + FloatEpsilon)
                        continue;
                }
                else
                {
                    if (mean < HighTierMeanMin - FloatEpsilon)
                        continue;
                }

                offer = new List<string>(3)
                {
                    pool[i0].Id,
                    pool[i1].Id,
                    pool[i2].Id
                };
                return true;
            }

            return false;
        }

        private static bool TryPickThreeDistinctIndices(int n, Func<int, int, int> rr, out int i0, out int i1,
            out int i2)
        {
            i0 = i1 = i2 = -1;
            if (n < 3)
                return false;

            for (var t = 0; t < MaxMeanSampleAttempts; t++)
            {
                i0 = rr(0, n);
                i1 = rr(0, n);
                i2 = rr(0, n);
                if (i0 != i1 && i1 != i2 && i0 != i2)
                    return true;
            }

            i0 = 0;
            i1 = 1;
            i2 = 2;
            return true;
        }

        private static List<string> PickAnyThreeDistinct(IReadOnlyList<Card> pool, int n,
            Func<int, int, int> rr)
        {
            var chosen = new List<int>(3);
            for (var guard = 0; guard < MaxMeanSampleAttempts && chosen.Count < 3; guard++)
            {
                var k = rr(0, n);
                if (!chosen.Contains(k))
                    chosen.Add(k);
            }

            for (var i = 0; i < n && chosen.Count < 3; i++)
            {
                if (!chosen.Contains(i))
                    chosen.Add(i);
            }

            if (chosen.Count < 3)
                return null;

            return new List<string>(3)
            {
                pool[chosen[0]].Id,
                pool[chosen[1]].Id,
                pool[chosen[2]].Id
            };
        }
    }
}
