using System;

namespace KingCardsSpire.Models
{
    [Serializable]
    public sealed class NpcDialogueProgress
    {
        public string NpcId;
        public int CompletedCount;
    }

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
        public int[] HeroDialogueProgress = Array.Empty<int>();
        public NpcDialogueProgress[] NpcDialogueProgress = Array.Empty<NpcDialogueProgress>();
        public int LastHeroDialogueDay;

        /// <summary>新游戏开场教程对话是否已播完（继续游戏从存档恢复）。</summary>
        public bool HasCompletedOpeningTutorial;

        /// <summary>已通过 NPC 界面结识的原住民 Id（与塔层 npcIds 对应）。</summary>
        public string[] MetNpcIds = Array.Empty<string>();

        /// <summary>上一次消耗「当日 NPC 访问」的游戏内天数；与 <see cref="CurrentDay"/> 相等表示今日已访问过 NPC。</summary>
        public int LastNpcInteractionDay;

        /// <summary>可用于推进原住民剧情的剩余次数；每进入新层增加 3 次，可跨天保留。</summary>
        public int NpcDialogueCredits;
    }
}
