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
    /// 卡牌图鉴：按配置表列出全部卡，解锁状态来自 <see cref="CardAlbumProgressStore"/>。
    /// </summary>
    public sealed class AlbumView : BaseView
    {
        private const float AlbumCardScale = 0.35f;

        [SerializeField] private RectTransform cardGridRoot;
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private Button closeButton;

        private readonly List<CardConfigEntry> _entriesScratch = new();

        public override void Initialize()
        {
            SetPanelId(UIPanelId.Album);
            closeButton.onClick.AddListener(OnCloseClicked);
        }

        public override void Dispose()
        {
            ClearGrid();
            closeButton.onClick.RemoveListener(OnCloseClicked);
            base.Dispose();
        }

        protected override void OnOpen()
        {
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            ClearGrid();

            var cfg = ConfigManager.Instance;
            if (cfg == null || cardGridRoot == null || cardPrefab == null)
                return;

            _entriesScratch.Clear();
            cfg.CopyAllCardEntries(_entriesScratch);

            var discovered = CardAlbumProgressStore.LoadDiscoveredIds();

            for (var i = 0; i < _entriesScratch.Count; i++)
            {
                var entry = _entriesScratch[i];
                if (entry == null || string.IsNullOrEmpty(entry.Id))
                    continue;

                var isDiscovered = discovered.Contains(entry.Id);
                var cv = Instantiate(cardPrefab, cardGridRoot, false);
                cv.SetScale(AlbumCardScale);
                cv.SetFaceDown(false);
                cv.OverrideClick(null);
                cv.Apply(CardViewModel.FromAlbumEntry(entry, isDiscovered));
                cv.LoadCardArtFromConfig(entry);
                cv.SetAlbumMaskRevealed(isDiscovered);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(cardGridRoot);
        }

        private void ClearGrid()
        {
            if (cardGridRoot == null)
                return;

            for (var i = cardGridRoot.childCount - 1; i >= 0; i--)
                Destroy(cardGridRoot.GetChild(i).gameObject);
        }

        private void OnCloseClicked()
        {
            UiButtonSfx.PlayDefaultClick();
            UIManager.Instance?.Close(UIPanelId.Album);
        }
    }
}
