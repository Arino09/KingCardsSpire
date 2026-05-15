using System;
using UnityEngine;

namespace KingCardsSpire.Configs
{
    /// <summary>
    /// 单层塔配置（BOSS Id、NPC、驻守金币档位、BOSS AI 强度等）。
    /// 驻守 BOSS 敌方卡组由 <see cref="KingCardsSpire.Core.Battle.BossDeckGenerator"/> 按层随机生成；
    /// <see cref="EnemyDeckCardIds"/> 保留供编辑器占位或将来非 BOSS 覆写，运行时 BOSS 战不读取该列表组卡。
    /// </summary>
    [Serializable]
    public class TowerFloorEntry
    {
        [SerializeField] private string bossId = "boss_placeholder";
        [SerializeField] private string[] enemyDeckCardIds = Array.Empty<string>();
        [SerializeField] private string[] npcIds = Array.Empty<string>();
        [SerializeField] private int bossAiStrength;

        /// <summary>历史字段：驻守静默金币现为代码内 <c>SpareDay * 5</c>，本值当前未参与计算。</summary>
        [SerializeField] private int goldBonusPerSpareDay = 5;

        public string BossId => bossId;
        public string[] EnemyDeckCardIds => enemyDeckCardIds ?? Array.Empty<string>();
        public string[] NpcIds => npcIds ?? Array.Empty<string>();
        public int BossAiStrength => Mathf.Max(0, bossAiStrength);
        public int GoldBonusPerSpareDay => goldBonusPerSpareDay;
    }
}
