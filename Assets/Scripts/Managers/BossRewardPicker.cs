using System.Collections.Generic;
using KingCardsSpire.Configs;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Managers
{
    /// <summary>
    /// 驻守者卡牌奖励选项（至多 5 张，来自战斗结束事件携带的敌方手牌+弃牌候选 Id）；静默金币为 <see cref="ComputeSpareDayBossSilentGold"/>，不进本列表。
    /// </summary>
    public static class BossRewardPicker
    {
        /// <summary>驻守战胜静默金币：<c>SpareDay * 5</c>（<paramref name="spareDays"/> 为层内剩余日数语义，由调用方计算）。</summary>
        public static int ComputeSpareDayBossSilentGold(int spareDays) => Mathf.Max(0, spareDays) * 5;

        public static BossRewardOption[] GenerateCardBossRewardOptions(ConfigManager cfg,
            IReadOnlyList<string> bossVictoryRewardCardIdsOrNull)
        {
            var options = new List<BossRewardOption>();
            var usedKeys = new HashSet<string>();

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
    }
}
