namespace KingCardsSpire.Models
{
    public enum CardType
    {
        Basic = 0,
        Function = 1,
        Ability = 2,
        Consumable = 3
    }

    /// <summary>战场与世界天气；晴天（Clear）仅新手教程使用，不参与每日天气随机池。</summary>
    public enum WeatherType
    {
        Rainy = 0,
        Sunny = 1,
        Hail = 2,
        WarmWind = 3,
        Ending = 4,

        /// <summary>晴天：无等级修正；仅教学阶段使用。</summary>
        Clear = 5
    }

    /// <summary>
    /// 初始 Buff 池（文档 §2.4）。
    /// </summary>
    public enum BuffId
    {
        None = 0,
        RichSecondGen = 1,
        UnlimitedSupply = 2,
        RandomCommoner = 3,
        RandomKing = 4,
        SurprisePack = 5,
        XRayBoost = 6,
        ChaoticBattlefield = 7
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
        CardList = 10,
        NpcHub = 11,
        Dialog = 12,
        BuffRewardView = 13,
        DeckStorage = 14,
        Settings = 15,
        NpcRecord = 16,
        DialogueRecord = 17,
        Album = 18,
        GameOver = 19
    }
}
