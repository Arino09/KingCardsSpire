using KingCardsSpire.Configs;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using KingCardsSpire.Views.UI.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>
    /// 通用卡牌列表弹窗：在带 <see cref="GridLayoutGroup"/> 的内容根下实例化 <see cref="CardView"/>，并统一 <see cref="CardView.SetScale(float)"/>。
    /// 预制体 <c>CardListView</c> 根节点挂本脚本，在 Inspector 绑定 Title、Content（Grid 父节点）与 Card 预制体。
    /// 独立打开：先 <see cref="UIManager.OpenAsync"/>（<see cref="UIPanelId.CardList"/>），再 <see cref="UIManager.TryGetView{T}"/> 取得本组件并 <see cref="Apply"/>；亦可嵌套在其他界面中直接 <see cref="Apply"/>。
    /// </summary>
    public sealed class CardListView : BaseView
    {
        private const float ListCardScale = 0.35f;

        public override void Initialize()
        {
            SetPanelId(UIPanelId.CardList);
            closeButton.onClick.AddListener(OnCloseButtonClicked);
        }

        public override void Dispose()
        {
            ClearList();
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            base.Dispose();
        }

        [SerializeField] private Text titleText;
        [SerializeField] private RectTransform cardGridRoot;
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private Button closeButton;

        /// <summary>
        /// 根据视图模型刷新标题并在网格中重建卡牌；每张卡牌缩放均为 <see cref="ListCardScale"/>。
        /// </summary>
        public void Apply(CardListViewModel model)
        {
            if (model == null)
                throw new System.ArgumentNullException(nameof(model));

            titleText.text = model.Title;

            ClearChildren(cardGridRoot);

            var cards = model.Cards;
            var count = cards.Count;
            var cfg = ConfigManager.Instance;

            for (var i = 0; i < count; i++)
            {
                var card = cards[i];
                if (card == null)
                    continue;

                var cv = Instantiate(cardPrefab, cardGridRoot, false);
                cv.SetScale(ListCardScale);
                cv.OverrideClick(null);

                if (model.FaceDown)
                {
                    cv.Clear();
                    cv.SetFaceDown(true);
                    continue;
                }

                CardConfigEntry entry = null;
                if (cfg != null && !string.IsNullOrEmpty(card.Id))
                    cfg.TryGetCard(card.Id, out entry);

                var vm = CardViewModel.FromCard(card, entry);
                cv.Apply(vm);
                if (entry != null)
                    cv.LoadCardArtFromConfig(entry);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(cardGridRoot);
        }

        /// <summary>清空网格内动态生成的卡牌实例。</summary>
        public void ClearList()
        {
            ClearChildren(cardGridRoot);
            LayoutRebuilder.ForceRebuildLayoutImmediate(cardGridRoot);
        }

        private static void ClearChildren(RectTransform row)
        {
            for (var i = row.childCount - 1; i >= 0; i--)
                Destroy(row.GetChild(i).gameObject);
        }

        private void OnCloseButtonClicked()
        {
            UIManager.Instance.Close(UIPanelId.CardList);
        }
    }
}
