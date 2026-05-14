using System.Collections;
using System.Collections.Generic;
using KingCardsSpire.Core;
using KingCardsSpire.Configs;
using KingCardsSpire.Controllers;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using KingCardsSpire.Views.UI.Cards;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>
    /// 驻守者奖励（两档金币 + 至多 5 张 BOSS 卡组卡牌）或常规战斗胜利后从敌方卡组中至多 3 张里选 1 张；选项使用与战斗相同的 Card 预制体（<see cref="CardView"/>）。
    /// 驻守：跳过卡牌按钮领取列表中<strong>全部金币项</strong>（见 <see cref="GameManager.TrySkipBossCardRewardsCollectGoldAndAdvance"/>）。
    /// 常规：跳过表示不获得卡牌，仅关闭本面板。
    /// </summary>
    public sealed class CardRewardView : BaseView
    {
        [SerializeField] private Text titleText;
        [SerializeField] private RectTransform cardOptionsRoot;
        [SerializeField] private CardView cardOptionPrefab;
        [SerializeField] private float cardOptionScale = 0.22f;
        [SerializeField] private Button skipCardRewardButton;

        private GameManager _game;
        private ConfigManager _config;

        private readonly List<CardView> _spawnedOptionCards = new();

        private UnityAction _onSkipClicked;

        /// <summary>本次打开是否为「常规战胜 · 敌方卡组三选一」模式。</summary>
        private bool _casualVictoryRewardMode;

        public override void Initialize()
        {
            SetPanelId(UIPanelId.CardReward);
            _game = GameManager.Instance;
            _config = ConfigManager.Instance;
        }

        protected override void OnOpen()
        {
            ClearSpawnedCards();
            _casualVictoryRewardMode = ResolveCasualVictoryRewardMode();
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

        private static bool ResolveCasualVictoryRewardMode()
        {
            var bm = BattleManager.Instance;
            var pending = bm?.PendingCasualVictoryRewardCardIds;
            return pending != null && pending.Count > 0;
        }

        private void RefreshDisplay()
        {
            if (titleText == null)
                return;

            if (_casualVictoryRewardMode)
            {
                titleText.text = "战斗胜利 · 选择一张卡牌";
                return;
            }

            var gm = _game ?? GameManager.Instance;
            if (gm?.PlayerState != null)
                titleText.text = $"第{gm.PlayerState.CurrentFloor}层 · 驻守奖励";
        }

        private void BuildOptionCards()
        {
            if (cardOptionsRoot == null || cardOptionPrefab == null)
            {
                Debug.LogWarning("[CardRewardView] 请绑定 cardOptionsRoot 与 cardOptionPrefab（如 Assets/GameAssets/UI/Cards/Card.prefab）。");
                return;
            }

            if (_casualVictoryRewardMode)
            {
                BuildCasualVictoryOptionCards();
                return;
            }

            var gm = _game ?? GameManager.Instance;
            var list = gm?.PendingBossRewards;
            var count = list != null ? list.Count : 0;

            for (var i = 0; i < count; i++)
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
                cv.OverrideClick(() => StartCoroutine(OnBossOptionConfirmed(idx)));
                _spawnedOptionCards.Add(cv);
            }
        }

        private void BuildCasualVictoryOptionCards()
        {
            var bm = BattleManager.Instance;
            var ids = bm?.PendingCasualVictoryRewardCardIds;
            if (ids == null || ids.Count == 0)
                return;

            for (var i = 0; i < ids.Count; i++)
            {
                var cardId = ids[i];
                if (string.IsNullOrEmpty(cardId))
                    continue;

                var cv = Instantiate(cardOptionPrefab, cardOptionsRoot, false);
                cv.SetScale(cardOptionScale);
                cv.SetFaceDown(false);

                CardConfigEntry cfg = null;
                if (_config != null)
                    _config.TryGetCard(cardId, out cfg);

                var vm = cfg != null
                    ? CardViewModel.FromConfigOnly(cfg)
                    : CardViewModel.FromBossRewardOption(
                        new BossRewardOption
                        {
                            IsGold = false,
                            GoldAmount = 0,
                            CardId = cardId,
                            CardDisplayName = cardId
                        },
                        null);
                cv.Apply(vm);

                if (cfg != null)
                    cv.LoadCardArtFromConfig(cfg);

                var idx = i;
                cv.OverrideClick(() => StartCoroutine(OnCasualOptionConfirmed(idx)));
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

            _onSkipClicked = () =>
            {
                if (_casualVictoryRewardMode)
                    StartCoroutine(OnCasualSkipConfirmedRoutine());
                else
                    StartCoroutine(OnBossSkipConfirmed());
            };
            skipCardRewardButton.onClick.AddListener(_onSkipClicked);
        }

        private void UnwireSkipButton()
        {
            if (skipCardRewardButton != null && _onSkipClicked != null)
                skipCardRewardButton.onClick.RemoveListener(_onSkipClicked);
            _onSkipClicked = null;
        }

        private IEnumerator OnBossOptionConfirmed(int index)
        {
            var gm = GameManager.Instance;
            var ui = UIManager.Instance;
            if (gm == null || ui == null)
                yield break;

            if (!gm.TryApplyBossRewardChoice(index))
                yield break;

            CloseRewardAndBattle(ui);
            yield return RunEndingDialogueIfVictoryRoutine(ui);
        }

        private IEnumerator OnCasualOptionConfirmed(int index)
        {
            var gm = GameManager.Instance;
            var ui = UIManager.Instance;
            var bm = BattleManager.Instance;
            if (gm == null || ui == null || bm == null)
                yield break;

            var pending = bm.PendingCasualVictoryRewardCardIds;
            if (pending == null || index < 0 || index >= pending.Count)
                yield break;

            var cardId = pending[index];
            if (!gm.TryGrantCasualVictoryRewardCard(cardId))
                yield break;

            bm.ClearPendingCasualVictoryRewardOffer();
            ui.Close(UIPanelId.CardReward);
        }

        private IEnumerator OnBossSkipConfirmed()
        {
            var gm = GameManager.Instance;
            var ui = UIManager.Instance;
            if (gm == null || ui == null)
                yield break;

            if (!gm.TrySkipBossCardRewardsCollectGoldAndAdvance())
                yield break;

            CloseRewardAndBattle(ui);
            yield return RunEndingDialogueIfVictoryRoutine(ui);
        }

        private IEnumerator OnCasualSkipConfirmedRoutine()
        {
            var bm = BattleManager.Instance;
            var ui = UIManager.Instance;
            bm?.ClearPendingCasualVictoryRewardOffer();
            if (ui != null)
                ui.Close(UIPanelId.CardReward);
            yield break;
        }

        private static IEnumerator RunEndingDialogueIfVictoryRoutine(UIManager ui)
        {
            var gm = GameManager.Instance;
            var dialogue = ServiceLocator.Get<DialogueController>();
            if (gm == null || ui == null || dialogue == null || !gm.IsRunVictory)
                yield break;

            yield return ui.StartCoroutine(dialogue.PlayDialogue("ending_final", null));
        }

        private static void CloseRewardAndBattle(UIManager ui)
        {
            ui.Close(UIPanelId.CardReward);
            ui.Close(UIPanelId.Battle);
        }
    }
}
