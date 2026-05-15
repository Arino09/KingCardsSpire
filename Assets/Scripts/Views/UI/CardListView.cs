using System;
using System.Collections.Generic;
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

        private readonly HashSet<int> _selectedIndices = new();

        private CardListViewModel _model;

        public override void Initialize()
        {
            SetPanelId(UIPanelId.CardList);
            closeButton.onClick.AddListener(OnCloseButtonClicked);
        }

        public override void Dispose()
        {
            ClearList();
            TeardownSelectionListeners();
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            base.Dispose();
        }

        [SerializeField] private Text titleText;
        [SerializeField] private RectTransform cardGridRoot;
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private Button closeButton;

        [SerializeField] private Button confirmSelectionButton;
        [SerializeField] private Text selectionHintText;

        /// <summary>
        /// 根据视图模型刷新标题并在网格中重建卡牌；每张卡牌缩放均为 <see cref="ListCardScale"/>。
        /// </summary>
        public void Apply(CardListViewModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            TeardownSelectionListeners();

            _model = model;
            _selectedIndices.Clear();
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
                var capturedIndex = i;

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

                if (model.MaxSelectable > 0)
                {
                    cv.OverrideClick(() => OnSelectableCardClicked(capturedIndex, cv));
                    cv.SetVisualState(_selectedIndices.Contains(capturedIndex)
                        ? CardVisualState.Selected
                        : CardVisualState.Normal);
                }
                else
                    cv.OverrideClick(null);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(cardGridRoot);

            confirmSelectionButton.gameObject.SetActive(model.MaxSelectable > 0);
            if (model.MaxSelectable > 0)
            {
                EnsureSelectionChrome(model);
                WireSelectionListeners();
                UpdateSelectButtonInteractable();
            }
        }

        private void OnSelectableCardClicked(int index, CardView cv)
        {
            if (_model == null || _model.MaxSelectable <= 0 || cv == null)
                return;

            if (_selectedIndices.Contains(index))
                _selectedIndices.Remove(index);
            else
            {
                if (_selectedIndices.Count >= _model.MaxSelectable)
                    return;
                _selectedIndices.Add(index);
            }

            cv.SetVisualState(_selectedIndices.Contains(index) ? CardVisualState.Selected : CardVisualState.Normal);
            UpdateSelectButtonInteractable();
        }

        private void EnsureSelectionChrome(CardListViewModel model)
        {
            var hint = string.IsNullOrEmpty(model.SelectionHint)
                ? $"选择 {model.MaxSelectable} 张牌"
                : model.SelectionHint;
            selectionHintText.text = hint;
        }

        private void WireSelectionListeners()
        {
            confirmSelectionButton.onClick.AddListener(OnConfirmSelectionClicked);
        }

        private void TeardownSelectionListeners()
        {
            confirmSelectionButton.onClick.RemoveListener(OnConfirmSelectionClicked);
        }

        private void OnConfirmSelectionClicked()
        {
            UiButtonSfx.PlayDefaultClick();
            if (_model == null || _model.MaxSelectable <= 0)
                return;
            if (_selectedIndices.Count != _model.MaxSelectable)
                return;

            var cards = _model.Cards;
            var list = new List<Card>(_model.MaxSelectable);
            foreach (var idx in _selectedIndices)
            {
                if (idx < 0 || idx >= cards.Count)
                    continue;
                var src = cards[idx];
                if (src == null)
                    continue;
                list.Add(CloneRuntimeCard(src));
            }

            if (list.Count != _model.MaxSelectable)
                return;

            _model.OnConfirmSelection?.Invoke(list);
            UIManager.Instance?.Close(UIPanelId.CardList);
        }

        private void UpdateSelectButtonInteractable()
        {
            if (_model == null || _model.MaxSelectable <= 0)
                return;
            confirmSelectionButton.interactable = _selectedIndices.Count == _model.MaxSelectable;
        }

        private static Card CloneRuntimeCard(Card src) =>
            new Card
            {
                Id = src.Id,
                Name = src.Name,
                Level = src.Level,
                Type = src.Type,
                EffectDesc = src.EffectDesc,
                IsUnique = src.IsUnique,
                BattleInstanceId = src.BattleInstanceId,
                DeckInstanceId = src.DeckInstanceId ?? string.Empty
            };

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
            UiButtonSfx.PlayDefaultClick();
            _model?.OnCancelSelection?.Invoke();
            UIManager.Instance?.Close(UIPanelId.CardList);
        }
    }
}
