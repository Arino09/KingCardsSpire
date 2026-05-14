using System;

namespace KingCardsSpire.Models
{
    [Serializable]
    public class SaveData
    {
        /// <summary>存档结构版本；递增时配合载入迁移逻辑。</summary>
        public const int CurrentSchemaVersion = 5;

        public int Version = CurrentSchemaVersion;
        public PlayerData Player = new();
        public FloorState Floor = new();
        public ShopState Shop = new();
        public HistoryRecord[] History = Array.Empty<HistoryRecord>();
    }
}
