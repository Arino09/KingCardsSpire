namespace KingCardsSpire.Models
{
    public enum CardType
    {
        Basic = 0,
        Function = 1,
        Ability = 2,
        Consumable = 3
    }

    public enum WeatherType
    {
        Rainy = 0,
        Sunny = 1,
        Hail = 2,
        WarmWind = 3,
        Ending = 4
    }

    /// <summary>
    /// 初始 Buff 十选一（文档 §2.4）。
    /// </summary>
    public enum BuffId
    {
        None = 0,
        Socialite = 1,
        RichSecondGen = 2,
        UnlimitedSupply = 3,
        RandomCommoner = 4,
        RandomKing = 5,
        SurprisePack = 6,
        HighSalaryJob = 7,
        ThiefInstinct = 8,
        XRayBoost = 9,
        ChaoticBattlefield = 10
    }

    public enum UIPanelId
    {
        None = 0,
        TopStatus = 1,
        DailyChoice = 2,
        HeroRoom = 3,
        Shop = 4,
        UnlockedDialogues = 5,
        Battle = 6,
        MainMenu = 7,
        MainHub = 8,
        CardReward = 9,
        CardList = 10
    }
}
