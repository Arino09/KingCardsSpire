using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using KingCardsSpire.Configs;
using KingCardsSpire.Controllers;
using KingCardsSpire.Core;
using KingCardsSpire.Core.Battle;
using KingCardsSpire.Core.Events;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using KingCardsSpire.Views.UI.Cards;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>
    /// 战斗界面：绑定 BattleView 预制体；手牌区实例化 Card 预制体并接入 <see cref="BattleController"/>。
    /// </summary>
    /// <remarks>
    /// 需在 Unity Inspector 中绑定 <see cref="playerDiscardPileAnchor"/>、<see cref="enemyDiscardPileAnchor"/>
    ///（可与己方/敌方弃牌堆按钮同一 RectTransform），否则飞行动画目标无效。
    /// 同时绑定 <see cref="confirmPlayButton"/>：先点手牌选中，再点此按钮确认出牌。
    /// </remarks>
    public sealed class BattleView : BaseView
    {
        /// <summary>敌方手牌区单卡缩放（相对预制体基准）。</summary>
        private const float EnemyHandCardScale = 0.15f;

        /// <summary>己方手牌区单卡缩放。</summary>
        private const float PlayerHandCardScale = 0.25f;

        /// <summary>出牌区（比大小回合展示）单卡缩放。</summary>
        private const float PlayAreaCardScale = 0.3f;

        [Header("顶部状态")]
        [SerializeField] private Text opponentNameText;
        [SerializeField] private Text turnsText;
        [SerializeField] private Text weatherText;

        [Header("手牌")]
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private RectTransform enemyHandRoot;
        [SerializeField] private RectTransform playerHandRoot;

        [Tooltip("指数形态弃牌：全屏遮罩（通常为 Mask 节点）；未绑定时仅依赖手牌 Canvas 抬升。")]
        [SerializeField] private GameObject exponentialSacrificeDimmer;

        [Tooltip("指数形态弃牌：一般为 MyHand 上的 Canvas；留空则从 playerHandRoot 上取 Canvas。")]
        [SerializeField] private Canvas playerHandCanvasOverride;

        [Tooltip("指数形态弃牌时遮罩上方的提示文案（可选）。")]
        [SerializeField] private Text exponentialSacrificeHintText;

        [Header("出牌区")]
        [SerializeField] private RectTransform enemyPlayArea;
        [SerializeField] private RectTransform myPlayArea;

        [Header("按钮")]
        [SerializeField] private Button enemyDiscardButton;
        [SerializeField] private Button playerDiscardButton;
        [SerializeField] private Button settingsButton;

        [Header("出牌")]
        [Tooltip("玩家先点击手牌选中，再点此按钮确认出牌（需在预制体上绑定）。")]
        [SerializeField] private Button confirmPlayButton;

        [Tooltip("翻面结算后、飞入弃牌堆前停留的秒数，便于辨认胜负后再播动画。")]
        [SerializeField] private float outcomeReadPauseSeconds = 0.85f;

        [Header("动画锚点")]
        [Tooltip("一般为己方弃牌堆按钮所在 RectTransform（Inspector 可与 playerDiscardButton 同一物体）。")]
        [SerializeField] private RectTransform playerDiscardPileAnchor;

        [Tooltip("一般为敌方弃牌堆按钮所在 RectTransform（可与 enemyDiscardButton 同一物体）。")]
        [SerializeField] private RectTransform enemyDiscardPileAnchor;

        [Header("开场教学战")]
        [Tooltip("第一回合强引导：全屏暗层，手牌 Canvas 需叠在其上方；未绑定时仅依赖手牌区抬升。")]
        [SerializeField] private GameObject tutorialHandDimmer;

        [Tooltip("教学战败占位（全屏节点）；未绑定时跳过展示。")]
        [SerializeField] private GameObject tutorialDefeatPlaceholder;

        /// <summary>战斗流程控制器（出牌、比大小、回合提交）。</summary>
        private BattleController _battle;

        /// <summary>全局事件总线，订阅 <see cref="BattleStateChangedEvent"/> 以刷新界面。</summary>
        private EventManager _events;

        /// <summary>卡牌配置查询（名称、立绘等）。</summary>
        private ConfigManager _config;

        /// <summary>按钮监听缓存，便于 <see cref="UnwireButtons"/> 成对移除。</summary>
        private UnityAction _onSettings;

        /// <summary>敌方弃牌堆按钮：打开列表展示。</summary>
        private UnityAction _onEnemyDiscard;

        /// <summary>己方弃牌堆按钮：打开列表展示。</summary>
        private UnityAction _onPlayerDiscard;

        /// <summary>确认出牌按钮。</summary>
        private UnityAction _onConfirmPlay;

        /// <summary>己方手牌中选中的下标；-1 表示未选中（尚未写入 <see cref="BattleController"/>）。</summary>
        private int _selectedPlayerHandIndex = -1;

        /// <summary>翻面与飞行动画进行中；期间忽略手牌点击并延后完整 Refresh。</summary>
        private bool _roundVisualBusy;

        /// <summary>已确认出牌，指数形态下正等待玩家点击弃掉另一张手牌。</summary>
        private bool _awaitingExponentialSacrifice;

        private int _savedPlayerHandCanvasSortOrder;

        private bool _hasSavedPlayerHandCanvasSort;

        /// <summary>指数弃牌阶段为手牌 Canvas 叠加的排序增量。</summary>
        private const int ExponentialHandCanvasSortBoost = 80;

        /// <summary>本回合敌方出牌区实例（Prepare 后创建）。</summary>
        private CardView _enemyPlayCard;

        private string _enemyPlayCardInstanceId;

        private Coroutine _tutorialFlowCoroutine;

        private Coroutine _chaoticAutoCoroutine;

        private bool _tutorialAllowPrepareEnemyPlay;

        private bool _round3PreBattleDialogShown;

        private bool? _pendingTutorialBattleVictory;

        private bool _tutorialFirstRoundGuideActive;

        private int _savedTutorialHandCanvasSortOrder;

        private bool _hasSavedTutorialHandCanvasSort;

        private const int TutorialHandCanvasSortBoost = 80;

        /// <summary>
        /// 面板初始化：解析服务、注册按钮；战斗状态订阅在 <see cref="OnOpen"/> 中进行。
        /// </summary>
        public override void Initialize()
        {
            SetPanelId(UIPanelId.Battle);
            _battle = ServiceLocator.Get<BattleController>();
            _events = EventManager.Instance;
            _config = ConfigManager.Instance;

            if (playerHandCanvasOverride == null && playerHandRoot != null)
                playerHandCanvasOverride = playerHandRoot.GetComponent<Canvas>();

            WireButtons();
        }

        /// <summary>
        /// 释放时取消事件订阅与按钮监听，避免泄漏与重复回调。
        /// </summary>
        public override void Dispose()
        {
            StopTutorialFlowCoroutine();
            StopChaoticAutoCoroutine();
            ExitExponentialSacrificeUi(true);
            UnwireButtons();
            if (_events != null)
            {
                _events.Unsubscribe<BattleStateChangedEvent>(OnBattleStateChanged);
                _events.Unsubscribe<BattleEndedEvent>(OnTutorialBattleEndedCapture);
            }

            base.Dispose();
        }

        /// <summary>
        /// 每次打开面板：订阅战斗状态变化；若尚未开战则请求开局并全量刷新 UI。
        /// </summary>
        protected override void OnOpen()
        {
            if (_events != null)
            {
                _events.Unsubscribe<BattleStateChangedEvent>(OnBattleStateChanged);
                _events.Subscribe<BattleStateChangedEvent>(OnBattleStateChanged);
            }

            _battle ??= ServiceLocator.Get<BattleController>();
            var bm = BattleManager.Instance;

            if (bm != null && bm.IsTutorialBattle && _battle != null && _battle.IsBattleActive)
            {
                _tutorialAllowPrepareEnemyPlay = false;
                _round3PreBattleDialogShown = false;
                _pendingTutorialBattleVictory = null;

                if (_events != null)
                {
                    _events.Unsubscribe<BattleEndedEvent>(OnTutorialBattleEndedCapture);
                    _events.Subscribe<BattleEndedEvent>(OnTutorialBattleEndedCapture);
                }

                StopTutorialFlowCoroutine();
                _tutorialFlowCoroutine = StartCoroutine(OpeningTutorialBattleFlowRoutine());

                ExitExponentialSacrificeUi(true);
                RefreshBattleChromeOnly();
                return;
            }

            if (_events != null)
                _events.Unsubscribe<BattleEndedEvent>(OnTutorialBattleEndedCapture);

            StopChaoticAutoCoroutine();

            if (_battle != null && !_battle.IsBattleActive)
                StartCoroutine(OpenNonTutorialBattleRoutine());

            ExitExponentialSacrificeUi(true);
            RefreshAll();
            TryScheduleChaoticAutoRound();
        }

        private IEnumerator OpenNonTutorialBattleRoutine()
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.HasBuff(BuffId.ChaoticBattlefield))
                yield return gm.RunChaoticBattlefieldPreBattlePickRoutine();

            _battle?.RequestStartBattle();
        }

        private void StopChaoticAutoCoroutine()
        {
            if (_chaoticAutoCoroutine == null)
                return;
            StopCoroutine(_chaoticAutoCoroutine);
            _chaoticAutoCoroutine = null;
        }

        private void TryScheduleChaoticAutoRound()
        {
            var bm = BattleManager.Instance;
            if (bm == null || !bm.IsBattleActive || bm.IsTutorialBattle || !bm.PlayerChaoticRandomPlayThisBattle)
                return;
            if (_roundVisualBusy || _awaitingExponentialSacrifice)
                return;
            if (_chaoticAutoCoroutine != null)
                return;
            _chaoticAutoCoroutine = StartCoroutine(ChaoticAutoPlayRoundRoutine());
        }

        private IEnumerator ChaoticAutoPlayRoundRoutine()
        {
            yield return new WaitForSeconds(0.12f);

            var bm = BattleManager.Instance;
            if (_battle == null || bm == null || !bm.IsBattleActive || bm.IsTutorialBattle ||
                !bm.PlayerChaoticRandomPlayThisBattle || _roundVisualBusy || _awaitingExponentialSacrifice)
            {
                _chaoticAutoCoroutine = null;
                yield break;
            }

            if (!bm.PrepareEnemyPlay(out _))
            {
                _chaoticAutoCoroutine = null;
                yield break;
            }

            if (bm.PendingPlayerHandIndex >= 0)
            {
                _chaoticAutoCoroutine = null;
                yield break;
            }

            var legal = new List<int>();
            bm.CollectLegalPlayerHandPlayIndices(legal);
            if (legal.Count == 0)
            {
                _chaoticAutoCoroutine = null;
                yield break;
            }

            var idx = legal[UnityEngine.Random.Range(0, legal.Count)];
            if (!_battle.TryStagePlayerCard(idx, out var err))
            {
                Debug.LogWarning($"[BattleView] 混乱战场自动出牌: {err}");
                _chaoticAutoCoroutine = null;
                yield break;
            }

            if (_battle.RequiresPlayerExponentialSacrifice())
            {
                if (!bm.TryAutoResolvePlayerExponentialIfNeeded(out var e2))
                    Debug.LogWarning($"[BattleView] 混乱战场指数弃牌: {e2}");
            }

            _selectedPlayerHandIndex = -1;
            SyncPlayerHandSelectionVisuals();
            UpdateConfirmPlayButtonInteractable();
            _chaoticAutoCoroutine = null;
            StartCoroutine(RoundVisualSequenceRoutine());
        }

        /// <summary>
        /// 根据是否选中手牌、战斗是否进行中、动画是否占用，更新确认出牌按钮可点状态。
        /// </summary>
        private void UpdateConfirmPlayButtonInteractable()
        {
            if (confirmPlayButton == null)
                return;

            var tutorialKingOk = true;
            if (_tutorialFirstRoundGuideActive)
            {
                var st = BattleManager.Instance != null ? BattleManager.Instance.CurrentBattle : null;
                var kingIdx = FindPlayerHandIndexForCardId(st?.PlayerHand, WellKnownCardIds.King);
                tutorialKingOk = kingIdx >= 0 && _selectedPlayerHandIndex == kingIdx;
            }

            var chaoticRandom = BattleManager.Instance is { IsTutorialBattle: false, PlayerChaoticRandomPlayThisBattle: true };

            var canConfirm = !chaoticRandom && _battle != null && _battle.IsBattleActive && !_roundVisualBusy &&
                             !_awaitingExponentialSacrifice && _selectedPlayerHandIndex >= 0 && tutorialKingOk;
            confirmPlayButton.interactable = canConfirm;
        }

        /// <summary>
        /// 绑定设置与双方弃牌堆按钮的点击（先卸再绑，避免重复注册）。
        /// </summary>
        private void WireButtons()
        {
            UnwireButtons();

            _onSettings = OnSettingsClicked;
            _onEnemyDiscard = ShowEnemyDiscardClicked;
            _onPlayerDiscard = ShowPlayerDiscardClicked;
            _onConfirmPlay = OnConfirmPlayClicked;

            AddClick(settingsButton, _onSettings);
            AddClick(enemyDiscardButton, _onEnemyDiscard);
            AddClick(playerDiscardButton, _onPlayerDiscard);
            AddClick(confirmPlayButton, _onConfirmPlay);
        }

        /// <summary>移除所有已注册的按钮监听并清空委托缓存。</summary>
        private void UnwireButtons()
        {
            RemoveClick(settingsButton, _onSettings);
            RemoveClick(enemyDiscardButton, _onEnemyDiscard);
            RemoveClick(playerDiscardButton, _onPlayerDiscard);
            RemoveClick(confirmPlayButton, _onConfirmPlay);

            _onSettings = null;
            _onEnemyDiscard = null;
            _onPlayerDiscard = null;
            _onConfirmPlay = null;
        }

        /// <summary>安全地向 <see cref="Button.onClick"/> 添加监听。</summary>
        private static void AddClick(Button btn, UnityAction action)
        {
            if (btn == null || action == null)
                return;
            btn.onClick.AddListener(action);
        }

        /// <summary>安全地从 <see cref="Button.onClick"/> 移除监听。</summary>
        private static void RemoveClick(Button btn, UnityAction action)
        {
            if (btn == null || action == null)
                return;
            btn.onClick.RemoveListener(action);
        }

        /// <summary>
        /// 战斗数据变更时：动画进行中仅刷新顶部栏与透视提示，避免打断翻面/飞行；
        /// 否则全量重建手牌与出牌区。
        /// </summary>
        private void OnBattleStateChanged(BattleStateChangedEvent _)
        {
            if (_roundVisualBusy)
            {
                RefreshBattleChromeOnly();
                return;
            }

            RefreshAll();
            TryScheduleChaoticAutoRound();
        }

        /// <summary>设置按钮占位。</summary>
        private void OnSettingsClicked()
        {
            Debug.Log("[BattleView] 设置：占位。");
        }

        /// <summary>打开卡牌列表面板并展示敌方弃牌堆。</summary>
        private void ShowEnemyDiscardClicked()
        {
            StartCoroutine(OpenDiscardPanelAsync(true));
        }

        /// <summary>打开卡牌列表面板并展示己方弃牌堆。</summary>
        private void ShowPlayerDiscardClicked()
        {
            StartCoroutine(OpenDiscardPanelAsync(false));
        }

        /// <summary>
        /// 异步打开 <see cref="UIPanelId.CardList"/>，并将当前战斗中对应一方的弃牌列表绑定到视图模型。
        /// </summary>
        private IEnumerator OpenDiscardPanelAsync(bool isEnemy)
        {
            yield return UIManager.Instance.OpenAsync(UIPanelId.CardList);
            if (UIManager.Instance.TryGetView(UIPanelId.CardList, out CardListView view))
            {
                var state = BattleManager.Instance.CurrentBattle;
                var pile = isEnemy ? state.EnemyDiscard : state.PlayerDiscard;
                view.Apply(new CardListViewModel(isEnemy ? "敌方弃牌堆" : "己方弃牌堆", pile));
            }
        }

        /// <summary>
        /// 仅刷新「外壳」：回合数、天气、对手名、透视提示；不重建手牌树，供动画占用时使用。
        /// </summary>
        private void RefreshBattleChromeOnly()
        {
            var state = BattleManager.Instance.CurrentBattle;

            if (state != null)
            {
                var cap = state.NoRoundLimit ? "∞" : state.MaxRound.ToString();
                turnsText.text = $"{state.Round} / {cap}";
                weatherText.text = WeatherDisplay.Format(state.BattleWeather);
                opponentNameText.text = string.IsNullOrEmpty(state.OpponentDisplayName)
                    ? "对手"
                    : state.OpponentDisplayName;
            }
        }

        /// <summary>
        /// 全量刷新：先更新外壳，再在暂时关闭 LayoutGroup 的前提下重建双方手牌，最后布置本回合出牌区。
        /// </summary>
        /// <remarks>
        /// 重建手牌时禁用 LayoutGroup，避免每帧布局与 Instantiate/Destroy 交错导致抖动或错误尺寸。
        /// </remarks>
        private void RefreshAll()
        {
            RefreshBattleChromeOnly();

            if (ShouldShowRoundThreePreBattleDialog())
            {
                StartCoroutine(RoundThreeDialogThenRefreshRoutine());
                return;
            }

            LayoutGroup layoutEnemy = null;
            LayoutGroup layoutPlayer = null;
            var wasEnemyLayout = false;
            var wasPlayerLayout = false;
            if (enemyHandRoot != null)
            {
                layoutEnemy = enemyHandRoot.GetComponent<LayoutGroup>();
                if (layoutEnemy != null)
                {
                    wasEnemyLayout = layoutEnemy.enabled;
                    layoutEnemy.enabled = false;
                }
            }

            if (playerHandRoot != null)
            {
                layoutPlayer = playerHandRoot.GetComponent<LayoutGroup>();
                if (layoutPlayer != null)
                {
                    wasPlayerLayout = layoutPlayer.enabled;
                    layoutPlayer.enabled = false;
                }
            }

            try
            {
                var state = BattleManager.Instance.CurrentBattle;
                if (!_roundVisualBusy && _battle != null && _battle.IsBattleActive)
                    SetupPlayAreaRound();

                RebuildEnemyHand(state?.EnemyHand);
                RebuildPlayerHand(state?.PlayerHand);
            }
            finally
            {
                if (layoutEnemy != null && wasEnemyLayout)
                    layoutEnemy.enabled = true;
                if (layoutPlayer != null && wasPlayerLayout)
                    layoutPlayer.enabled = true;
            }

            UpdateConfirmPlayButtonInteractable();
        }

        /// <summary>
        /// 本回合需在敌方出牌区展示一张待翻开牌：由控制器 <c>PrepareEnemyPlay</c> 驱动。
        /// 被透视的牌（见 <see cref="BattleState.EnemyVisible"/>）自出现起即正面，与手牌区一致，整局保持可见（§2.2.4）。
        /// </summary>
        private void SetupPlayAreaRound()
        {
            if (_battle == null || !_battle.IsBattleActive)
            {
                ClearPlayAreaRoots();
                _enemyPlayCard = null;
                _enemyPlayCardInstanceId = null;
                return;
            }

            var bm = BattleManager.Instance;
            if (bm != null && bm.IsTutorialBattle && !_tutorialAllowPrepareEnemyPlay)
            {
                ClearPlayAreaRoots();
                _enemyPlayCard = null;
                _enemyPlayCardInstanceId = null;
                return;
            }

            if (!_battle.PrepareEnemyPlay(out _))
            {
                ClearPlayAreaRoots();
                _enemyPlayCard = null;
                _enemyPlayCardInstanceId = null;
                return;
            }

            var idx = _battle.PendingEnemyHandIndex;
            var state = BattleManager.Instance.CurrentBattle;
            if (state?.EnemyHand == null || idx < 0 || idx >= state.EnemyHand.Length)
                return;

            var card = state.EnemyHand[idx];
            if (_enemyPlayCard != null && enemyPlayArea != null && enemyPlayArea.childCount > 0 &&
                string.Equals(_enemyPlayCardInstanceId, card?.BattleInstanceId, StringComparison.Ordinal))
                return;

            ClearPlayAreaRoots();
            _enemyPlayCard = null;
            _enemyPlayCardInstanceId = null;

            CardConfigEntry cfg = null;
            if (_config != null && card != null && !string.IsNullOrEmpty(card.Id))
                _config.TryGetCard(card.Id, out cfg);

            var vm = CardViewModel.FromCard(card, cfg);
            var cv = Instantiate(cardPrefab, enemyPlayArea, false);
            cv.SetScale(PlayAreaCardScale);
            cv.Apply(vm);
            if (cfg != null)
                cv.LoadCardArtFromConfig(cfg);
            var xRayFaceUp = IsEnemyCardRevealedInXRay(card, state.EnemyVisible);
            cv.SetFaceDown(!xRayFaceUp);
            cv.OverrideClick(null);
            var bmEnemy = BattleManager.Instance;
            var enemyRestricted = bmEnemy != null && bmEnemy.IsEnemyCardRestrictedByLastPlay(card);
            cv.SetVisualState(enemyRestricted ? CardVisualState.Disabled : CardVisualState.Normal);
            _enemyPlayCard = cv;
            _enemyPlayCardInstanceId = card?.BattleInstanceId;
        }

        /// <summary>清空双方出牌区下的所有子物体。</summary>
        private void ClearPlayAreaRoots()
        {
            ClearChildTransforms(enemyPlayArea);
            ClearChildTransforms(myPlayArea);
        }

        /// <summary>从末位向前销毁子物体，避免向前遍历时索引错位。</summary>
        private static void ClearChildTransforms(RectTransform root)
        {
            if (root == null)
                return;

            for (var i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }

        /// <summary>
        /// 按模型长度重建敌方手牌槽位：未透视的牌为背面；被透视的牌（<see cref="BattleState.EnemyVisible"/>）整局正面（§2.2.4）。
        /// 本回合已移至出牌区的牌（<see cref="BattleController.PendingEnemyHandIndex"/>）不再在手牌区重复绘制。
        /// </summary>
        private void RebuildEnemyHand(Card[] hand)
        {
            var n = hand?.Length ?? 0;
            var state = BattleManager.Instance != null ? BattleManager.Instance.CurrentBattle : null;
            var visible = state?.EnemyVisible;
            var pendingEnemy = _battle != null && _battle.IsBattleActive ? _battle.PendingEnemyHandIndex : -1;
            var hideStagedInPlayArea = pendingEnemy >= 0 && _enemyPlayCard != null;

            var slot = 0;
            for (var i = 0; i < n; i++)
            {
                if (hideStagedInPlayArea && i == pendingEnemy)
                    continue;

                CardView cv;
                if (slot < enemyHandRoot.childCount)
                    cv = enemyHandRoot.GetChild(slot).GetComponent<CardView>();
                else
                {
                    cv = Instantiate(cardPrefab, enemyHandRoot, false);
                    cv.SetScale(EnemyHandCardScale);
                }

                var card = hand[i];
                if (IsEnemyCardRevealedInXRay(card, visible))
                {
                    CardConfigEntry cfg = null;
                    if (_config != null && card != null && !string.IsNullOrEmpty(card.Id))
                        _config.TryGetCard(card.Id, out cfg);

                    var vm = CardViewModel.FromCard(card, cfg);
                    cv.Apply(vm);
                    if (cfg != null)
                        cv.LoadCardArtFromConfig(cfg);
                    cv.SetFaceDown(false);
                    cv.OverrideClick(null);
                    var bmEnemy = BattleManager.Instance;
                    var enemyRestricted = bmEnemy != null && bmEnemy.IsEnemyCardRestrictedByLastPlay(card);
                    cv.SetVisualState(enemyRestricted ? CardVisualState.Disabled : CardVisualState.Normal);
                }
                else
                {
                    cv.Clear();
                    cv.SetFaceDown(true);
                }

                slot++;
            }

            TrimHandTail(enemyHandRoot, slot);
        }

        /// <summary>
        /// 己方手牌单槽视觉：指数弃牌、上回合不可再出的置灰、选中高亮。
        /// </summary>
        private CardVisualState ResolvePlayerHandCardVisualState(Card card, int handIndex)
        {
            if (_awaitingExponentialSacrifice && _battle != null)
            {
                var pend = _battle.PendingPlayerHandIndex;
                return handIndex == pend ? CardVisualState.Disabled : CardVisualState.Normal;
            }

            var bm = BattleManager.Instance;
            if (bm != null && card != null && bm.IsPlayerCardRestrictedByLastPlay(card))
                return CardVisualState.Disabled;

            return handIndex == _selectedPlayerHandIndex ? CardVisualState.Selected : CardVisualState.Normal;
        }

        /// <summary>
        /// 按模型重建己方手牌：绑定配置与立绘，设置手牌下标与点击回调（出牌意图交由控制器校验）。
        /// </summary>
        private void RebuildPlayerHand(Card[] hand)
        {
            if (_battle == null)
                return;

            var n = hand?.Length ?? 0;
            if (_selectedPlayerHandIndex >= n)
                _selectedPlayerHandIndex = -1;

            if (n > 0 && _selectedPlayerHandIndex >= 0 && _selectedPlayerHandIndex < n)
            {
                var sel = hand[_selectedPlayerHandIndex];
                var bmSel = BattleManager.Instance;
                if (sel != null && bmSel != null && bmSel.IsPlayerCardRestrictedByLastPlay(sel))
                    _selectedPlayerHandIndex = -1;
            }

            var battleRef = _battle;
            var chaoticPlay = BattleManager.Instance is { IsTutorialBattle: false, PlayerChaoticRandomPlayThisBattle: true };

            for (var i = 0; i < n; i++)
            {
                var card = hand[i];
                CardView cv;
                if (i < playerHandRoot.childCount)
                    cv = playerHandRoot.GetChild(i).GetComponent<CardView>();
                else
                {
                    cv = Instantiate(cardPrefab, playerHandRoot, false);
                    cv.SetScale(PlayerHandCardScale);
                }

                cv.SetFaceDown(false);

                CardConfigEntry cfg = null;
                if (_config != null && card != null && !string.IsNullOrEmpty(card.Id))
                    _config.TryGetCard(card.Id, out cfg);

                var vm = CardViewModel.FromCard(card, cfg);
                cv.Apply(vm);
                if (cfg != null)
                    cv.LoadCardArtFromConfig(cfg);

                cv.HandIndex = i;
                var capturedIndex = i;
                cv.OverrideClick(() => OnPlayerHandCardClicked(battleRef, capturedIndex));

                cv.SetVisualState(ResolvePlayerHandCardVisualState(card, i));

                var bm = BattleManager.Instance;
                var restricted = card != null && bm != null && bm.IsPlayerCardRestrictedByLastPlay(card);
                if (_tutorialFirstRoundGuideActive)
                {
                    var kingIdx = FindPlayerHandIndexForCardId(hand, WellKnownCardIds.King);
                    cv.SetClickInteractionEnabled(!restricted && !chaoticPlay && (kingIdx < 0 || i == kingIdx));
                }
                else
                    cv.SetClickInteractionEnabled(!restricted && !chaoticPlay);
            }

            TrimHandTail(playerHandRoot, n);
        }

        /// <summary>
        /// 玩家点击手牌：仅切换选中态（高亮）；正式出牌需再点 <see cref="confirmPlayButton"/>。
        /// </summary>
        private void OnPlayerHandCardClicked(BattleController battleRef, int capturedIndex)
        {
            if (battleRef == null || !battleRef.IsBattleActive || _roundVisualBusy)
                return;

            if (_awaitingExponentialSacrifice)
            {
                OnExponentialSacrificeHandClicked(capturedIndex);
                return;
            }

            _selectedPlayerHandIndex = _selectedPlayerHandIndex == capturedIndex ? -1 : capturedIndex;
            SyncPlayerHandSelectionVisuals();
            UpdateConfirmPlayButtonInteractable();
        }

        /// <summary>按 <see cref="_selectedPlayerHandIndex"/> 与上回合不可再出规则刷新己方手牌高亮。</summary>
        private void SyncPlayerHandSelectionVisuals()
        {
            var state = BattleManager.Instance?.CurrentBattle;
            var hand = state?.PlayerHand;
            var n = playerHandRoot != null ? playerHandRoot.childCount : 0;
            for (var i = 0; i < n; i++)
            {
                var cv = playerHandRoot.GetChild(i).GetComponent<CardView>();
                if (cv == null)
                    continue;

                var card = hand != null && i < hand.Length ? hand[i] : null;
                cv.SetVisualState(ResolvePlayerHandCardVisualState(card, i));
            }
        }

        /// <summary>
        /// 将当前选中手牌提交给控制器并启动本回合翻面与结算协程。
        /// </summary>
        private void OnConfirmPlayClicked()
        {
            if (_battle == null || !_battle.IsBattleActive || _roundVisualBusy)
                return;

            if (_selectedPlayerHandIndex < 0)
                return;

            var idx = _selectedPlayerHandIndex;
            if (!_battle.TryStagePlayerCard(idx, out var err))
            {
                Debug.LogWarning($"[BattleView] {err}");
                return;
            }

            _selectedPlayerHandIndex = -1;
            SyncPlayerHandSelectionVisuals();

            if (_battle.RequiresPlayerExponentialSacrifice())
            {
                EnterExponentialSacrificeUi();
                RefreshAll();
                UpdateConfirmPlayButtonInteractable();
                return;
            }

            UpdateConfirmPlayButtonInteractable();
            StartCoroutine(RoundVisualSequenceRoutine());
        }

        private void EnterExponentialSacrificeUi()
        {
            _awaitingExponentialSacrifice = true;
            if (exponentialSacrificeDimmer != null)
                exponentialSacrificeDimmer.SetActive(true);
            if (exponentialSacrificeHintText != null)
            {
                exponentialSacrificeHintText.gameObject.SetActive(true);
                exponentialSacrificeHintText.text = "指数形态：请点击弃置一张手牌（不能弃本回合打出的牌）";
            }

            if (playerHandCanvasOverride != null)
            {
                if (!_hasSavedPlayerHandCanvasSort)
                {
                    _savedPlayerHandCanvasSortOrder = playerHandCanvasOverride.sortingOrder;
                    _hasSavedPlayerHandCanvasSort = true;
                }

                playerHandCanvasOverride.overrideSorting = true;
                playerHandCanvasOverride.sortingOrder =
                    _savedPlayerHandCanvasSortOrder + ExponentialHandCanvasSortBoost;
            }
        }

        private void ExitExponentialSacrificeUi(bool restoreHandCanvasSort)
        {
            _awaitingExponentialSacrifice = false;
            if (exponentialSacrificeDimmer != null)
                exponentialSacrificeDimmer.SetActive(false);
            if (exponentialSacrificeHintText != null)
                exponentialSacrificeHintText.gameObject.SetActive(false);

            if (restoreHandCanvasSort && playerHandCanvasOverride != null && _hasSavedPlayerHandCanvasSort)
            {
                playerHandCanvasOverride.sortingOrder = _savedPlayerHandCanvasSortOrder;
                _hasSavedPlayerHandCanvasSort = false;
            }
        }

        private void OnExponentialSacrificeHandClicked(int handIndex)
        {
            if (!_awaitingExponentialSacrifice || _battle == null)
                return;

            if (handIndex == _battle.PendingPlayerHandIndex)
            {
                Debug.Log("[BattleView] 指数形态：不能弃置本回合打出的牌。");
                return;
            }

            if (!_battle.TryCompletePlayerExponentialSacrifice(handIndex, out var err))
            {
                Debug.LogWarning($"[BattleView] {err}");
                return;
            }

            ExitExponentialSacrificeUi(true);
            UpdateConfirmPlayButtonInteractable();
            StartCoroutine(RoundVisualSequenceRoutine());
        }

        /// <summary>
        /// 单回合演出：在己方出牌区生成对应牌 → 双方同步翻面 → 提交比大小结果 → 按结果飞往手牌/弃牌堆 → 清理并全量刷新。
        /// </summary>
        private IEnumerator RoundVisualSequenceRoutine()
        {
            _roundVisualBusy = true;
            UpdateConfirmPlayButtonInteractable();

            var playerIdx = _battle.PendingPlayerHandIndex;
            var state = BattleManager.Instance.CurrentBattle;
            var hand = state?.PlayerHand;
            if (hand == null || playerIdx < 0 || playerIdx >= hand.Length || _enemyPlayCard == null)
            {
                _roundVisualBusy = false;
                UpdateConfirmPlayButtonInteractable();
                yield break;
            }

            var pCard = hand[playerIdx];
            CardConfigEntry pCfg = null;
            if (_config != null && pCard != null && !string.IsNullOrEmpty(pCard.Id))
                _config.TryGetCard(pCard.Id, out pCfg);

            var pVm = CardViewModel.FromCard(pCard, pCfg);
            var playerPlay = Instantiate(cardPrefab, myPlayArea, false);
            playerPlay.SetScale(PlayAreaCardScale);
            playerPlay.Apply(pVm);
            if (pCfg != null)
                playerPlay.LoadCardArtFromConfig(pCfg);
            playerPlay.SetFaceDown(true);
            playerPlay.OverrideClick(null);

            // 等待一帧，确保出牌区布局与渲染就绪后再翻面；敌方被透视的牌已正面，不再播翻面动画。
            yield return null;

            var enemyIdx = _battle.PendingEnemyHandIndex;
            var enemyStaged = state.EnemyHand != null && enemyIdx >= 0 && enemyIdx < state.EnemyHand.Length
                ? state.EnemyHand[enemyIdx]
                : null;
            var enemyXRayFaceUp = IsEnemyCardRevealedInXRay(enemyStaged, state.EnemyVisible);

            if (enemyXRayFaceUp)
                yield return playerPlay.PlayRevealFlipRoutine();
            else
                yield return RunTwoEnumeratorsParallel(
                    _enemyPlayCard.PlayRevealFlipRoutine(),
                    playerPlay.PlayRevealFlipRoutine());

            if (!_battle.CommitPendingRound(out var cmp, out var cerr))
            {
                Debug.LogWarning($"[BattleView] CommitPendingRound: {cerr}");
                Destroy(playerPlay.gameObject);
                _roundVisualBusy = false;
                RefreshAll();
                yield break;
            }

            ApplyRoundOutcomeVisuals(playerPlay, _enemyPlayCard, cmp);
            var pause = Mathf.Max(0f, outcomeReadPauseSeconds);
            if (pause > 0f)
                yield return new WaitForSeconds(pause);

            ResetRoundOutcomeVisuals(playerPlay, _enemyPlayCard);
            yield return FlyOutcomeRoutine(playerPlay, _enemyPlayCard, cmp);

            Destroy(playerPlay.gameObject);
            if (_enemyPlayCard != null)
                Destroy(_enemyPlayCard.gameObject);
            _enemyPlayCard = null;
            _enemyPlayCardInstanceId = null;

            _roundVisualBusy = false;

            bool? tutorialOutcome = null;
            if (_pendingTutorialBattleVictory.HasValue)
            {
                tutorialOutcome = _pendingTutorialBattleVictory.Value;
                _pendingTutorialBattleVictory = null;
            }

            RefreshAll();
            TryScheduleChaoticAutoRound();

            TryExitCasualBattleToMainHub();

            if (tutorialOutcome.HasValue)
                StartCoroutine(TutorialPostBattleFlowRoutine(tutorialOutcome.Value));
        }

        /// <summary>
        /// 非 BOSS 战（NPC / 其他主角等）结束时：无 Run 惩罚，仅关闭战斗界面回到塔内主界面（MainView）。
        /// 需在回合演出结束后调用；BOSS 战由奖励面板或战败回标题逻辑收尾。
        /// </summary>
        private void TryExitCasualBattleToMainHub()
        {
            if (_battle != null && _battle.IsBattleActive)
                return;

            var bm = BattleManager.Instance;
            if (bm != null && bm.IsTutorialBattle)
                return;

            if (bm == null)
                return;

            var state = bm.CurrentBattle;
            if (state == null || state.IsBossBattle)
                return;

            StartCoroutine(CloseCasualBattleToMainHubRoutine());
        }

        private IEnumerator CloseCasualBattleToMainHubRoutine()
        {
            yield return null;

            if (_battle != null)
                _battle.RequestEndBattle();

            var ui = UIManager.Instance;
            if (ui != null)
                ui.Close(UIPanelId.Battle);
        }

        /// <summary>
        /// 结算后短暂停留期间：胜方 <see cref="CardVisualState.Selected"/>，败方 <see cref="CardVisualState.Disabled"/>，平局均为 Normal。
        /// </summary>
        private static void ApplyRoundOutcomeVisuals(CardView playerCard, CardView enemyCard, BattleCompareResult result)
        {
            switch (result)
            {
                case BattleCompareResult.Draw:
                    playerCard.SetVisualState(CardVisualState.Normal);
                    enemyCard.SetVisualState(CardVisualState.Normal);
                    break;
                case BattleCompareResult.FirstWins:
                    playerCard.SetVisualState(CardVisualState.Selected);
                    enemyCard.SetVisualState(CardVisualState.Disabled);
                    break;
                case BattleCompareResult.SecondWins:
                    playerCard.SetVisualState(CardVisualState.Disabled);
                    enemyCard.SetVisualState(CardVisualState.Selected);
                    break;
            }
        }

        private static void ResetRoundOutcomeVisuals(CardView playerCard, CardView enemyCard)
        {
            playerCard.SetVisualState(CardVisualState.Normal);
            enemyCard.SetVisualState(CardVisualState.Normal);
        }

        /// <summary>
        /// 根据比大小结果同时播放两张牌飞向目标区域（平局进双方弃牌堆；单方胜则胜者回手牌、败者进对方弃牌堆等，与 <see cref="BattleCompareResult"/> 语义一致）。
        /// </summary>
        private IEnumerator FlyOutcomeRoutine(CardView playerCard, CardView enemyCard, BattleCompareResult result)
        {
            switch (result)
            {
                case BattleCompareResult.Draw:
                    yield return RunTwoEnumeratorsParallel(
                        playerCard.FlyToRectTransformRoutine(playerDiscardPileAnchor),
                        enemyCard.FlyToRectTransformRoutine(enemyDiscardPileAnchor));
                    break;
                case BattleCompareResult.FirstWins:
                    yield return RunTwoEnumeratorsParallel(
                        playerCard.FlyToRectTransformRoutine(playerHandRoot),
                        enemyCard.FlyToRectTransformRoutine(enemyDiscardPileAnchor));
                    break;
                case BattleCompareResult.SecondWins:
                    yield return RunTwoEnumeratorsParallel(
                        playerCard.FlyToRectTransformRoutine(playerDiscardPileAnchor),
                        enemyCard.FlyToRectTransformRoutine(enemyHandRoot));
                    break;
            }
        }

        /// <summary>
        /// 在同一协程内并行推进两个 <see cref="IEnumerator"/>（每帧各 <c>MoveNext</c> 一次直至两者结束）。
        /// </summary>
        private static IEnumerator RunTwoEnumeratorsParallel(IEnumerator a, IEnumerator b)
        {
            while (true)
            {
                var am = a.MoveNext();
                var bm = b.MoveNext();
                if (!am && !bm)
                    break;
                yield return null;
            }
        }

        private void StopTutorialFlowCoroutine()
        {
            if (_tutorialFlowCoroutine == null)
                return;

            StopCoroutine(_tutorialFlowCoroutine);
            _tutorialFlowCoroutine = null;
        }

        private IEnumerator OpeningTutorialBattleFlowRoutine()
        {
            yield return null;

            var ui = UIManager.Instance;
            var dialogue = ServiceLocator.Get<DialogueController>();

            if (dialogue != null && ui != null)
                yield return ui.StartCoroutine(dialogue.PlayDialogue(TutorialDialogueIds.BattleIntro, null));

            RefreshAll();

            if (dialogue != null && ui != null)
                yield return ui.StartCoroutine(dialogue.PlayDialogue(TutorialDialogueIds.BattleForceKing, null));

            _tutorialAllowPrepareEnemyPlay = true;
            RefreshAll();

            EnterTutorialFirstRoundGuide();

            if (_events != null)
                _events.Subscribe<BattleRoundResolvedEvent>(OnTutorialFirstRoundResolvedExitGuide);

            while (_tutorialFirstRoundGuideActive)
                yield return null;

            if (_events != null)
                _events.Unsubscribe<BattleRoundResolvedEvent>(OnTutorialFirstRoundResolvedExitGuide);

            _tutorialFlowCoroutine = null;
        }

        private void OnTutorialBattleEndedCapture(BattleEndedEvent e)
        {
            var bm = BattleManager.Instance;
            if (bm == null || !bm.IsTutorialBattle)
                return;

            _pendingTutorialBattleVictory = e.PlayerVictory;
        }

        private void OnTutorialFirstRoundResolvedExitGuide(BattleRoundResolvedEvent _)
        {
            if (!_tutorialFirstRoundGuideActive)
                return;

            ExitTutorialFirstRoundGuide();
        }

        private void EnterTutorialFirstRoundGuide()
        {
            _tutorialFirstRoundGuideActive = true;
            if (tutorialHandDimmer != null)
                tutorialHandDimmer.SetActive(true);

            BoostPlayerHandCanvasForTutorial(true);
            SetAuxiliaryBattleButtonsInteractable(false);
            RefreshAll();
            UpdateConfirmPlayButtonInteractable();
        }

        private void ExitTutorialFirstRoundGuide()
        {
            _tutorialFirstRoundGuideActive = false;
            if (tutorialHandDimmer != null)
                tutorialHandDimmer.SetActive(false);

            BoostPlayerHandCanvasForTutorial(false);
            SetAuxiliaryBattleButtonsInteractable(true);
            RefreshAll();
            UpdateConfirmPlayButtonInteractable();
        }

        private void BoostPlayerHandCanvasForTutorial(bool enable)
        {
            if (playerHandCanvasOverride == null)
                return;

            if (enable)
            {
                if (!_hasSavedTutorialHandCanvasSort)
                {
                    _savedTutorialHandCanvasSortOrder = playerHandCanvasOverride.sortingOrder;
                    _hasSavedTutorialHandCanvasSort = true;
                }

                playerHandCanvasOverride.overrideSorting = true;
                playerHandCanvasOverride.sortingOrder =
                    _savedTutorialHandCanvasSortOrder + TutorialHandCanvasSortBoost;
            }
            else if (_hasSavedTutorialHandCanvasSort)
            {
                playerHandCanvasOverride.sortingOrder = _savedTutorialHandCanvasSortOrder;
                playerHandCanvasOverride.overrideSorting = false;
                _hasSavedTutorialHandCanvasSort = false;
            }
        }

        private void SetAuxiliaryBattleButtonsInteractable(bool interactable)
        {
            if (settingsButton != null)
                settingsButton.interactable = interactable;
            if (enemyDiscardButton != null)
                enemyDiscardButton.interactable = interactable;
            if (playerDiscardButton != null)
                playerDiscardButton.interactable = interactable;
        }

        private bool ShouldShowRoundThreePreBattleDialog()
        {
            var bm = BattleManager.Instance;
            if (bm == null || !bm.IsTutorialBattle || !bm.IsBattleActive)
                return false;

            if (_round3PreBattleDialogShown)
                return false;

            var state = bm.CurrentBattle;
            if (state == null || state.Round != 2)
                return false;

            if (state.PlayerHand == null || state.EnemyHand == null)
                return false;

            if (state.PlayerHand.Length == 0 || state.EnemyHand.Length == 0)
                return false;

            return true;
        }

        private IEnumerator RoundThreeDialogThenRefreshRoutine()
        {
            var dialogue = ServiceLocator.Get<DialogueController>();
            var ui = UIManager.Instance;
            if (dialogue != null && ui != null)
                yield return ui.StartCoroutine(dialogue.PlayDialogue(TutorialDialogueIds.BattleRound3, null));

            _round3PreBattleDialogShown = true;
            RefreshAll();
        }

        private IEnumerator TutorialPostBattleFlowRoutine(bool playerVictory)
        {
            var ui = UIManager.Instance;
            var dialogue = ServiceLocator.Get<DialogueController>();

            if (!playerVictory && dialogue != null && ui != null)
                yield return ui.StartCoroutine(dialogue.PlayDialogue(TutorialDialogueIds.BattleDefeat, null));

            if (!playerVictory && tutorialDefeatPlaceholder != null)
            {
                tutorialDefeatPlaceholder.SetActive(true);
                yield return new WaitForSecondsRealtime(0.6f);
                tutorialDefeatPlaceholder.SetActive(false);
            }

            if (_events != null)
                _events.Unsubscribe<BattleEndedEvent>(OnTutorialBattleEndedCapture);

            if (_battle != null)
                _battle.RequestEndBattle();

            if (ui != null)
                ui.Close(UIPanelId.Battle);

            if (_events != null)
                _events.Publish(new OpeningTutorialBattleFlowCompletedEvent(playerVictory));
        }

        private static bool IsEnemyCardRevealedInXRay(Card card, Card[] visibleSet)
        {
            if (card == null || visibleSet == null || visibleSet.Length == 0)
                return false;

            foreach (var v in visibleSet)
            {
                if (v != null && v.BattleInstanceId == card.BattleInstanceId)
                    return true;
            }

            return false;
        }

        private static int FindPlayerHandIndexForCardId(Card[] hand, string cardId)
        {
            if (hand == null || string.IsNullOrEmpty(cardId))
                return -1;

            for (var i = 0; i < hand.Length; i++)
            {
                var c = hand[i];
                if (c != null && string.Equals(c.Id, cardId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// 删除手牌区下标 <paramref name="desiredCount"/> 及之后的子物体（保留 0..desiredCount-1）。
        /// 先按索引快照 <see cref="GameObject"/> 再 <see cref="Destroy"/>，避免 <see cref="Destroy"/> 延迟导致
        /// 同帧内反复对同一末位下标 <see cref="Transform.GetChild(int)"/> 或 <see cref="Transform.childCount"/> 判断出现死循环 / 错删。
        /// </summary>
        private static void TrimHandTail(RectTransform handRoot, int desiredCount)
        {
            var removeCount = handRoot.childCount - desiredCount;
            if (removeCount <= 0)
                return;

            var toDestroy = new GameObject[removeCount];
            for (var k = 0; k < removeCount; k++)
                toDestroy[k] = handRoot.GetChild(desiredCount + k).gameObject;

            for (var k = 0; k < removeCount; k++)
                Destroy(toDestroy[k]);
        }
    }
}
