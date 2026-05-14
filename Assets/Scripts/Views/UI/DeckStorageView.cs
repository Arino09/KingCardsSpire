using System;
using System.Collections.Generic;
using KingCardsSpire.Configs;
using KingCardsSpire.Core.Battle;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using KingCardsSpire.Views.UI.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>
    /// 牌组仓库交换：左侧出战 <see cref="PlayerData.HandCards"/>，右侧仓库 <see cref="PlayerData.StoredCards"/>；
    /// 列标题显示「出战卡组（当前/10）」「卡牌仓库（当前/10）」。预制体根挂本脚本并在 Inspector 绑定 Content 与 Card 预制体。
    /// </summary>
    public sealed class DeckStorageView : BaseView
    {
        private const float ListCardScale = 0.3f;

        [SerializeField] private RectTransform activeDeckGridRoot;
        [SerializeField] private RectTransform storageGridRoot;
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private Button closeButton;

        private DeckStorageViewModel _viewModel;

        public override void Initialize()
        {
            SetPanelId(UIPanelId.DeckStorage);
            var gm = GameManager.Instance;
            _viewModel = new DeckStorageViewModel(
                () => gm.PlayerState.HandCards ?? Array.Empty<Card>(),
                () => gm.PlayerState.StoredCards ?? Array.Empty<Card>(),
                gm.TryMoveCardBetweenDeckAndStorage);
            closeButton.onClick.AddListener(OnCloseClicked);
        }

        public override void Dispose()
        {
            ClearGrid(activeDeckGridRoot);
            ClearGrid(storageGridRoot);
            closeButton.onClick.RemoveListener(OnCloseClicked);
            base.Dispose();
        }

        protected override void OnOpen()
        {
            RefreshGrids();
        }

        /// <summary>在打开面板后或外部需要时重建两侧列表。</summary>
        public void RefreshGrids()
        {
            if (_viewModel == null)
                return;

            RefreshColumnHeaderTexts();
            RebuildSide(activeDeckGridRoot, _viewModel.ActiveCards, true);
            RebuildSide(storageGridRoot, _viewModel.StorageCards, false);
        }

        /// <summary>
        /// 列标题为「出战卡组（当前/上限）」「卡牌仓库（当前/上限）」；依赖预制体结构：列表根为 ScrollView/Viewport/Content，
        /// 其再上两级父节点为列根，且列根第一个子物体带 <see cref="Text"/>。
        /// </summary>
        private void RefreshColumnHeaderTexts()
        {
            var hand = _viewModel.ActiveCards?.Count ?? 0;
            var store = _viewModel.StorageCards?.Count ?? 0;

            TrySetScrollColumnHeaderText(activeDeckGridRoot, hand, GameManager.MaxBattleDeckCards, "出战卡组");
            TrySetScrollColumnHeaderText(storageGridRoot, store, GameManager.MaxStorageCards, "卡牌仓库");
        }

        private static void TrySetScrollColumnHeaderText(RectTransform gridRoot, int currentCount, int maxCount,
            string baseLabel)
        {
            if (gridRoot == null || string.IsNullOrEmpty(baseLabel))
                return;

            var column = gridRoot.parent?.parent?.parent as RectTransform;
            if (column == null || column.childCount < 1)
                return;

            var titleTransform = column.GetChild(0);
            var text = titleTransform.GetComponent<Text>();
            if (text == null)
                return;

            text.text = $"{baseLabel}（{currentCount}/{maxCount}）";
        }

        private void RebuildSide(RectTransform root, IReadOnlyList<Card> cards, bool isActiveSide)
        {
            ClearGrid(root);
            var cfg = ConfigManager.Instance;
            var count = cards.Count;

            for (var i = 0; i < count; i++)
            {
                var card = cards[i];
                if (card == null)
                    continue;

                var cv = Instantiate(cardPrefab, root, false);
                cv.SetScale(ListCardScale);
                var capturedIndex = i;
                var fromActive = isActiveSide;

                CardConfigEntry entry = null;
                if (cfg != null && !string.IsNullOrEmpty(card.Id))
                    cfg.TryGetCard(card.Id, out entry);

                var vm = CardViewModel.FromCard(card, entry);
                cv.Apply(vm);
                if (entry != null)
                    cv.LoadCardArtFromConfig(entry);

                var lockedOnActive = isActiveSide &&
                                     (CardBattleRules.IsKing(card) || CardBattleRules.IsCommoner(card));
                if (lockedOnActive)
                {
                    cv.OverrideClick(null);
                    cv.SetVisualState(CardVisualState.Disabled);
                }
                else
                {
                    cv.OverrideClick(() =>
                    {
                        var ok = fromActive
                            ? _viewModel.TryMoveActiveToStorage(capturedIndex)
                            : _viewModel.TryMoveStorageToActive(capturedIndex);
                        if (ok)
                            RefreshGrids();
                    });
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }

        private static void ClearGrid(RectTransform row)
        {
            if (row == null)
                return;
            for (var i = row.childCount - 1; i >= 0; i--)
                Destroy(row.GetChild(i).gameObject);
        }

        private void OnCloseClicked()
        {
            UIManager.Instance?.Close(UIPanelId.DeckStorage);
        }
    }
}
