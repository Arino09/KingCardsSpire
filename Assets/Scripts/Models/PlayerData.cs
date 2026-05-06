using System;

namespace KingCardsSpire.Models
{
    /// <summary>
    /// 玩家存档相关数据（文档 §8.2 与塔机制）。
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        public int CurrentFloor = 1;
        public int CurrentDay = 1;
        public int FloorDay;
        public int Gold = 50;
        public Card[] HandCards = Array.Empty<Card>();
        public Card[] DiscardPile = Array.Empty<Card>();
        public Card[] OwnedCards = Array.Empty<Card>();
        public BuffId SelectedBuff = BuffId.None;
        public int XRayCount = 1;
        public WeatherType CurrentWeather = WeatherType.WarmWind;
        public string[] UnlockedDialogues = Array.Empty<string>();
        public string[] UnlockedAchievements = Array.Empty<string>();
    }
}
