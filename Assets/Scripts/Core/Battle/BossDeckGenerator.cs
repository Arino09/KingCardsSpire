using System.Collections.Generic;
using KingCardsSpire.Configs;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Core.Battle
{
    /// <summary>
    /// 驻守 BOSS：固定「国王 + 平民」，其余按层目标张数从卡池随机；能力牌不放回（同 Id 至多一张），功能/基础放回抽样。
    /// 层目标张数 6,6,6,7,8,9,10；目标平均等级由第 1 层基准 + 每层 U(1.5,2.5) 累加，再对整副牌 Level 做平移对齐。
    /// </summary>
    public static class BossDeckGenerator
    {
        /// <summary>与塔层数一致，下标 0 对应第 1 层。</summary>
        public static readonly int[] DeckSizesPerFloor = { 6, 6, 6, 7, 8, 9, 10 };

        private const float Floor1BaseMeanLevel = 2f;
        private const float MeanStepMin = 1.5f;
        private const float MeanStepMax = 2.5f;
        private const float MinLevelAfterShift = 0.01f;

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
            AppendRandomWithReplacement(deck, cfg, fbPool, fbNeeded, rng);

            if (deck.Count < deckSize)
                PadDeckToSize(deck, cfg, fbPool, deckSize, rng);

            var targetMean = ComputeTargetMeanLevel(floor1Based, rng);
            ApplyMeanLevelShift(deck, targetMean);

            ShuffleInPlace(deck, rng);
            return deck;
        }

        /// <summary>将卡组算术平均 Level 平移到 <paramref name="targetMean"/>（各张 Level 同步加减；再不低于 <see cref="MinLevelAfterShift"/>）。</summary>
        public static void ApplyMeanLevelShift(List<Card> deck, float targetMean)
        {
            if (deck == null || deck.Count == 0)
                return;

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

            if (n == 0)
                return;

            var actualMean = sum / n;
            var delta = targetMean - actualMean;
            for (var i = 0; i < deck.Count; i++)
            {
                var c = deck[i];
                if (c == null)
                    continue;
                c.Level = Mathf.Max(MinLevelAfterShift, c.Level + delta);
            }
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

        private static void AppendRandomWithReplacement(List<Card> deck, ConfigManager cfg,
            List<CardConfigEntry> pool, int count, System.Random rng)
        {
            if (count <= 0 || pool == null || pool.Count == 0)
                return;

            var targetTotal = deck.Count + count;
            const int maxAttempts = 4096;
            var attempts = 0;
            while (deck.Count < targetTotal && attempts < maxAttempts)
            {
                attempts++;
                var idx = NextInt(0, pool.Count, rng);
                var entry = pool[idx];
                if (entry == null)
                    continue;
                var card = cfg.CreateRuntimeCard(entry);
                if (card != null)
                    deck.Add(card);
            }
        }

        private static void PadDeckToSize(List<Card> deck, ConfigManager cfg, List<CardConfigEntry> fbPool,
            int deckSize, System.Random rng)
        {
            const int maxAttempts = 8192;
            var attempts = 0;
            while (deck.Count < deckSize && attempts < maxAttempts)
            {
                attempts++;
                if (fbPool.Count > 0)
                {
                    var idx = NextInt(0, fbPool.Count, rng);
                    var card = cfg.CreateRuntimeCard(fbPool[idx]);
                    if (card != null)
                        deck.Add(card);
                    continue;
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
