using System;
using UnityEngine;

namespace KingCardsSpire.Configs
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "KingCardsSpire/Configs/Game", order = 4)]
    public class GameConfig : ScriptableObject
    {
        [SerializeField] private string id = "default";
        [SerializeField] private int initialGold = 50;
        [SerializeField] private int towerFloors = 7;
        [SerializeField] private int maxDaysPerFloor = 3;
        [SerializeField] private int initialXRayCount = 1;

        [Header("开局卡组")]
        [Tooltip("新游戏时按顺序写入：前 10 张进入出战卡组 PlayerData.HandCards，其后至多 10 张进入仓库（出战+仓库合计至多 20 张）；超出部分不会写入。留空则开局出战与仓库均为空。")]
        [SerializeField] private string[] starterDeckCardIds = Array.Empty<string>();

        public string Id => id;
        public int InitialGold => initialGold;
        public int TowerFloors => towerFloors;
        public int MaxDaysPerFloor => maxDaysPerFloor;
        public int InitialXRayCount => initialXRayCount;

        /// <summary>新开局时填充「持有卡组」的卡牌 Id（可为空）。</summary>
        public string[] StarterDeckCardIds => starterDeckCardIds ?? Array.Empty<string>();
    }
}
