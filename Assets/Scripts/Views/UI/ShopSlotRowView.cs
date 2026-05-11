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
        [Header("售罄")]
        [Tooltip("售罄时在卡牌区域上显示的遮罩（由预制体挂载 Image/Panel 等）。")]
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
            if (soldOutOverlay != null)
                soldOutOverlay.SetActive(showSoldOut);

            priceText.gameObject.SetActive(!showSoldOut);
            if (!showSoldOut)
                priceText.text = $"${price}";

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
            _wrappedBuy?.Invoke();
        }

        private void OnDestroy()
        {
            buyButton.onClick.RemoveListener(WrappedBuy);
        }
    }
}
