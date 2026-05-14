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

        private RectTransform _selectionFooterRoot;

        private Button _runtimeConfirmButton;

        private Button _runtimeCancelButton;

        private Button _boundConfirmButton;

        private Button _boundCancelButton;

        public override void Initialize()
        {
            SetPanelId(UIPanelId.CardList);
            closeButton.onClick.AddListener(OnCloseButtonClicked);
        }

        public override void Dispose()
        {
            ClearList();
            TeardownSelectionListeners();
            DestroyRuntimeFooter();
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            base.Dispose();
        }

        [SerializeField] private Text titleText;
        [SerializeField] private RectTransform cardGridRoot;
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private Button closeButton;

        [Header("多选（混乱战场等；可选 Inspector 绑定，未绑定时运行时生成）")]
        [SerializeField] private Button confirmSelectionButton;

        [SerializeField] private Button cancelSelectionButton;

        [SerializeField] private Text selectionHintText;

        /// <summary>
        /// 根据视图模型刷新标题并在网格中重建卡牌；每张卡牌缩放均为 <see cref="ListCardScale"/>。
        /// </summary>
        public void Apply(CardListViewModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            TeardownSelectionListeners();
            DestroyRuntimeFooter();

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

            if (model.MaxSelectable > 0)
            {
                EnsureSelectionChrome(model);
                WireSelectionListeners();
                UpdateSelectionFooterInteractable();
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
            UpdateSelectionFooterInteractable();
        }

        private void EnsureSelectionChrome(CardListViewModel model)
        {
            var hint = string.IsNullOrEmpty(model.SelectionHint)
                ? $"请点选恰好 {model.MaxSelectable} 张卡牌"
                : model.SelectionHint;

            if (selectionHintText != null)
                selectionHintText.text = hint;

            if (confirmSelectionButton != null && cancelSelectionButton != null)
            {
                _boundConfirmButton = confirmSelectionButton;
                _boundCancelButton = cancelSelectionButton;
                return;
            }

            var footerGo = new GameObject("SelectionFooter", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            footerGo.transform.SetParent(transform, false);
            _selectionFooterRoot = footerGo.GetComponent<RectTransform>();
            StretchBottomBar(_selectionFooterRoot, 72f);
            var h = footerGo.GetComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 16f;
            h.padding = new RectOffset(24, 24, 8, 8);
            h.childControlHeight = true;
            h.childControlWidth = false;
            h.childForceExpandHeight = true;

            if (selectionHintText == null)
            {
                var hintGo = new GameObject("Hint", typeof(RectTransform), typeof(Text));
                hintGo.transform.SetParent(_selectionFooterRoot, false);
                var le = hintGo.AddComponent<LayoutElement>();
                le.preferredWidth = 420f;
                le.flexibleWidth = 1f;
                var tx = hintGo.GetComponent<Text>();
                tx.fontSize = 18;
                tx.color = Color.white;
                tx.alignment = TextAnchor.MiddleLeft;
                tx.horizontalOverflow = HorizontalWrapMode.Wrap;
                tx.text = hint;
            }

            if (confirmSelectionButton == null)
            {
                _runtimeConfirmButton = CreateFooterButton("Confirm", "确认", _selectionFooterRoot);
                _boundConfirmButton = _runtimeConfirmButton;
            }
            else
                _boundConfirmButton = confirmSelectionButton;

            if (cancelSelectionButton == null)
            {
                _runtimeCancelButton = CreateFooterButton("Cancel", "取消", _selectionFooterRoot);
                _boundCancelButton = _runtimeCancelButton;
            }
            else
                _boundCancelButton = cancelSelectionButton;
        }

        private static Button CreateFooterButton(string name, string label, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 140f;
            le.preferredHeight = 44f;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            go.GetComponent<Image>().color = new Color(0.25f, 0.45f, 0.7f, 1f);
            var tGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            tGo.transform.SetParent(go.transform, false);
            var trt = tGo.GetComponent<RectTransform>();
            StretchFull(trt);
            var tx = tGo.GetComponent<Text>();
            tx.text = label;
            tx.fontSize = 20;
            tx.alignment = TextAnchor.MiddleCenter;
            tx.color = Color.white;
            return btn;
        }

        private static void StretchBottomBar(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = Vector2.zero;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void WireSelectionListeners()
        {
            if (_boundConfirmButton != null)
                _boundConfirmButton.onClick.AddListener(OnConfirmSelectionClicked);
            if (_boundCancelButton != null)
                _boundCancelButton.onClick.AddListener(OnCancelSelectionClicked);
        }

        private void TeardownSelectionListeners()
        {
            if (_boundConfirmButton != null)
                _boundConfirmButton.onClick.RemoveListener(OnConfirmSelectionClicked);
            if (_boundCancelButton != null)
                _boundCancelButton.onClick.RemoveListener(OnCancelSelectionClicked);
            _boundConfirmButton = null;
            _boundCancelButton = null;
        }

        private void DestroyRuntimeFooter()
        {
            if (_selectionFooterRoot != null)
            {
                Destroy(_selectionFooterRoot.gameObject);
                _selectionFooterRoot = null;
            }

            _runtimeConfirmButton = null;
            _runtimeCancelButton = null;
        }

        private void OnConfirmSelectionClicked()
        {
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

        private void OnCancelSelectionClicked()
        {
            _model?.OnCancelSelection?.Invoke();
            UIManager.Instance?.Close(UIPanelId.CardList);
        }

        private void UpdateSelectionFooterInteractable()
        {
            if (_model == null || _model.MaxSelectable <= 0)
                return;

            if (_boundConfirmButton != null)
                _boundConfirmButton.interactable = _selectedIndices.Count == _model.MaxSelectable;
        }

        private static Card CloneRuntimeCard(Card src) =>
            new Card
            {
                Id = src.Id,
                Name = src.Name,
                Level = src.Level,
                Type = src.Type,
                EffectDesc = src.EffectDesc,
                IsUnique = src.IsUnique
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
            if (_model != null && _model.MaxSelectable > 0)
            {
                _model.OnCancelSelection?.Invoke();
                UIManager.Instance?.Close(UIPanelId.CardList);
                return;
            }

            UIManager.Instance?.Close(UIPanelId.CardList);
        }
    }
}
