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
        [Tooltip("新游戏时写入 PlayerData.OwnedCards 的卡牌 Id 列表（须与 Card 配置表一致）。留空则开局持有卡组为空（战斗仍会使用 BattleManager 内置测试卡组兜底）。")]
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
