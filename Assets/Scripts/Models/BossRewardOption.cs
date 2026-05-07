using System;

namespace KingCardsSpire.Models
{
    /// <summary>
    /// 驻守者通关奖励的一条选项（金币或卡牌），供 JsonUtility 与事件携带。
    /// </summary>
    [Serializable]
    public class BossRewardOption
    {
        public bool IsGold;
        public int GoldAmount;
        public string CardId;
        public string CardDisplayName;
    }
}
