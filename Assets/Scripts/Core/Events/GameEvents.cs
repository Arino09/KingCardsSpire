using KingCardsSpire.Core.Battle;
using KingCardsSpire.Models;

namespace KingCardsSpire.Core.Events
{
    public readonly struct GameBootedEvent : IEvent { }

    public readonly struct GameStartedEvent : IEvent { }

    public readonly struct DayChangedEvent : IEvent
    {
        public readonly int Day;
        public DayChangedEvent(int day) => Day = day;
    }

    public readonly struct FloorChangedEvent : IEvent
    {
        public readonly int Floor;
        public FloorChangedEvent(int floor) => Floor = floor;
    }

    public readonly struct GoldChangedEvent : IEvent
    {
        public readonly int Gold;
        public GoldChangedEvent(int gold) => Gold = gold;
    }

    public readonly struct CardAcquiredEvent : IEvent
    {
        public readonly string CardId;
        public CardAcquiredEvent(string cardId) => CardId = cardId;
    }

    public readonly struct BuffAcquiredEvent : IEvent
    {
        public readonly BuffId BuffId;
        public BuffAcquiredEvent(BuffId buffId) => BuffId = buffId;
    }

    public readonly struct BattleStartedEvent : IEvent { }

    public readonly struct BattleStateChangedEvent : IEvent { }

    public readonly struct BattleRoundResolvedEvent : IEvent
    {
        public readonly string Summary;
        public BattleRoundResolvedEvent(string summary) => Summary = summary;
    }

    public readonly struct BattleEndedEvent : IEvent
    {
        public readonly bool PlayerVictory;
        public readonly BattleEndReason Reason;
        public readonly bool IsBossBattle;

        /// <summary>本局是否打出过金色项链（获胜后可由经济系统用于金币倍率）。</summary>
        public readonly bool GoldenNecklacePlayedThisBattle;

        /// <summary>
        /// 驻守 BOSS 战胜时：从本局 BOSS 卡组（手牌+弃牌）筛出的卡牌奖励候选 Id（至多 5，已排除国王/平民）；否则为 <c>null</c>。
        /// </summary>
        public readonly string[] BossVictoryRewardCardIds;

        /// <summary>主角房友谊赛；与 <see cref="HeroRoomDuelSlotIndex"/> 配合用于胜场奖励分支。</summary>
        public readonly bool IsHeroRoomDuel;

        /// <summary>友谊赛对应参赛者槽位 0～2；非友谊战或未解析时为 -1。</summary>
        public readonly int HeroRoomDuelSlotIndex;

        public BattleEndedEvent(bool playerVictory, BattleEndReason reason, bool isBossBattle = false,
            bool goldenNecklacePlayedThisBattle = false, string[] bossVictoryRewardCardIds = null,
            bool isHeroRoomDuel = false, int heroRoomDuelSlotIndex = -1)
        {
            PlayerVictory = playerVictory;
            Reason = reason;
            IsBossBattle = isBossBattle;
            GoldenNecklacePlayedThisBattle = goldenNecklacePlayedThisBattle;
            BossVictoryRewardCardIds = bossVictoryRewardCardIds;
            IsHeroRoomDuel = isHeroRoomDuel;
            HeroRoomDuelSlotIndex = heroRoomDuelSlotIndex;
        }
    }

    /// <summary>新档开场教学战流程结束（含战败对白与 GameOverView 等）；由 <see cref="Views.UI.BattleView"/> 发布，<see cref="Views.UI.MainMenuView"/> 等待后分流 Hub / 主菜单。</summary>
    public readonly struct OpeningTutorialBattleFlowCompletedEvent : IEvent
    {
        public readonly bool PlayerVictory;

        public OpeningTutorialBattleFlowCompletedEvent(bool playerVictory) => PlayerVictory = playerVictory;
    }

    public readonly struct WeatherChangedEvent : IEvent
    {
        public readonly WeatherType Weather;
        public WeatherChangedEvent(WeatherType weather) => Weather = weather;
    }

    public readonly struct GameOverEvent : IEvent
    {
        public readonly string Reason;
        public GameOverEvent(string reason) => Reason = reason;
    }

    public readonly struct GameVictoryEvent : IEvent { }

    public readonly struct BossRewardOfferedEvent : IEvent
    {
        public readonly int FloorIndex;
        public readonly BossRewardOption[] Options;

        public BossRewardOfferedEvent(int floorIndex, BossRewardOption[] options)
        {
            FloorIndex = floorIndex;
            Options = options;
        }
    }

    public readonly struct SaveLoadedEvent : IEvent { }

    public readonly struct SaveWrittenEvent : IEvent { }

    /// <summary>大结局对白链激活状态（供 BGM 导演层优先播放结局曲）。</summary>
    public readonly struct EndingStoryBgmActiveEvent : IEvent
    {
        public readonly bool Active;

        public EndingStoryBgmActiveEvent(bool active) => Active = active;
    }

    /// <summary>玩家当日 NPC 访问已消耗且进入对话占位流程（对话系统未实装前供 UI 刷新等订阅）。</summary>
    public readonly struct NpcEncounterStartedEvent : IEvent
    {
        public readonly string NpcId;

        public NpcEncounterStartedEvent(string npcId) => NpcId = npcId ?? string.Empty;
    }
}
