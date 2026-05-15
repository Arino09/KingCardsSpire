namespace KingCardsSpire.Core
{
    /// <summary>
    /// Addressables 主地址键（与 Unity 内 AudioClip 条目地址一致）；BGM 建议打标签 <c>audio_bgm</c>，音效 <c>audio_sfx</c>。
    /// </summary>
    public static class AudioAddressKeys
    {
        public const string BgmMainMenu = "Audio/BGM/MainMenu";
        public const string BgmTutorial = "Audio/BGM/Tutorial";
        public const string BgmHubTierLow = "Audio/BGM/Hub_TierLow";
        public const string BgmHubTierMid = "Audio/BGM/Hub_TierMid";
        public const string BgmHubTierHigh = "Audio/BGM/Hub_TierHigh";
        public const string BgmBattle = "Audio/BGM/Battle";
        public const string BgmEnding = "Audio/BGM/Ending";

        public const string SfxUiButton = "Audio/SFX/UiButton";
        public const string SfxConfirmPlayCard = "Audio/SFX/ConfirmPlayCard";
        public const string SfxCardToDiscard = "Audio/SFX/CardToDiscard";
    }
}
