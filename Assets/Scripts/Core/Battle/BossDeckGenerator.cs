using System.Collections.Generic;
using KingCardsSpire.Configs;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Core.Battle
{
    /// <summary>
    /// 驻守 BOSS：固定「国王 + 平民」，其余按层目标张数从卡池随机；能力牌不放回（同 Id 至多一张），功能/基础放回抽样。
    /// 层目标张数 6,6,6,7,8,9,10；目标平均等级由第 1 层基准 + 每层 U(1.5,2.5) 累加，再通过从卡池贪心选牌逼近该均值（不修改卡表上的等级数值）。
    /// </summary>
    public static class BossDeckGenerator
    {
        /// <summary>与塔层数一致，下标 0 对应第 1 层。</summary>
        public static readonly int[] DeckSizesPerFloor = { 6, 6, 6, 7, 8, 9, 10 };

        private const float Floor1BaseMeanLevel = 2f;
        private const float MeanStepMin = 1.5f;
        private const float MeanStepMax = 2.5f;

        /// <summary>返回第 <paramref name="floor1Based"/> 层 BOSS 卡组目标张数（层数越界时钳位到 1–7）。</summary>
        public static int GetDeckTargetSize(int floor1Based)
        {
            var idx = Mathf.Clamp(floor1Based, 1, DeckSizesPerFloor.Length) - 1;
            return DeckSizesPerFloor[idx];
        }

        /// <summary>使用 <see cref="UnityEngine.Random"/> 构建 BOSS 卡组。</summary>
        public static List<Card> BuildDeck(int floor1Based, ConfigManager cfg) =>
            BuildDeck(floor1Based, cfg, null);

        /// <summary>使用 <paramref name="rng"/> 构建 BOSS 卡组（单测注入）；为 null 时等同无参重载。</summary>
        public static List<Card> BuildDeck(int floor1Based, ConfigManager cfg, System.Random rng)
        {
            if (cfg == null)
                return new List<Card>();

            var deckSize = GetDeckTargetSize(floor1Based);
            var targetMean = ComputeTargetMeanLevel(floor1Based, rng);
            var randomSlots = Mathf.Max(0, deckSize - 2);
            var deck = new List<Card>(deckSize);

            AppendKingAndCommoner(deck, cfg);

            var owned = new HashSet<string>();
            var abilityPool = new List<CardConfigEntry>();
            cfg.CollectShopCandidates(CardType.Ability, owned, abilityPool);
            FilterConsumables(abilityPool);

            var fbPool = new List<CardConfigEntry>();
            var scratch = new List<CardConfigEntry>();
            cfg.CollectShopCandidates(CardType.Function, owned, scratch);
            FilterConsumables(scratch);
            RemoveKingAndCommonerFromPool(scratch);
            fbPool.AddRange(scratch);

            scratch.Clear();
            cfg.CollectShopCandidates(CardType.Basic, owned, scratch);
            FilterConsumables(scratch);
            RemoveKingAndCommonerFromPool(scratch);
            fbPool.AddRange(scratch);

            var maxExclusiveAbilityRoll = Mathf.Max(1, 1 + floor1Based / 2);
            var rawAbility = NextInt(0, maxExclusiveAbilityRoll, rng);
            var abilityCount = Mathf.Clamp(rawAbility, 0, Mathf.Min(abilityPool.Count, randomSlots));

            var shuffledAbilities = new List<CardConfigEntry>(abilityPool);
            ShuffleEntriesInPlace(shuffledAbilities, rng);
            for (var i = 0; i < abilityCount; i++)
            {
                var entry = shuffledAbilities[i];
                var card = cfg.CreateRuntimeCard(entry);
                if (card != null)
                    deck.Add(card);
            }

            var fbNeeded = deckSize - deck.Count;
            AppendGreedyTowardTargetMean(deck, cfg, fbPool, fbNeeded, deckSize, targetMean, rng);

            if (deck.Count < deckSize)
                PadDeckToSize(deck, cfg, fbPool, deckSize, targetMean, rng);

            ShuffleInPlace(deck, rng);
            return deck;
        }

        /// <summary>当前卡组（非 null 牌）的算术平均等级。</summary>
        public static float ComputeDeckMeanLevel(IReadOnlyList<Card> deck)
        {
            if (deck == null || deck.Count == 0)
                return 0f;
            float sum = 0f;
            var n = 0;
            for (var i = 0; i < deck.Count; i++)
            {
                var c = deck[i];
                if (c == null)
                    continue;
                sum += c.Level;
                n++;
            }

            return n > 0 ? sum / n : 0f;
        }

        private static float ComputeTargetMeanLevel(int floor1Based, System.Random rng)
        {
            var sum = Floor1BaseMeanLevel;
            var f = Mathf.Max(1, floor1Based);
            for (var i = 2; i <= f; i++)
                sum += NextFloatInclusive(MeanStepMin, MeanStepMax, rng);
            return sum;
        }

        private static void AppendKingAndCommoner(List<Card> deck, ConfigManager cfg)
        {
            TryAppendCoreCard(deck, cfg, WellKnownCardIds.King, "国王", 3f);
            TryAppendCoreCard(deck, cfg, WellKnownCardIds.Commoner, "平民", 1f);
        }

        private static void TryAppendCoreCard(List<Card> deck, ConfigManager cfg, string id, string fallbackName,
            float fallbackLevel)
        {
            if (cfg.TryGetCard(id, out var cc))
            {
                var card = cfg.CreateRuntimeCard(cc);
                if (card != null)
                    deck.Add(card);
                else
                    deck.Add(NewRuntimeFallbackCard(id, fallbackName, fallbackLevel));
            }
            else
            {
                deck.Add(NewRuntimeFallbackCard(id, fallbackName, fallbackLevel));
            }
        }

        private static Card NewRuntimeFallbackCard(string id, string name, float level) =>
            new Card
            {
                Id = id,
                Name = name,
                Level = level,
                Type = CardType.Basic
            };

        private static void FilterConsumables(List<CardConfigEntry> pool)
        {
            if (pool == null)
                return;
            for (var i = pool.Count - 1; i >= 0; i--)
            {
                var e = pool[i];
                if (e == null || e.Type == CardType.Consumable)
                    pool.RemoveAt(i);
            }
        }

        private static void RemoveKingAndCommonerFromPool(List<CardConfigEntry> pool)
        {
            if (pool == null)
                return;
            for (var i = pool.Count - 1; i >= 0; i--)
            {
                var e = pool[i];
                if (e == null || string.IsNullOrEmpty(e.Id))
                {
                    pool.RemoveAt(i);
                    continue;
                }

                if (IsKingOrCommonerId(e.Id))
                    pool.RemoveAt(i);
            }
        }

        private static bool IsKingOrCommonerId(string id) =>
            string.Equals(id, WellKnownCardIds.King, System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(id, WellKnownCardIds.Commoner, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>从 <paramref name="pool"/> 放回抽样 <paramref name="count"/> 张，使整副 <paramref name="deckSize"/> 张牌的平均等级逼近 <paramref name="targetMean"/>。</summary>
        private static void AppendGreedyTowardTargetMean(List<Card> deck, ConfigManager cfg,
            List<CardConfigEntry> pool, int count, int deckSize, float targetMean, System.Random rng)
        {
            if (count <= 0 || pool == null || pool.Count == 0 || cfg == null)
                return;

            for (var i = 0; i < count; i++)
            {
                var lockedSum = SumDeckLevels(deck);
                var slotsLeft = deckSize - deck.Count;
                if (slotsLeft <= 0)
                    break;
                var desiredTotal = targetMean * deckSize;
                var remainingSum = desiredTotal - lockedSum;
                var idealNext = remainingSum / slotsLeft;
                var entry = PickClosestLevelEntry(pool, idealNext, rng);
                if (entry == null)
                    break;
                var card = cfg.CreateRuntimeCard(entry);
                if (card != null)
                    deck.Add(card);
            }
        }

        private static float SumDeckLevels(List<Card> deck)
        {
            if (deck == null)
                return 0f;
            float sum = 0f;
            for (var i = 0; i < deck.Count; i++)
            {
                var c = deck[i];
                if (c != null)
                    sum += c.Level;
            }

            return sum;
        }

        private static CardConfigEntry PickClosestLevelEntry(List<CardConfigEntry> pool, float idealLevel,
            System.Random rng)
        {
            if (pool == null || pool.Count == 0)
                return null;

            var best = float.PositiveInfinity;
            var ties = new List<int>(4);
            for (var i = 0; i < pool.Count; i++)
            {
                var e = pool[i];
                if (e == null)
                    continue;
                var d = Mathf.Abs(e.Level - idealLevel);
                if (d < best - 1e-6f)
                {
                    best = d;
                    ties.Clear();
                    ties.Add(i);
                }
                else if (Mathf.Abs(d - best) < 1e-6f)
                    ties.Add(i);
            }

            if (ties.Count == 0)
                return null;
            var pick = ties[NextInt(0, ties.Count, rng)];
            return pool[pick];
        }

        private static void PadDeckToSize(List<Card> deck, ConfigManager cfg, List<CardConfigEntry> fbPool,
            int deckSize, float targetMean, System.Random rng)
        {
            const int maxAttempts = 8192;
            var attempts = 0;
            while (deck.Count < deckSize && attempts < maxAttempts)
            {
                attempts++;
                if (fbPool.Count > 0)
                {
                    var lockedSum = SumDeckLevels(deck);
                    var slotsLeft = deckSize - deck.Count;
                    var desiredTotal = targetMean * deckSize;
                    var remainingSum = desiredTotal - lockedSum;
                    var idealNext = remainingSum / Mathf.Max(1, slotsLeft);
                    var entry = PickClosestLevelEntry(fbPool, idealNext, rng);
                    if (entry != null)
                    {
                        var card = cfg.CreateRuntimeCard(entry);
                        if (card != null)
                        {
                            deck.Add(card);
                            continue;
                        }
                    }
                }

                if (cfg.TryGetCard(WellKnownCardIds.Minister, out var minister))
                {
                    var card = cfg.CreateRuntimeCard(minister);
                    if (card != null)
                        deck.Add(card);
                }
                else
                {
                    break;
                }
            }
        }

        private static void ShuffleEntriesInPlace(List<CardConfigEntry> list, System.Random rng)
        {
            if (list == null || list.Count <= 1)
                return;
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = NextInt(0, i + 1, rng);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static void ShuffleInPlace(List<Card> deck, System.Random rng)
        {
            if (deck == null || deck.Count <= 1)
                return;
            for (var i = deck.Count - 1; i > 0; i--)
            {
                var j = NextInt(0, i + 1, rng);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
        }

        private static int NextInt(int minInclusive, int maxExclusive, System.Random rng)
        {
            if (maxExclusive <= minInclusive)
                return minInclusive;
            return rng != null ? rng.Next(minInclusive, maxExclusive) : Random.Range(minInclusive, maxExclusive);
        }

        private static float NextFloatInclusive(float min, float max, System.Random rng)
        {
            if (rng != null)
                return min + (float)(rng.NextDouble() * (max - min));
            return Random.Range(min, max);
        }
    }
}
