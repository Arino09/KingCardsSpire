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
using UnityEngine.EventSystems;
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

        /// <summary>与 <see cref="CardView"/> 内卡面基准宽度一致，用于手牌水平排布。</summary>
        private const float CardDesignWidth = 842f;

        [Header("顶部状态")]
        [SerializeField] private Text opponentNameText;
        [SerializeField] private Text turnsText;
        [SerializeField] private Text weatherText;

        [Header("天气 · 悬浮提示")]
        [SerializeField] private GameObject simpleTooltipPrefab;
        [Tooltip("为空则实例挂到天气 Text 所在 Canvas 根 RectTransform 下")]
        [SerializeField] private RectTransform weatherTooltipParentOverride;

        [Header("手牌")]
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private RectTransform enemyHandRoot;
        [SerializeField] private RectTransform playerHandRoot;

        [Header("能力牌 · 悬浮提示")]
        [Tooltip("己方手牌区旁「能力」文案 Text；悬停显示本场己方已打出能力牌效果（需在预制体 Inspector 绑定）。")]
        [SerializeField] private Text playerPlayedAbilityHoverText;

        [Tooltip("敌方手牌区旁「能力」文案 Text；悬停显示本场敌方已打出能力牌效果。")]
        [SerializeField] private Text enemyPlayedAbilityHoverText;

        [Tooltip("己方能力说明 tip 根节点（预制体内固定位置）；悬停时仅刷新正文并显示。")]
        [SerializeField] private GameObject playerPlayedAbilityTooltipRoot;

        [Tooltip("己方能力 tip 正文 Text（与 playerPlayedAbilityTooltipRoot 内自适应布局配合）。")]
        [SerializeField] private Text playerPlayedAbilityTooltipBodyText;

        [Tooltip("敌方能力说明 tip 根节点（预制体内固定位置）。")]
        [SerializeField] private GameObject enemyPlayedAbilityTooltipRoot;

        [Tooltip("敌方能力 tip 正文 Text。")]
        [SerializeField] private Text enemyPlayedAbilityTooltipBodyText;

        [Tooltip("指数形态弃牌：全屏遮罩（通常为 Mask 节点）；未绑定时仅依赖手牌 Canvas 抬升。")]
        [SerializeField] private GameObject exponentialSacrificeDimmer;

        [Tooltip("己方手牌区 Canvas（一般为 MyHand 上）。预制体上请保持 overrideSorting 关闭；仅在教学遮罩/指数弃牌遮罩时由代码临时开启。")]
        [SerializeField] private Canvas playerHandCanvasOverride;

        [Header("手牌 · 悬停预览")]
        [Tooltip("悬停时相对手牌基准缩放的倍率（须大于 1）。敌方会再乘以己方/敌方手牌基础缩放比，使悬停后与己方卡面视觉尺寸一致。")]
        [SerializeField] private float battleHandHoverScaleMultiplier = 1.35f;

        [Tooltip("悬停时 rectRoot 的 anchoredPosition 偏移（通常 Y 为正表示向上抽出）。")]
        [SerializeField] private Vector2 playerHandHoverAnchoredDelta = new(0f, 100f);

        [Tooltip("敌方透视牌悬停时的 anchoredPosition 偏移。敌方手牌区多在画面上方，Y 建议为负（向下）以免放大后顶出屏幕。")]
        [SerializeField] private Vector2 enemyHandHoverAnchoredDelta = new(0f, -90f);

        [Header("手牌 · 代码布局")]
        [Tooltip("己方：相邻两张牌中心水平间距 = 卡宽×缩放 + 本值（负值越大重叠越多）。")]
        [SerializeField] private float playerHandLayoutSlotSpacingAdd = -95f;

        [Tooltip("己方：相对扇心每偏移 1 张牌时的 Z 轴旋转（度）。")]
        [SerializeField] private float playerHandLayoutFanDegreesPerSlot = 3.5f;

        [Tooltip("己方手牌竖直偏移（相对手牌区锚点）。")]
        [SerializeField] private float playerHandLayoutAnchoredYOffset;

        [Tooltip("敌方：相邻两张牌中心水平间距 = 卡宽×缩放 + 本值。")]
        [SerializeField] private float enemyHandLayoutSlotSpacingAdd = -58f;

        [Tooltip("敌方：相对扇心每偏移 1 张牌时的 Z 轴旋转（度）。")]
        [SerializeField] private float enemyHandLayoutFanDegreesPerSlot = 4.5f;

        [Tooltip("敌方手牌竖直偏移（相对手牌区锚点）。")]
        [SerializeField] private float enemyHandLayoutAnchoredYOffset;

        [Tooltip("己方：扇形圆心在手牌区下方时，中间牌相对两侧略「拱起」的竖直幅度（像素，0 则仅旋转）。")]
        [SerializeField] private float playerHandFanVerticalBulge = 14f;

        [Tooltip("敌方：扇形圆心在手牌区上方时，中间牌相对两侧略「下凹」的竖直幅度（像素）。")]
        [SerializeField] private float enemyHandFanVerticalBulge = 12f;

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
        [Tooltip("第一回合强引导：全屏暗层；对话结束并显示本遮罩一帧后，再为手牌 Canvas 打开 override sorting 叠在其上。")]
        [SerializeField] private GameObject tutorialHandDimmer;

        [Tooltip("教学战败时曾用的全屏占位节点；战败流程已改为打开 GameOverView，此处仅确保未激活以免遮挡。")]
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

        /// <summary>手牌 Canvas 叠在全屏遮罩之上时使用的排序增量（教学强引导 / 指数弃牌共用）。</summary>
        private const int PlayerHandCanvasAboveOverlaySortDelta = 80;

        private int _playerHandCanvasBaselineSortOrder;

        private bool _playerHandCanvasSortingOverrideActive;

        /// <summary>本回合敌方出牌区实例（Prepare 后创建）。</summary>
        private CardView _enemyPlayCard;

        private string _enemyPlayCardInstanceId;

        private Coroutine _tutorialFlowCoroutine;

        private Coroutine _chaoticAutoCoroutine;

        private bool _tutorialAllowPrepareEnemyPlay;

        private bool _round2PreBattleDialogShown;

        private bool? _pendingTutorialBattleVictory;

        private bool _tutorialFirstRoundGuideActive;

        private SimpleTooltipView _weatherTooltipView;
        private RectTransform _weatherTooltipParentRect;
        private bool _battleWeatherTooltipWired;
        private UnityAction<BaseEventData> _battleWeatherPointerEnter;
        private UnityAction<BaseEventData> _battleWeatherPointerExit;

        private bool _handAbilityBattleTooltipWired;
        private UnityAction<BaseEventData> _handAbilityPlayerPointerEnter;
        private UnityAction<BaseEventData> _handAbilityPlayerPointerExit;
        private UnityAction<BaseEventData> _handAbilityEnemyPointerEnter;
        private UnityAction<BaseEventData> _handAbilityEnemyPointerExit;

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

            DisablePlayerHandCanvasSortingOverride();
            WireButtons();
            EnsureAndWireBattleWeatherTooltip();
            EnsureAndWireHandAbilityBattleTooltip();
        }

        /// <summary>
        /// 释放时取消事件订阅与按钮监听，避免泄漏与重复回调。
        /// </summary>
        public override void Dispose()
        {
            StopTutorialFlowCoroutine();
            StopChaoticAutoCoroutine();
            ExitExponentialSacrificeUi();
            UnwireBattleWeatherTooltip();
            UnwireHandAbilityBattleTooltip();
            UnwireButtons();
            if (_events != null)
            {
                _events.Unsubscribe<BattleStateChangedEvent>(OnBattleStateChanged);
                _events.Unsubscribe<BattleEndedEvent>(OnTutorialBattleEndedCapture);
            }

            base.Dispose();
        }

        private void OnDestroy()
        {
            UnwireBattleWeatherTooltip();
            UnwireHandAbilityBattleTooltip();
        }

        /// <summary>
        /// 每次打开面板：订阅战斗状态变化；若尚未开战则请求开局并全量刷新 UI。
        /// </summary>
        protected override void OnOpen()
        {
            DisablePlayerHandCanvasSortingOverride();

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
                _round2PreBattleDialogShown = false;
                _pendingTutorialBattleVictory = null;

                if (_events != null)
                {
                    _events.Unsubscribe<BattleEndedEvent>(OnTutorialBattleEndedCapture);
                    _events.Subscribe<BattleEndedEvent>(OnTutorialBattleEndedCapture);
                }

                StopTutorialFlowCoroutine();
                _tutorialFlowCoroutine = StartCoroutine(OpeningTutorialBattleFlowRoutine());

                ExitExponentialSacrificeUi();
                RefreshBattleChromeOnly();
                return;
            }

            if (_events != null)
                _events.Unsubscribe<BattleEndedEvent>(OnTutorialBattleEndedCapture);

            StopChaoticAutoCoroutine();

            if (_battle != null && !_battle.IsBattleActive)
                StartCoroutine(OpenNonTutorialBattleRoutine());

            ExitExponentialSacrificeUi();
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
                             !_awaitingExponentialSacrifice &&
                             !(BattleManager.Instance != null && BattleManager.Instance.AwaitingRegretPick) &&
                             _selectedPlayerHandIndex >= 0 && tutorialKingOk;
            confirmPlayButton.interactable = canConfirm;
        }

        private void EnsureAndWireBattleWeatherTooltip()
        {
            if (_battleWeatherTooltipWired)
                return;

            if (simpleTooltipPrefab == null || weatherText == null)
                return;

            var parent = weatherTooltipParentOverride != null
                ? weatherTooltipParentOverride
                : weatherText.canvas != null
                    ? weatherText.canvas.transform as RectTransform
                    : null;
            if (parent == null)
                return;

            var instance = Instantiate(simpleTooltipPrefab, parent);
            _weatherTooltipView = instance.GetComponent<SimpleTooltipView>();
            _weatherTooltipParentRect = parent;
            if (_weatherTooltipView == null)
            {
                Debug.LogWarning("[BattleView] SimpleTooltip 预制体根节点缺少 SimpleTooltipView。");
                Destroy(instance);
                return;
            }

            weatherText.raycastTarget = true;

            _battleWeatherPointerEnter = _ => ShowBattleWeatherTooltip();
            _battleWeatherPointerExit = _ => HideBattleWeatherTooltip();

            RegisterBattleWeatherPointerHover(weatherText.gameObject, _battleWeatherPointerEnter, _battleWeatherPointerExit);

            _battleWeatherTooltipWired = true;
        }

        private void UnwireBattleWeatherTooltip()
        {
            if (!_battleWeatherTooltipWired)
                return;

            _battleWeatherTooltipWired = false;

            UnregisterBattleWeatherPointerHover(
                weatherText != null ? weatherText.gameObject : null,
                _battleWeatherPointerEnter,
                _battleWeatherPointerExit);

            _battleWeatherPointerEnter = null;
            _battleWeatherPointerExit = null;

            if (_weatherTooltipView != null)
            {
                Destroy(_weatherTooltipView.gameObject);
                _weatherTooltipView = null;
            }

            _weatherTooltipParentRect = null;
        }

        private static void RegisterBattleWeatherPointerHover(
            GameObject target,
            UnityAction<BaseEventData> onEnter,
            UnityAction<BaseEventData> onExit)
        {
            if (target == null || onEnter == null || onExit == null)
                return;

            var trigger = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(onEnter);
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(onExit);
            trigger.triggers.Add(exit);
        }

        private static void UnregisterBattleWeatherPointerHover(
            GameObject target,
            UnityAction<BaseEventData> onEnter,
            UnityAction<BaseEventData> onExit)
        {
            if (target == null)
                return;

            var trigger = target.GetComponent<EventTrigger>();
            if (trigger == null)
                return;

            for (var i = trigger.triggers.Count - 1; i >= 0; i--)
            {
                var e = trigger.triggers[i];
                if (e.eventID == EventTriggerType.PointerEnter && onEnter != null)
                    e.callback.RemoveListener(onEnter);
                if (e.eventID == EventTriggerType.PointerExit && onExit != null)
                    e.callback.RemoveListener(onExit);
            }
        }

        private void ShowBattleWeatherTooltip()
        {
            if (_weatherTooltipView == null || weatherText == null || weatherText.canvas == null || _weatherTooltipParentRect == null)
                return;

            var bm = BattleManager.Instance;
            var state = bm != null ? bm.CurrentBattle : null;
            if (state == null)
                return;

            var body = WeatherDisplay.BuildTooltipBody(state.BattleWeather);
            _weatherTooltipView.Show(body, _weatherTooltipParentRect, weatherText.canvas);
        }

        private void HideBattleWeatherTooltip()
        {
            if (_weatherTooltipView != null)
                _weatherTooltipView.Hide();
        }

        private void EnsureAndWireHandAbilityBattleTooltip()
        {
            if (_handAbilityBattleTooltipWired)
                return;

            var playerOk = playerPlayedAbilityHoverText != null && playerPlayedAbilityTooltipRoot != null &&
                             playerPlayedAbilityTooltipBodyText != null;
            var enemyOk = enemyPlayedAbilityHoverText != null && enemyPlayedAbilityTooltipRoot != null &&
                            enemyPlayedAbilityTooltipBodyText != null;
            if (!playerOk && !enemyOk)
                return;

            if (playerOk)
            {
                playerPlayedAbilityHoverText.raycastTarget = true;
                _handAbilityPlayerPointerEnter = _ => ShowPlayerPlayedAbilityBattleTooltip();
                _handAbilityPlayerPointerExit = _ => HideHandAbilityBattleTooltip();
                RegisterBattleWeatherPointerHover(playerPlayedAbilityHoverText.gameObject,
                    _handAbilityPlayerPointerEnter, _handAbilityPlayerPointerExit);
            }

            if (enemyOk)
            {
                enemyPlayedAbilityHoverText.raycastTarget = true;
                _handAbilityEnemyPointerEnter = _ => ShowEnemyPlayedAbilityBattleTooltip();
                _handAbilityEnemyPointerExit = _ => HideHandAbilityBattleTooltip();
                RegisterBattleWeatherPointerHover(enemyPlayedAbilityHoverText.gameObject,
                    _handAbilityEnemyPointerEnter, _handAbilityEnemyPointerExit);
            }

            HideHandAbilityBattleTooltip();
            _handAbilityBattleTooltipWired = true;
        }

        private void UnwireHandAbilityBattleTooltip()
        {
            if (!_handAbilityBattleTooltipWired)
                return;

            _handAbilityBattleTooltipWired = false;
            HideHandAbilityBattleTooltip();

            UnregisterBattleWeatherPointerHover(
                playerPlayedAbilityHoverText != null ? playerPlayedAbilityHoverText.gameObject : null,
                _handAbilityPlayerPointerEnter,
                _handAbilityPlayerPointerExit);
            UnregisterBattleWeatherPointerHover(
                enemyPlayedAbilityHoverText != null ? enemyPlayedAbilityHoverText.gameObject : null,
                _handAbilityEnemyPointerEnter,
                _handAbilityEnemyPointerExit);

            _handAbilityPlayerPointerEnter = null;
            _handAbilityPlayerPointerExit = null;
            _handAbilityEnemyPointerEnter = null;
            _handAbilityEnemyPointerExit = null;
        }

        private void ShowPlayerPlayedAbilityBattleTooltip()
        {
            if (playerPlayedAbilityTooltipRoot == null || playerPlayedAbilityTooltipBodyText == null)
                return;

            var bm = BattleManager.Instance;
            if (bm == null || !bm.IsBattleActive)
                return;

            playerPlayedAbilityTooltipBodyText.text = bm.BuildPlayedAbilityBattleTooltipBody(true);
            playerPlayedAbilityTooltipRoot.SetActive(true);
        }

        private void ShowEnemyPlayedAbilityBattleTooltip()
        {
            if (enemyPlayedAbilityTooltipRoot == null || enemyPlayedAbilityTooltipBodyText == null)
                return;

            var bm = BattleManager.Instance;
            if (bm == null || !bm.IsBattleActive)
                return;

            enemyPlayedAbilityTooltipBodyText.text = bm.BuildPlayedAbilityBattleTooltipBody(false);
            enemyPlayedAbilityTooltipRoot.SetActive(true);
        }

        private void HideHandAbilityBattleTooltip()
        {
            if (playerPlayedAbilityTooltipRoot != null)
                playerPlayedAbilityTooltipRoot.SetActive(false);
            if (enemyPlayedAbilityTooltipRoot != null)
                enemyPlayedAbilityTooltipRoot.SetActive(false);
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

        private void OnSettingsClicked()
        {
            UiButtonSfx.PlayDefaultClick();
            var ui = UIManager.Instance;
            if (ui != null)
                ui.StartCoroutine(SettingsView.OpenSettingsRoutine());
        }

        /// <summary>打开卡牌列表面板并展示敌方弃牌堆。</summary>
        private void ShowEnemyDiscardClicked()
        {
            UiButtonSfx.PlayDefaultClick();
            StartCoroutine(OpenDiscardPanelAsync(true));
        }

        /// <summary>打开卡牌列表面板并展示己方弃牌堆。</summary>
        private void ShowPlayerDiscardClicked()
        {
            UiButtonSfx.PlayDefaultClick();
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

        private IEnumerator RegretPickRoutine()
        {
            yield return UIManager.Instance.OpenAsync(UIPanelId.CardList);
            if (!UIManager.Instance.TryGetView(UIPanelId.CardList, out CardListView view))
                yield break;

            var pile = BattleManager.Instance != null
                ? BattleManager.Instance.CurrentBattle.PlayerDiscard
                : Array.Empty<Card>();
            view.Apply(new CardListViewModel(
                "后悔：选择一张弃牌回到手牌",
                pile,
                false,
                1,
                selected =>
                {
                    var bm = BattleManager.Instance;
                    if (bm == null || selected == null || selected.Count == 0)
                        return;
                    var inst = selected[0].BattleInstanceId;
                    var disc = bm.CurrentBattle.PlayerDiscard;
                    var idx = -1;
                    if (disc != null)
                    {
                        for (var i = 0; i < disc.Length; i++)
                        {
                            var c = disc[i];
                            if (c != null && c.BattleInstanceId == inst)
                            {
                                idx = i;
                                break;
                            }
                        }
                    }

                    if (idx < 0)
                    {
                        Debug.LogWarning("[BattleView] 后悔：无法在弃牌堆定位所选牌");
                        return;
                    }

                    if (!bm.TryCompleteRegretPick(idx, out var err))
                        Debug.LogWarning($"[BattleView] 后悔: {err}");
                    RefreshAll();
                },
                () =>
                {
                    BattleManager.Instance?.CancelAwaitingRegretPick();
                    RefreshAll();
                },
                "点选 1 张后按确认"));
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
        /// 全量刷新：先更新外壳，再重建双方手牌（位置由代码扇形排布），最后布置本回合出牌区。
        /// </summary>
        private void RefreshAll()
        {
            RefreshBattleChromeOnly();
            DisableHandRootLayoutGroups();

            if (ShouldShowRoundTwoPreBattleDialog())
            {
                StartCoroutine(RoundTwoDialogThenRefreshRoutine());
                return;
            }

            var state = BattleManager.Instance.CurrentBattle;
            if (!_roundVisualBusy && _battle != null && _battle.IsBattleActive)
                SetupPlayAreaRound();

            RebuildEnemyHand(state?.EnemyHand);
            RebuildPlayerHand(state?.PlayerHand);

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
            var foresightFaceUp = false;
            var bmF = BattleManager.Instance;
            if (bmF != null && bmF.TryGetForesightRevealedEnemyCard(out var fs) && card != null
                && !string.IsNullOrEmpty(card.BattleInstanceId) && !string.IsNullOrEmpty(fs.BattleInstanceId)
                && string.Equals(card.BattleInstanceId, fs.BattleInstanceId, StringComparison.Ordinal))
                foresightFaceUp = true;
            cv.SetFaceDown(!xRayFaceUp && !foresightFaceUp);
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

                CardView cv = FindHandCardViewByHandIndex(enemyHandRoot, slot);
                if (cv == null)
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

                    var battleRefEnemy = _battle;
                    cv.ConfigureBattleHandHover(
                        true,
                        ResolveEnemyHandHoverScaleMultiplier(),
                        enemyHandHoverAnchoredDelta,
                        () => battleRefEnemy != null && battleRefEnemy.IsBattleActive && !_roundVisualBusy);
                }
                else
                {
                    cv.Clear();
                    cv.SetFaceDown(true);
                }

                cv.HandIndex = slot;
                slot++;
            }

            SortHandChildrenByHandIndex(enemyHandRoot, slot);
            TrimHandTail(enemyHandRoot, slot);
            ApplyManualEnemyHandLayout(slot);
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
            _battle ??= ServiceLocator.Get<BattleController>();

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
                var cv = FindHandCardViewByHandIndex(playerHandRoot, i);
                if (cv == null)
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

                var battleRefPlayer = _battle;
                cv.ConfigureBattleHandHover(
                    true,
                    battleHandHoverScaleMultiplier,
                    playerHandHoverAnchoredDelta,
                    () => battleRefPlayer != null && battleRefPlayer.IsBattleActive && !_roundVisualBusy);
            }

            SortHandChildrenByHandIndex(playerHandRoot, n);
            TrimHandTail(playerHandRoot, n);
            ApplyManualPlayerHandLayout(n);
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

            var st = BattleManager.Instance != null ? BattleManager.Instance.CurrentBattle : null;
            var hand = st?.PlayerHand;
            if (hand != null && capturedIndex >= 0 && capturedIndex < hand.Length)
            {
                var clicked = hand[capturedIndex];
                if (clicked != null && clicked.Type == CardType.Consumable)
                {
                    if (!battleRef.TryUseConsumableFromPlayerHand(capturedIndex, out var cErr))
                        Debug.LogWarning($"[BattleView] 一次性牌: {cErr}");
                    _selectedPlayerHandIndex = -1;
                    SyncPlayerHandSelectionVisuals();
                    UpdateConfirmPlayButtonInteractable();
                    RefreshAll();
                    if (BattleManager.Instance != null && BattleManager.Instance.AwaitingRegretPick)
                        StartCoroutine(RegretPickRoutine());
                    return;
                }
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
                var cv = FindHandCardViewByHandIndex(playerHandRoot, i);
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
            UiButtonSfx.PlayDefaultClick();
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

            AudioManager.Instance?.PlaySfx(AudioAddressKeys.SfxConfirmPlayCard);

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
            exponentialSacrificeDimmer.SetActive(true);
            
            exponentialSacrificeHintText.gameObject.SetActive(true);
            exponentialSacrificeHintText.text = "指数形态：请点击弃置一张手牌（不能弃本回合打出的牌）";

            EnablePlayerHandCanvasSortingOverride();
        }

        private void ExitExponentialSacrificeUi()
        {
            _awaitingExponentialSacrifice = false;
            exponentialSacrificeDimmer.SetActive(false);
            exponentialSacrificeHintText.gameObject.SetActive(false);
            DisablePlayerHandCanvasSortingOverride();
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

            ExitExponentialSacrificeUi();
            UpdateConfirmPlayButtonInteractable();
            StartCoroutine(RoundVisualSequenceRoutine());
        }

        /// <summary>
        /// 单回合演出：在己方出牌区生成对应牌 → 双方同步翻面 → 提交比大小结果 → 按结果飞往手牌/弃牌堆 → 清理并全量刷新。
        /// </summary>
        private IEnumerator RoundVisualSequenceRoutine()
        {
            _roundVisualBusy = true;
            ForceEndHandCardBattleHovers();
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

            // 结算过程中可能已直接结束战斗（最终时刻清场、预结算导致手牌清空等），此时 compare 结果无效，跳过飞牌以免错误演出。
            if (!_battle.IsBattleActive)
            {
                Destroy(playerPlay.gameObject);
                if (_enemyPlayCard != null)
                    Destroy(_enemyPlayCard.gameObject);
                _enemyPlayCard = null;
                _enemyPlayCardInstanceId = null;
                _roundVisualBusy = false;

                bool? tutorialOutcomeEarly = null;
                if (_pendingTutorialBattleVictory.HasValue)
                {
                    tutorialOutcomeEarly = _pendingTutorialBattleVictory.Value;
                    _pendingTutorialBattleVictory = null;
                }

                RefreshAll();
                TryScheduleChaoticAutoRound();
                TryExitCasualBattleToMainHub();

                if (tutorialOutcomeEarly.HasValue)
                    StartCoroutine(TutorialPostBattleFlowRoutine(tutorialOutcomeEarly.Value));
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

            var ui = UIManager.Instance;
            var bm = BattleManager.Instance;
            var gm = GameManager.Instance;
            var hasCasualOffer = bm != null && bm.PendingCasualVictoryRewardCardIds != null &&
                                 bm.PendingCasualVictoryRewardCardIds.Count > 0;
            var hasHeroPickThree = bm != null && bm.PendingHeroDuelPickThreeCardIds != null &&
                                   bm.PendingHeroDuelPickThreeCardIds.Count > 0;
            var hasHeroRemoveStorage = gm != null && gm.IsHeroDuelStorageRemovalRewardPending;

            if (ui != null && (hasCasualOffer || hasHeroPickThree || hasHeroRemoveStorage))
            {
                yield return ui.OpenAsync(UIPanelId.CardReward);
                while (ui.IsPanelOpen(UIPanelId.CardReward))
                    yield return null;
            }

            if (_battle != null)
                _battle.RequestEndBattle();

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

            PlayDiscardPileSfxForRoundResult(result);
        }

        private static void PlayDiscardPileSfxForRoundResult(BattleCompareResult result)
        {
            var am = AudioManager.Instance;
            if (am == null)
                return;

            switch (result)
            {
                case BattleCompareResult.Draw:
                    am.PlaySfx(AudioAddressKeys.SfxCardToDiscard);
                    am.PlaySfx(AudioAddressKeys.SfxCardToDiscard);
                    break;
                case BattleCompareResult.FirstWins:
                case BattleCompareResult.SecondWins:
                    am.PlaySfx(AudioAddressKeys.SfxCardToDiscard);
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

            var game = GameManager.Instance;
            while (game != null && game.DeferOpeningTutorialBattleIntro)
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
            yield return null;
            EnablePlayerHandCanvasSortingOverride();

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

            SetAuxiliaryBattleButtonsInteractable(false);
            RefreshAll();
            UpdateConfirmPlayButtonInteractable();
        }

        private void ExitTutorialFirstRoundGuide()
        {
            _tutorialFirstRoundGuideActive = false;
            if (tutorialHandDimmer != null)
                tutorialHandDimmer.SetActive(false);

            DisablePlayerHandCanvasSortingOverride();
            SetAuxiliaryBattleButtonsInteractable(true);
            RefreshAll();
            UpdateConfirmPlayButtonInteractable();
        }

        /// <summary>在全屏遮罩之上显示手牌（教学 / 指数弃牌）；与 <see cref="DisablePlayerHandCanvasSortingOverride"/> 成对。</summary>
        private void EnablePlayerHandCanvasSortingOverride()
        {
            if (playerHandCanvasOverride == null)
                return;

            if (!_playerHandCanvasSortingOverrideActive)
            {
                _playerHandCanvasBaselineSortOrder = playerHandCanvasOverride.sortingOrder;
                _playerHandCanvasSortingOverrideActive = true;
            }

            playerHandCanvasOverride.overrideSorting = true;
            playerHandCanvasOverride.sortingOrder =
                _playerHandCanvasBaselineSortOrder + PlayerHandCanvasAboveOverlaySortDelta;
        }

        private void DisablePlayerHandCanvasSortingOverride()
        {
            if (playerHandCanvasOverride == null)
                return;

            if (_playerHandCanvasSortingOverrideActive)
            {
                playerHandCanvasOverride.sortingOrder = _playerHandCanvasBaselineSortOrder;
                _playerHandCanvasSortingOverrideActive = false;
            }

            playerHandCanvasOverride.overrideSorting = false;
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

        private bool ShouldShowRoundTwoPreBattleDialog()
        {
            var bm = BattleManager.Instance;
            if (bm == null || !bm.IsTutorialBattle || !bm.IsBattleActive)
                return false;

            if (_round2PreBattleDialogShown)
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

        private IEnumerator RoundTwoDialogThenRefreshRoutine()
        {
            var dialogue = ServiceLocator.Get<DialogueController>();
            var ui = UIManager.Instance;
            if (dialogue != null && ui != null)
                yield return ui.StartCoroutine(dialogue.PlayDialogue(TutorialDialogueIds.BattleRound2, null));

            _round2PreBattleDialogShown = true;
            RefreshAll();
        }

        private IEnumerator TutorialPostBattleFlowRoutine(bool playerVictory)
        {
            var ui = UIManager.Instance;
            var dialogue = ServiceLocator.Get<DialogueController>();

            if (dialogue != null && ui != null)
            {
                var dialogueId = playerVictory
                    ? TutorialDialogueIds.BattleVictory
                    : TutorialDialogueIds.BattleDefeat;
                yield return ui.StartCoroutine(dialogue.PlayDialogue(dialogueId, null));
            }

            if (!playerVictory && ui != null)
            {
                if (tutorialDefeatPlaceholder != null)
                    tutorialDefeatPlaceholder.SetActive(false);

                yield return ui.OpenAsync(UIPanelId.GameOver);
                while (ui.IsPanelOpen(UIPanelId.GameOver))
                    yield return null;
            }

            if (_events != null)
                _events.Unsubscribe<BattleEndedEvent>(OnTutorialBattleEndedCapture);

            if (_battle != null)
                _battle.RequestEndBattle();

            // 不在此处关闭战斗界面：由 MainMenuView 在打开主菜单/主塔界面之后再关，避免 Addressables
            // 加载下一面板期间出现一帧「无全屏 UI」而闪底/闪屏。
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

        private void ForceEndHandCardBattleHovers()
        {
            ForceEndBattleHandHoversInRoot(playerHandRoot);
            ForceEndBattleHandHoversInRoot(enemyHandRoot);
        }

        /// <summary>
        /// 敌方手牌 <see cref="EnemyHandCardScale"/> 小于己方，用同一 Inspector 倍率时悬停仍显小；
        /// 按比例放大悬停倍率，使 <c>SetScale</c> 结果与己方悬停一致。
        /// </summary>
        private float ResolveEnemyHandHoverScaleMultiplier()
        {
            return battleHandHoverScaleMultiplier * (PlayerHandCardScale / EnemyHandCardScale);
        }

        private static void ForceEndBattleHandHoversInRoot(RectTransform root)
        {
            if (root == null)
                return;

            for (var i = 0; i < root.childCount; i++)
            {
                var cv = root.GetChild(i).GetComponent<CardView>();
                cv?.ForceEndBattleHandHover();
            }
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

        private void DisableHandRootLayoutGroups()
        {
            DisableLayoutGroupOnRoot(enemyHandRoot);
            DisableLayoutGroupOnRoot(playerHandRoot);
        }

        private static void DisableLayoutGroupOnRoot(RectTransform handRoot)
        {
            if (handRoot == null)
                return;

            var lg = handRoot.GetComponent<LayoutGroup>();
            if (lg != null)
                lg.enabled = false;
        }

        private void ApplyManualPlayerHandLayout(int count)
        {
            ApplyManualFanHandLayout(
                playerHandRoot,
                count,
                CardDesignWidth,
                PlayerHandCardScale,
                playerHandLayoutSlotSpacingAdd,
                playerHandLayoutFanDegreesPerSlot,
                playerHandLayoutAnchoredYOffset,
                invertFanZ: true,
                playerHandFanVerticalBulge,
                fanCenterBelowHandZone: true);
        }

        private void ApplyManualEnemyHandLayout(int count)
        {
            ApplyManualFanHandLayout(
                enemyHandRoot,
                count,
                CardDesignWidth,
                EnemyHandCardScale,
                enemyHandLayoutSlotSpacingAdd,
                enemyHandLayoutFanDegreesPerSlot,
                enemyHandLayoutAnchoredYOffset,
                invertFanZ: false,
                enemyHandFanVerticalBulge,
                fanCenterBelowHandZone: false);
        }

        /// <summary>
        /// 手牌区不使用 <see cref="LayoutGroup"/>：水平居中、绕 Z 扇形旋转，并可选竖直抛物线。
        /// 己方：<paramref name="fanCenterBelowHandZone"/> 为 true 表示圆心在区域下方（弧口朝上）；敌方传 false 表示圆心在区域上方（弧口朝下）。
        /// </summary>
        private static void ApplyManualFanHandLayout(
            RectTransform handRoot,
            int count,
            float cardDesignWidth,
            float cardScale,
            float slotSpacingAdd,
            float zDegreesPerSlotFromCenter,
            float anchoredYOffset,
            bool invertFanZ,
            float verticalBulge,
            bool fanCenterBelowHandZone)
        {
            if (handRoot == null || count <= 0)
                return;

            var slotCenterStep = cardDesignWidth * cardScale + slotSpacingAdd;
            var totalSpan = slotCenterStep * Mathf.Max(0, count - 1);
            var zSign = invertFanZ ? -1f : 1f;
            var maxFrom = (count - 1) * 0.5f;

            for (var i = 0; i < count; i++)
            {
                var child = FindHandCardRectByHandIndex(handRoot, i);
                if (child == null)
                    continue;

                var fromCenter = i - maxFrom;
                var x = -totalSpan * 0.5f + i * slotCenterStep;
                var zRot = zSign * zDegreesPerSlotFromCenter * fromCenter;

                var parab = 0f;
                if (count > 1 && maxFrom > 0.001f)
                    parab = 1f - (fromCenter / maxFrom) * (fromCenter / maxFrom);

                var yArc = (fanCenterBelowHandZone ? 1f : -1f) * verticalBulge * parab;
                var y = anchoredYOffset + yArc;

                child.localEulerAngles = new Vector3(0f, 0f, zRot);
                child.anchoredPosition = new Vector2(x, y);
            }
        }

        /// <summary>悬停置顶会打乱 sibling 顺序，手牌逻辑下标需用 <see cref="CardView.HandIndex"/> 解析。</summary>
        private static RectTransform FindHandCardRectByHandIndex(RectTransform handRoot, int handIndex)
        {
            var cv = FindHandCardViewByHandIndex(handRoot, handIndex);
            return cv != null ? cv.transform as RectTransform : null;
        }

        private static CardView FindHandCardViewByHandIndex(RectTransform handRoot, int handIndex)
        {
            if (handRoot == null)
                return null;

            for (var c = 0; c < handRoot.childCount; c++)
            {
                var cv = handRoot.GetChild(c).GetComponent<CardView>();
                if (cv != null && cv.HandIndex == handIndex)
                    return cv;
            }

            return null;
        }

        /// <summary>将 <see cref="CardView.HandIndex"/> 为 0..count-1 的卡牌依次排到子节点前部，便于 <see cref="TrimHandTail"/> 删除多余实例。</summary>
        private static void SortHandChildrenByHandIndex(RectTransform handRoot, int count)
        {
            if (handRoot == null || count <= 0)
                return;

            for (var target = 0; target < count; target++)
            {
                var cv = FindHandCardViewByHandIndex(handRoot, target);
                if (cv == null)
                    continue;

                cv.transform.SetSiblingIndex(target);
            }
        }
    }
}
