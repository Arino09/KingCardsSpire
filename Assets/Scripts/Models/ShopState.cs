using System;

namespace KingCardsSpire.Models
{
    [Serializable]
    public class ShopSlotState
    {
        public string ProductId;
        public bool SoldOut;
        public int BasePrice;
    }

    /// <summary>
    /// 单层商店当日状态（文档 §4）。
    /// </summary>
    [Serializable]
    public class ShopState
    {
        public int FloorIndex;
        public int DayIndex;
        public ShopSlotState[] Slots = Array.Empty<ShopSlotState>();
    }
}
