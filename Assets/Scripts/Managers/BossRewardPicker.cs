using System.Collections.Generic;
using KingCardsSpire.Configs;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Managers
{
    /// <summary>
    /// 驻守者奖励：两档 SpareDay 相关金币 + 至多 5 张从本局 BOSS 卡组（手牌+弃牌）筛出的卡牌；卡牌不足时不补金币。卡牌列表由战斗结束事件携带。
    /// </summary>
    public static class BossRewardPicker
    {
        public static BossRewardOption[] Generate(int spareDays, TowerFloorEntry floorOrNull,
            ConfigManager cfg, IReadOnlyList<string> bossVictoryRewardCardIdsOrNull)
        {
            var floor = floorOrNull;
            var goldBase = Mathf.Max(1, floor?.GoldBonusPerSpareDay ?? 5) * Mathf.Max(1, spareDays + 1);

            var options = new List<BossRewardOption>();
            var usedKeys = new HashSet<string>();

            options.Add(MakeGold(goldBase, usedKeys));
            options.Add(MakeGold(goldBase + 15, usedKeys));

            if (bossVictoryRewardCardIdsOrNull != null && cfg != null)
            {
                for (var i = 0; i < bossVictoryRewardCardIdsOrNull.Count; i++)
                {
                    var cardId = bossVictoryRewardCardIdsOrNull[i];
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
