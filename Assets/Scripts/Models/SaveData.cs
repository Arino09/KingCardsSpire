using System;

namespace KingCardsSpire.Models
{
    [Serializable]
    public class SaveData
    {
        public int Version = 2;
        public PlayerData Player = new();
        public FloorState Floor = new();
        public ShopState Shop = new();
        public HistoryRecord[] History = Array.Empty<HistoryRecord>();
    }
}
