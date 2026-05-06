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
                UIPanelId.TopStatus => "UI/Panel_TopStatus",
                UIPanelId.DailyChoice => "UI/Panel_DailyChoice",
                UIPanelId.HeroRoom => "UI/Panel_HeroRoom",
                UIPanelId.Shop => "UI/Panel_Shop",
                UIPanelId.UnlockedDialogues => "UI/Panel_UnlockedDialogues",
                UIPanelId.Battle => "UI/Panel_Battle",
                _ => string.Empty
            };
        }
    }
}
