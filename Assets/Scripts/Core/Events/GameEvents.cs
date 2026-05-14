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

        public BattleEndedEvent(bool playerVictory, BattleEndReason reason, bool isBossBattle = false,
            bool goldenNecklacePlayedThisBattle = false)
        {
            PlayerVictory = playerVictory;
            Reason = reason;
            IsBossBattle = isBossBattle;
            GoldenNecklacePlayedThisBattle = goldenNecklacePlayedThisBattle;
        }
    }

    /// <summary>新档开场教学战流程结束（含战败对话与占位 UI 后）；由 <see cref="Views.UI.BattleView"/> 发布，<see cref="Views.UI.MainMenuView"/> 等待后分流 Hub / 主菜单。</summary>
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

    /// <summary>玩家当日 NPC 访问已消耗且进入对话占位流程（对话系统未实装前供 UI 刷新等订阅）。</summary>
    public readonly struct NpcEncounterStartedEvent : IEvent
    {
        public readonly string NpcId;

        public NpcEncounterStartedEvent(string npcId) => NpcId = npcId ?? string.Empty;
    }
}
