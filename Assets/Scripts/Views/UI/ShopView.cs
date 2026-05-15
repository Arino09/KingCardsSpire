using System.Collections.Generic;
using KingCardsSpire.Configs;
using KingCardsSpire.Core.Events;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>塔内商店：进货规则见 <see cref="GameManager.EnsureShopStock"/>。</summary>
    public sealed class ShopView : BaseView
    {
        // [SerializeField] private Text goldText;
        [SerializeField] private RectTransform slotRowsRoot;
        [SerializeField] private ShopSlotRowView slotRowPrefab;
        [SerializeField] private float cardScale = 0.22f;
        [SerializeField] private Button closeButton;

        private GameManager _game;
        private ConfigManager _config;
        private EventManager _events;

        private UnityAction _onCloseClicked;
        private readonly List<ShopSlotRowView> _spawnedRows = new();

        public override void Initialize()
        {
            SetPanelId(UIPanelId.Shop);
            _game = GameManager.Instance;
            _config = ConfigManager.Instance;
            _events = EventManager.Instance;

            _onCloseClicked = OnCloseClicked;
            closeButton.onClick.AddListener(_onCloseClicked);
            if (_events != null)
                _events.Subscribe<GoldChangedEvent>(OnGoldChanged);
        }

        protected override void OnOpen()
        {
            var gm = _game ?? GameManager.Instance;
            gm?.EnsureShopStock();

            RefreshGoldText();

            RebuildSlots();
        }

        public override void Dispose()
        {
            if (_events != null)
                _events.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            closeButton.onClick.RemoveListener(_onCloseClicked);
            ClearSpawnedRows();
            base.Dispose();
        }

        private void OnDestroy()
        {
            if (_events != null)
                _events.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            closeButton.onClick.RemoveListener(_onCloseClicked);
            ClearSpawnedRows();
        }

        private void OnGoldChanged(GoldChangedEvent _) => RefreshGoldText();

        private void RefreshGoldText()
        {
            var gm = _game ?? GameManager.Instance;
            // goldText.text = gm != null ? $"${gm.PlayerState.Gold}" : string.Empty;
        }

        private void RebuildSlots()
        {
            ClearSpawnedRows();

            var gm = _game ?? GameManager.Instance;
            var slots = gm?.ShopState?.Slots;
            if (slots == null || slots.Length == 0)
                return;

            var unlimited = gm.HasBuff(BuffId.UnlimitedSupply);

            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                var row = Instantiate(slotRowPrefab, slotRowsRoot, false);
                var idx = i;

                CardConfigEntry cfg = null;
                _config.TryGetCard(slot.ProductId, out cfg);

                row.Bind(cfg, slot.BasePrice, slot.SoldOut, unlimited, cardScale, () => OnBuySlot(idx));
                _spawnedRows.Add(row);
            }
        }

        private void OnBuySlot(int index)
        {
            var gm = _game ?? GameManager.Instance;
            if (gm == null)
                return;

            if (gm.TryPurchaseShopSlot(index))
                RebuildSlots();
            RefreshGoldText();
        }

        private void OnCloseClicked()
        {
            UiButtonSfx.PlayDefaultClick();
            UIManager.Instance.Close(UIPanelId.Shop);
        }

        private void ClearSpawnedRows()
        {
            for (var i = 0; i < _spawnedRows.Count; i++)
            {
                var row = _spawnedRows[i];
                Destroy(row.gameObject);
            }

            _spawnedRows.Clear();
        }
    }
}
