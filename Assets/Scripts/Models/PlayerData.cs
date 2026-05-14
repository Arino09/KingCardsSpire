using System;

namespace KingCardsSpire.Models
{
    /// <summary>随存档持久化的音量设置（0～1）。</summary>
    [Serializable]
    public sealed class GameAudioSettings
    {
        public float BgmVolume = 1f;
        public float SfxVolume = 1f;
    }

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

        /// <summary>本层当前为第几天（1～MaxDaysPerFloor；<see cref="GameManager.AdvanceDay"/> 结束时 +1，超过上限且未击败 BOSS 则失败）。</summary>
        public int FloorDay = 1;
        public int Gold = 50;
        public Card[] HandCards = Array.Empty<Card>();

        /// <summary>卡牌仓库：非出战持有的牌（文档 §3.2）；与 <see cref="HandCards"/> 分区持久化。</summary>
        public Card[] StoredCards = Array.Empty<Card>();

        public Card[] DiscardPile = Array.Empty<Card>();

        /// <summary>旧版整包持有；读档后由 <see cref="GameManager"/> 迁移至 Hand+Stored 并清空。</summary>
        public Card[] OwnedCards = Array.Empty<Card>();

        /// <summary>旧版单 Buff 字段；载入后迁移至 <see cref="ActiveBuffs"/> 并清空为 None。</summary>
        public BuffId SelectedBuff = BuffId.None;

        /// <summary>本 Run 已生效的全部 Buff（可叠加）。</summary>
        public BuffId[] ActiveBuffs = Array.Empty<BuffId>();

        /// <summary>已完成几次 Buff 3 选 1（0～4，与层数里程碑对齐）。</summary>
        public int BuffPicksCompleted;

        /// <summary>下一次应弹出 Buff 草案的层数（1、3、5、7）；选完后推进为 <c>CurrentFloor + 2</c>。</summary>
        public int NextBuffOfferFloor = 1;

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

        /// <summary>当前游戏日内已完成的原住民剧情推进次数（统计用；是否可再推进仅由 <see cref="NpcDialogueCredits"/> 决定）。跨天在 <see cref="GameManager.AdvanceDay"/> 中清零。</summary>
        public int NpcStoryVisitsUsedToday;

        /// <summary>可用于推进原住民剧情的剩余次数；第一层为 6 点分 3 笔到账（开局 2 + 本层内两次休息各 2）；第 2 层起为进层与按日分期累计，未用完可跨层保留。</summary>
        public int NpcDialogueCredits;

        /// <summary>本层尚未通过「结束当日」发放的原住民分期笔数（第一层与更高层均为 2：对应第 2、第 3 笔到账）。第一层开局已发首笔。</summary>
        public int NpcCreditInstallmentsRemaining;

        /// <summary>音乐 / 音效音量；旧档无此字段时由 GameManager 规范化。</summary>
        public GameAudioSettings AudioSettings;
    }
}
