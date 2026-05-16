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
    /// 驻守者奖励、原住民剧情完成三选一、常规战胜三选一、主角房友谊赛三选一或「删仓库一张牌」奖励；选项使用与战斗相同的 Card 预制体（<see cref="CardView"/>）。
    /// 驻守：SpareDay 金币在战胜时已静默发放；跳过表示不领卡并进层（见 <see cref="GameManager.TrySkipBossCardRewardsCollectGoldAndAdvance"/>）。
    /// 原住民三段剧情完成：三选一基础卡，确认后加入持有，跳过放弃本次奖励；不占驻守、不进层。
    /// 常规 / 友谊赛三选一：跳过表示不获得本次卡牌奖励，仅关闭本面板。
    /// 友谊赛删仓库：跳过仍发放 5 金币且不删牌。
    /// </summary>
    public sealed class CardRewardView : BaseView
    {
        private enum CardRewardSessionKind
        {
            BossRewards,
            CasualVictory,
            HeroDuelPickThree,
            HeroDuelRemoveStorage,
            NpcStoryCompletion
        }

        [SerializeField] private Text titleText;
        [SerializeField] private RectTransform cardOptionsRoot;
        [SerializeField] private CardView cardOptionPrefab;
        [SerializeField] private float cardOptionScale = 0.22f;
        [SerializeField] private Button skipCardRewardButton;

        private GameManager _game;
        private ConfigManager _config;

        private readonly List<CardView> _spawnedOptionCards = new();

        private UnityAction _onSkipClicked;

        private CardRewardSessionKind _sessionKind;

        public override void Initialize()
        {
            SetPanelId(UIPanelId.CardReward);
            _game = GameManager.Instance;
            _config = ConfigManager.Instance;
        }

        protected override void OnOpen()
        {
            ClearSpawnedCards();
            _sessionKind = ResolveSessionKind();
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

        private static CardRewardSessionKind ResolveSessionKind()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.IsHeroDuelStorageRemovalRewardPending)
                return CardRewardSessionKind.HeroDuelRemoveStorage;

            var bm = BattleManager.Instance;
            if (bm?.PendingHeroDuelPickThreeCardIds != null && bm.PendingHeroDuelPickThreeCardIds.Count > 0)
                return CardRewardSessionKind.HeroDuelPickThree;

            if (bm?.PendingCasualVictoryRewardCardIds != null && bm.PendingCasualVictoryRewardCardIds.Count > 0)
                return CardRewardSessionKind.CasualVictory;

            if (gm?.PendingNpcStoryCompletionRewards != null && gm.PendingNpcStoryCompletionRewards.Count > 0)
                return CardRewardSessionKind.NpcStoryCompletion;

            return CardRewardSessionKind.BossRewards;
        }

        private void RefreshDisplay()
        {
            if (titleText == null)
                return;

            switch (_sessionKind)
            {
                case CardRewardSessionKind.CasualVictory:
                    titleText.text = "战斗胜利 · 选择一张卡牌";
                    return;
                case CardRewardSessionKind.HeroDuelPickThree:
                    titleText.text = "友谊赛胜利 · 三选一卡牌";
                    return;
                case CardRewardSessionKind.HeroDuelRemoveStorage:
                    titleText.text = "友谊赛胜利 · 删除仓库中的一张牌";
                    return;
                case CardRewardSessionKind.NpcStoryCompletion:
                    titleText.text = "原住民剧情完成 · 选择一张卡牌";
                    return;
                default:
                {
                    var gm = _game ?? GameManager.Instance;
                    if (gm?.PlayerState != null)
                        titleText.text = $"第{gm.PlayerState.CurrentFloor}层 · 驻守奖励";
                    return;
                }
            }
        }

        private void BuildOptionCards()
        {
            if (cardOptionsRoot == null || cardOptionPrefab == null)
            {
                Debug.LogWarning("[CardRewardView] 请绑定 cardOptionsRoot 与 cardOptionPrefab（如 Assets/GameAssets/UI/Cards/Card.prefab）。");
                return;
            }

            if (_sessionKind == CardRewardSessionKind.CasualVictory)
            {
                BuildCasualVictoryOptionCards();
                return;
            }

            if (_sessionKind == CardRewardSessionKind.HeroDuelPickThree)
            {
                BuildHeroDuelPickThreeOptionCards();
                return;
            }

            if (_sessionKind == CardRewardSessionKind.HeroDuelRemoveStorage)
            {
                BuildHeroDuelRemoveStorageOptionCards();
                return;
            }

            if (_sessionKind == CardRewardSessionKind.NpcStoryCompletion)
            {
                BuildNpcStoryCompletionOptionCards();
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

        private void BuildHeroDuelPickThreeOptionCards()
        {
            var bm = BattleManager.Instance;
            var ids = bm?.PendingHeroDuelPickThreeCardIds;
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
                cv.OverrideClick(() => StartCoroutine(OnHeroDuelPickThreeConfirmed(idx)));
                _spawnedOptionCards.Add(cv);
            }
        }

        private void BuildNpcStoryCompletionOptionCards()
        {
            var gm = _game ?? GameManager.Instance;
            var list = gm?.PendingNpcStoryCompletionRewards;
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
                cv.OverrideClick(() => StartCoroutine(OnNpcStoryCompletionOptionConfirmed(idx)));
                _spawnedOptionCards.Add(cv);
            }
        }

        private void BuildHeroDuelRemoveStorageOptionCards()
        {
            var gm = GameManager.Instance;
            var stored = gm?.PlayerState?.StoredCards;
            if (stored == null || stored.Length == 0)
                return;

            for (var i = 0; i < stored.Length; i++)
            {
                var c = stored[i];
                if (c == null || string.IsNullOrEmpty(c.Id))
                    continue;

                var cv = Instantiate(cardOptionPrefab, cardOptionsRoot, false);
                cv.SetScale(cardOptionScale);
                cv.SetFaceDown(false);

                CardConfigEntry cfg = null;
                if (_config != null)
                    _config.TryGetCard(c.Id, out cfg);

                var vm = cfg != null
                    ? CardViewModel.FromConfigOnly(cfg)
                    : CardViewModel.FromBossRewardOption(
                        new BossRewardOption
                        {
                            IsGold = false,
                            GoldAmount = 0,
                            CardId = c.Id,
                            CardDisplayName = string.IsNullOrEmpty(c.Name) ? c.Id : c.Name
                        },
                        null);
                cv.Apply(vm);

                if (cfg != null)
                    cv.LoadCardArtFromConfig(cfg);

                var idx = i;
                cv.OverrideClick(() => StartCoroutine(OnHeroDuelRemoveStorageConfirmed(idx)));
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
                UiButtonSfx.PlayDefaultClick();
                switch (_sessionKind)
                {
                    case CardRewardSessionKind.BossRewards:
                        StartCoroutine(OnBossSkipConfirmed());
                        break;
                    case CardRewardSessionKind.CasualVictory:
                        StartCoroutine(OnCasualSkipConfirmedRoutine());
                        break;
                    case CardRewardSessionKind.HeroDuelPickThree:
                        StartCoroutine(OnHeroDuelPickThreeSkipRoutine());
                        break;
                    case CardRewardSessionKind.HeroDuelRemoveStorage:
                        StartCoroutine(OnHeroDuelRemoveStorageSkipRoutine());
                        break;
                    case CardRewardSessionKind.NpcStoryCompletion:
                        StartCoroutine(OnNpcStoryCompletionSkipRoutine());
                        break;
                }
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

            yield return ui.StartCoroutine(ui.CoCloseBossRewardBattleRevealMainHubRoutine());
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

        private IEnumerator OnHeroDuelPickThreeConfirmed(int index)
        {
            var gm = GameManager.Instance;
            var ui = UIManager.Instance;
            var bm = BattleManager.Instance;
            if (gm == null || ui == null || bm == null)
                yield break;

            var pending = bm.PendingHeroDuelPickThreeCardIds;
            if (pending == null || index < 0 || index >= pending.Count)
                yield break;

            var cardId = pending[index];
            if (!gm.TryGrantHeroDuelPickThreeReward(cardId))
                yield break;

            bm.ClearPendingHeroDuelPickThreeOffer();
            ui.Close(UIPanelId.CardReward);
        }

        private IEnumerator OnNpcStoryCompletionOptionConfirmed(int index)
        {
            var gm = GameManager.Instance;
            var ui = UIManager.Instance;
            if (gm == null || ui == null)
                yield break;

            if (!gm.TryApplyNpcStoryCompletionReward(index))
                yield break;

            ui.Close(UIPanelId.CardReward);
        }

        private IEnumerator OnNpcStoryCompletionSkipRoutine()
        {
            var gm = GameManager.Instance;
            var ui = UIManager.Instance;
            gm?.ClearPendingNpcStoryCompletionRewardOffer();
            if (ui != null)
                ui.Close(UIPanelId.CardReward);
            yield break;
        }

        private IEnumerator OnHeroDuelRemoveStorageConfirmed(int index)
        {
            var gm = GameManager.Instance;
            var ui = UIManager.Instance;
            if (gm == null || ui == null)
                yield break;

            if (!gm.TryCompleteHeroDuelStorageRemovalRewardAtStorageIndex(index))
                yield break;

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

            yield return ui.StartCoroutine(ui.CoCloseBossRewardBattleRevealMainHubRoutine());
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

        private IEnumerator OnHeroDuelPickThreeSkipRoutine()
        {
            var bm = BattleManager.Instance;
            var ui = UIManager.Instance;
            bm?.ClearPendingHeroDuelPickThreeOffer();
            if (ui != null)
                ui.Close(UIPanelId.CardReward);
            yield break;
        }

        private IEnumerator OnHeroDuelRemoveStorageSkipRoutine()
        {
            var gm = GameManager.Instance;
            var ui = UIManager.Instance;
            if (gm == null || ui == null)
                yield break;

            if (!gm.TrySkipHeroDuelStorageRemovalReward())
                yield break;

            ui.Close(UIPanelId.CardReward);
            yield break;
        }

        private static IEnumerator RunEndingDialogueIfVictoryRoutine(UIManager ui)
        {
            var gm = GameManager.Instance;
            var dialogue = ServiceLocator.Get<DialogueController>();
            if (gm == null || ui == null || dialogue == null || !gm.IsRunVictory)
                yield break;

            yield return ui.StartCoroutine(dialogue.PlayDialogue(WellKnownDialogueIds.EndingFinal, null));

            if (!gm.IsRunVictory)
                yield break;

            yield return GameManager.CoCloseAllUiAndOpenMainMenuRoutine();
        }
    }
}
