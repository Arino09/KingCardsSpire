using System.Collections.Generic;
using KingCardsSpire.Configs;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Managers
{
    /// <summary>
    /// 驻守者 5 选 1 奖励生成（占位档位：spareDays 越大金币档位越高）。
    /// </summary>
    public static class BossRewardPicker
    {
        private const int OptionCount = 5;

        public static BossRewardOption[] Generate(int spareDays, TowerFloorEntry floorOrNull,
            ConfigManager cfg)
        {
            var floor = floorOrNull;
            var pool = floor?.RewardCardPoolIds;
            var goldBase = Mathf.Max(1, floor?.GoldBonusPerSpareDay ?? 5) * Mathf.Max(1, spareDays + 1);

            var options = new List<BossRewardOption>();
            var usedKeys = new HashSet<string>();

            options.Add(MakeGold(goldBase, usedKeys));
            options.Add(MakeGold(goldBase + 15, usedKeys));

            if (pool != null && cfg != null)
            {
                foreach (var cardId in pool)
                {
                    if (options.Count >= OptionCount)
                        break;
                    if (string.IsNullOrEmpty(cardId) || !cfg.TryGetCard(cardId, out var cc))
                        continue;
                    var key = "c:" + cardId;
                    if (!usedKeys.Add(key))
                        continue;
                    options.Add(new BossRewardOption
                    {
                        IsGold = false,
                        GoldAmount = 0,
                        CardId = cc.Id,
                        CardDisplayName = cc.DisplayName
                    });
                }
            }

            while (options.Count < OptionCount)
                options.Add(MakeGold(goldBase + 8 * options.Count, usedKeys));

            return options.ToArray();
        }

        private static BossRewardOption MakeGold(int amount, HashSet<string> usedKeys)
        {
            var a = amount;
            var key = "g:" + a;
            while (!usedKeys.Add(key))
            {
                a += 3;
                key = "g:" + a;
            }

            return new BossRewardOption { IsGold = true, GoldAmount = a };
        }
    }
}
