using System.Collections;
using System.Collections.Generic;
using KingCardsSpire.Configs;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using KingCardsSpire.Views.UI.Cards;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>
    /// 击败驻守者后的五选一奖励：选项使用与战斗相同的 Card 预制体（<see cref="CardView"/>）。
    /// 预制体需在 Addressables 注册为 <c>UI/Panel_BossReward</c>，并绑定选项容器、Card 预制体与跳过按钮。
    /// </summary>
    public sealed class BossRewardView : BaseView
    {
        private const int MaxOptions = 5;

        [Header("文案")]
        [SerializeField] private Text titleText;

        [Header("选项区 · Card 预制体")]
        [SerializeField] private RectTransform cardOptionsRoot;

        [SerializeField] private CardView cardOptionPrefab;

        [SerializeField] private float cardOptionScale = 0.22f;

        [Header("跳过 · 不领取本次任一选项（金币与卡牌均放弃），仍进层")]
        [SerializeField] private Button skipCardRewardButton;

        private GameManager _game;
        private ConfigManager _config;

        private readonly List<CardView> _spawnedOptionCards = new();

        private UnityAction _onSkipClicked;

        public override void Initialize()
        {
            SetPanelId(UIPanelId.BossReward);
            _game = GameManager.Instance;
            _config = ConfigManager.Instance;
        }

        protected override void OnOpen()
        {
            ClearSpawnedCards();
            RefreshDisplay();
            BuildOptionCards();
            WireSkipButton();
        }

        public override void Dispose()
        {
            UnwireSkipButton();
            ClearSpawnedCards();
            base.Dispose();
        }

        private void OnDestroy()
        {
            UnwireSkipButton();
            ClearSpawnedCards();
        }

        private void RefreshDisplay()
        {
            var gm = _game ?? GameManager.Instance;

            if (titleText != null && gm != null)
                titleText.text = $"第{gm.PlayerState.CurrentFloor}层 · 驻守奖励";
        }

        private void BuildOptionCards()
        {
            if (cardOptionsRoot == null || cardOptionPrefab == null)
            {
                Debug.LogWarning("[BossRewardView] 请绑定 cardOptionsRoot 与 cardOptionPrefab（如 Assets/GameAssets/UI/Cards/Card.prefab）。");
                return;
            }

            var gm = _game ?? GameManager.Instance;
            var list = gm?.PendingBossRewards;
            var count = list != null ? list.Count : 0;

            for (var i = 0; i < MaxOptions && i < count; i++)
            {
                var opt = list[i];
                if (opt == null)
                    continue;

                var cv = Instantiate(cardOptionPrefab, cardOptionsRoot, false);
                cv.SetScale(cardOptionScale);
                cv.SetFaceDown(false);

                CardConfigEntry cfg = null;
                if (!opt.IsGold && !string.IsNullOrEmpty(opt.CardId) && _config != null)
                    _config.TryGetCard(opt.CardId, out cfg);

                var vm = CardViewModel.FromBossRewardOption(opt, cfg);
                cv.Apply(vm);

                if (cfg != null)
                    cv.LoadCardArtFromConfig(cfg);

                var idx = i;
                cv.OverrideClick(() => StartCoroutine(OnOptionConfirmed(idx)));
                _spawnedOptionCards.Add(cv);
            }
        }

        private void ClearSpawnedCards()
        {
            for (var i = 0; i < _spawnedOptionCards.Count; i++)
            {
                var cv = _spawnedOptionCards[i];
                if (cv != null)
                    Destroy(cv.gameObject);
            }

            _spawnedOptionCards.Clear();
        }

        private void WireSkipButton()
        {
            UnwireSkipButton();
            if (skipCardRewardButton == null)
                return;

            _onSkipClicked = () => StartCoroutine(OnSkipConfirmed());
            skipCardRewardButton.onClick.AddListener(_onSkipClicked);
        }

        private void UnwireSkipButton()
        {
            if (skipCardRewardButton != null && _onSkipClicked != null)
                skipCardRewardButton.onClick.RemoveListener(_onSkipClicked);
            _onSkipClicked = null;
        }

        private IEnumerator OnOptionConfirmed(int index)
        {
            var gm = GameManager.Instance;
            var ui = UIManager.Instance;
            if (gm == null || ui == null)
                yield break;

            if (!gm.TryApplyBossRewardChoice(index))
                yield break;

            CloseRewardAndBattle(ui);
        }

        private IEnumerator OnSkipConfirmed()
        {
            var gm = GameManager.Instance;
            var ui = UIManager.Instance;
            if (gm == null || ui == null)
                yield break;

            if (!gm.TryForfeitBossRewardsAndAdvance())
                yield break;

            CloseRewardAndBattle(ui);
        }

        private static void CloseRewardAndBattle(UIManager ui)
        {
            ui.Close(UIPanelId.BossReward);
            ui.Close(UIPanelId.Battle);
        }
    }
}
