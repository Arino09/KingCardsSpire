using System;
using System.Collections.Generic;
using KingCardsSpire.Configs;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Core.Battle
{
    /// <summary>
    /// 主角房对战：敌方卡组生成（固定国王/大臣/平民；约 20% 概率额外 1 张能力牌，否则 0 张；再放回随机功能牌与补充基础牌；不含消耗牌）。
    /// </summary>
    public static class HeroOpponentDeckGenerator
    {
        /// <summary>
        /// 放回抽样：在 <c>[min, max)</c> 内取 <paramref name="pickCount"/> 个下标（池大小 ≤0 或抽取数 ≤0 时不写入）。供测试注入 RNG。
        /// </summary>
        /// <param name="poolSize">候选池大小（与 <paramref name="randomRange"/> 的上界一致）。</param>
        /// <param name="pickCount">抽取次数。</param>
        /// <param name="dest">写入下标列表（会先 Clear）。</param>
        /// <param name="randomRange">与 <see cref="UnityEngine.Random.Range(int, int)"/> 相同语义：<c>[min, max)</c>。</param>
        public static void PickWithReplacementIndices(int poolSize, int pickCount, List<int> dest,
            System.Func<int, int, int> randomRange)
        {
            dest?.Clear();
            if (dest == null || poolSize <= 0 || pickCount <= 0)
                return;

            for (var i = 0; i < pickCount; i++)
                dest.Add(randomRange(0, poolSize));
        }

        /// <summary>构建参赛者友谊战敌方卡组；<paramref name="heroSlotId"/> 预留（当前未参与随机）。</summary>
        public static List<Card> BuildDeck(string heroSlotId)
        {
            _ = heroSlotId;
            var deck = new List<Card>();
            var cfg = ConfigManager.Instance;

            AppendCoreTrio(deck, cfg);

            var ownedScratch = new HashSet<string>();
            var pool = new List<CardConfigEntry>();

            if (cfg != null)
            {
                const float abilityOneCardChance = 0.2f;
                var abilityCount = UnityEngine.Random.value < abilityOneCardChance ? 1 : 0;
                cfg.CollectShopCandidates(CardType.Ability, ownedScratch, pool);
                FilterHeroOpponentConsumablesFromPool(pool);
                AppendRandomWithReplacement(deck, cfg, pool, abilityCount);

                var functionCount = UnityEngine.Random.Range(0, 4);
                cfg.CollectShopCandidates(CardType.Function, ownedScratch, pool);
                FilterHeroOpponentConsumablesFromPool(pool);
                AppendRandomWithReplacement(deck, cfg, pool, functionCount);

                var basicExtraCount = UnityEngine.Random.Range(0, 4);
                cfg.CollectShopCandidates(CardType.Basic, ownedScratch, pool);
                FilterHeroOpponentConsumablesFromPool(pool);
                RemoveCoreTrioFromPool(pool);
                AppendRandomWithReplacement(deck, cfg, pool, basicExtraCount);
            }

            ShuffleInPlace(deck);
            return deck;
        }

        private static void AppendCoreTrio(List<Card> deck, ConfigManager cfg)
        {
            TryAppendCoreCard(deck, cfg, WellKnownCardIds.King, "国王", 3f);
            TryAppendCoreCard(deck, cfg, WellKnownCardIds.Minister, "大臣", 2f);
            TryAppendCoreCard(deck, cfg, WellKnownCardIds.Commoner, "平民", 1f);
        }

        private static void TryAppendCoreCard(List<Card> deck, ConfigManager cfg, string id, string fallbackName,
            float fallbackLevel)
        {
            if (cfg != null && cfg.TryGetCard(id, out var cc))
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

        private static void RemoveCoreTrioFromPool(List<CardConfigEntry> pool)
        {
            for (var i = pool.Count - 1; i >= 0; i--)
            {
                var e = pool[i];
                if (e == null || string.IsNullOrEmpty(e.Id))
                {
                    pool.RemoveAt(i);
                    continue;
                }

                if (IsCoreTrioId(e.Id))
                    pool.RemoveAt(i);
            }
        }

        private static bool IsCoreTrioId(string id) =>
            string.Equals(id, WellKnownCardIds.King, StringComparison.OrdinalIgnoreCase)
            || string.Equals(id, WellKnownCardIds.Minister, StringComparison.OrdinalIgnoreCase)
            || string.Equals(id, WellKnownCardIds.Commoner, StringComparison.OrdinalIgnoreCase);

        /// <summary>友谊战敌方额外牌池：排除一次性消耗牌（表类型误标时亦不会入池）。</summary>
        private static void FilterHeroOpponentConsumablesFromPool(List<CardConfigEntry> pool)
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

        private static bool IsAllowedHeroOpponentExtraEntry(CardConfigEntry entry) =>
            entry != null && entry.Type != CardType.Consumable;

        private static void AppendRandomWithReplacement(List<Card> deck, ConfigManager cfg,
            List<CardConfigEntry> pool, int count)
        {
            if (cfg == null || pool == null || pool.Count == 0 || count <= 0)
                return;

            var indices = new List<int>(count);
            PickWithReplacementIndices(pool.Count, count, indices, UnityEngine.Random.Range);
            for (var i = 0; i < indices.Count; i++)
            {
                var entry = pool[indices[i]];
                if (!IsAllowedHeroOpponentExtraEntry(entry))
                    continue;

                var card = cfg.CreateRuntimeCard(entry);
                if (card != null)
                    deck.Add(card);
            }
        }

        private static void ShuffleInPlace(List<Card> deck)
        {
            if (deck == null || deck.Count <= 1)
                return;

            for (var i = deck.Count - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
        }
    }
}
