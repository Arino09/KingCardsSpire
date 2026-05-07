using System;
using UnityEngine;

namespace KingCardsSpire.Configs
{
    /// <summary>
    /// 单层塔配置（BOSS、敌方卡组 Id、驻守奖励池等）。
    /// </summary>
    [Serializable]
    public class TowerFloorEntry
    {
        [SerializeField] private string bossId = "boss_placeholder";
        [SerializeField] private string[] enemyDeckCardIds = Array.Empty<string>();
        [SerializeField] private string[] npcIds = Array.Empty<string>();
        [SerializeField] private string[] rewardCardPoolIds = Array.Empty<string>();

        /// <summary>驻守奖励金币档位：按 spareDays 放大时的基数（占位）。</summary>
        [SerializeField] private int goldBonusPerSpareDay = 5;

        public string BossId => bossId;
        public string[] EnemyDeckCardIds => enemyDeckCardIds ?? Array.Empty<string>();
        public string[] NpcIds => npcIds ?? Array.Empty<string>();
        public string[] RewardCardPoolIds => rewardCardPoolIds ?? Array.Empty<string>();
        public int GoldBonusPerSpareDay => goldBonusPerSpareDay;
    }
}
