using KingCardsSpire.Configs;
using KingCardsSpire.Views.UI.Cards;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>商店单列：卡牌展示 + 价格 + 购买（卡牌点击不与购买重复触发）。</summary>
    public sealed class ShopSlotRowView : MonoBehaviour
    {
        [SerializeField] private GameObject soldOutOverlay;
        [SerializeField] private CardView cardView;
        [SerializeField] private Text priceText;
        [SerializeField] private Button buyButton;

        private UnityAction _wrappedBuy;

        public void Bind(CardConfigEntry cfg, int price, bool soldOut, bool unlimitedShelf, float cardScale, UnityAction onBuy)
        {
            cardView.SetScale(cardScale);
            cardView.OverrideClick(() => { });

            var showSoldOut = soldOut && !unlimitedShelf;
            soldOutOverlay.SetActive(showSoldOut);

            priceText.gameObject.SetActive(!showSoldOut);
            if (!showSoldOut)
                priceText.text = price.ToString();

            var canBuy = cfg != null && (!soldOut || unlimitedShelf);
            buyButton.onClick.RemoveListener(WrappedBuy);
            _wrappedBuy = onBuy;
            buyButton.onClick.AddListener(WrappedBuy);
            buyButton.interactable = canBuy;

            if (cfg != null)
            {
                cardView.SetFaceDown(false);
                cardView.Apply(CardViewModel.FromConfigOnly(cfg));
                cardView.LoadCardArtFromConfig(cfg);
            }
            else
                cardView.Clear();
        }

        private void WrappedBuy()
        {
            UiButtonSfx.PlayDefaultClick();
            _wrappedBuy?.Invoke();
        }

        private void OnDestroy()
        {
            buyButton.onClick.RemoveListener(WrappedBuy);
        }
    }
}
