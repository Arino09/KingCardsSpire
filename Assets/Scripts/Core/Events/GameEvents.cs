using KingCardsSpire.Core.Battle;

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

        public BattleEndedEvent(bool playerVictory, BattleEndReason reason)
        {
            PlayerVictory = playerVictory;
            Reason = reason;
        }
    }

    public readonly struct WeatherChangedEvent : IEvent { }

    public readonly struct SaveLoadedEvent : IEvent { }

    public readonly struct SaveWrittenEvent : IEvent { }
}
