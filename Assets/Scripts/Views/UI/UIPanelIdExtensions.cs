using KingCardsSpire.Models;

namespace KingCardsSpire.Views.UI
{
    public static class UIPanelIdExtensions
    {
        /// <summary>
        /// 将面板枚举映射为 Addressables 地址键（与资源命名一致）。
        /// </summary>
        public static string ToAddress(this UIPanelId id)
        {
            return id switch
            {
                UIPanelId.Shop => "UI/ShopView",
                UIPanelId.UnlockedDialogues => "UI/UnlockedDialoguesView",
                UIPanelId.Battle => "UI/BattleView",
                UIPanelId.MainMenu => "UI/MainMenuView",
                UIPanelId.MainHub => "UI/MainView",
                UIPanelId.CardReward => "UI/CardRewardView",
                UIPanelId.CardList => "UI/CardListView",
                UIPanelId.NpcHub => "UI/NPCView",
                UIPanelId.Dialog => "UI/DialogView",
                UIPanelId.HeroRoom => "UI/HeroView",
                UIPanelId.BuffRewardView => "UI/BuffRewardView",
                UIPanelId.DeckStorage => "UI/DeckStorageView",
                UIPanelId.Settings => "UI/Settings",
                _ => string.Empty
            };
        }
    }
}
