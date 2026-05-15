using KingCardsSpire.Core;
using KingCardsSpire.Managers;

namespace KingCardsSpire.Views.UI
{
    /// <summary>通用 UI 按钮按下音效（Addressables 键见 <see cref="AudioAddressKeys.SfxUiButton"/>）。</summary>
    public static class UiButtonSfx
    {
        public static void PlayDefaultClick()
        {
            AudioManager.Instance?.PlaySfx(AudioAddressKeys.SfxUiButton);
        }
    }
}
