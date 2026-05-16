using System;
using System.Collections;
using System.Text;
using KingCardsSpire.Configs;
using KingCardsSpire.Controllers;
using KingCardsSpire.Core;
using KingCardsSpire.Core.Events;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>
    /// 塔内主界面：状态栏、操作入口、历史记录区；对应预制体 MainView。
    /// </summary>
    public sealed class MainHubView : BaseView
    {
        [Header("顶部状态 · Text")]
        [SerializeField] private Text levelText;
        [SerializeField] private Text curDayText;
        [SerializeField] private Text weatherText;
        [SerializeField] private Text coinText;
        [SerializeField] private Text buffText;

        [Header("状态 · 悬浮提示")]
        [SerializeField] private GameObject simpleTooltipPrefab;
        [Tooltip("为空则实例挂到天气 Text 所在 Canvas 根 RectTransform 下")]
        [SerializeField] private RectTransform tooltipParentOverride;

        [Header("历史记录")]
        [SerializeField] private Text historyText;

        [Header("顶部 · 按钮")]
        [SerializeField] private Button deckButton;
        [SerializeField] private Button settingsButton;

        [SerializeField] private Image bg;

        [Header("中部 · 按钮")]
        [SerializeField] private Button bossButton;
        [SerializeField] private Button visitHeroButton;
        [SerializeField] private Button visitNpcButton;
        [SerializeField] private Button visitedNpcButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button nextDayButton;

        private GameManager _game;
        private PersistenceManager _persistence;
        private EventManager _events;

        private UnityAction _onDeck;
        private UnityAction _onSettings;
        private UnityAction _onBoss;
        private UnityAction _onVisitHero;
        private UnityAction _onVisitNpc;
        private UnityAction _onVisitedNpc;
        private UnityAction _onShop;
        private UnityAction _onNextDay;

        private bool _wired;

        private Coroutine _floorBgRoutine;
        private Coroutine _buffDraftOfferRoutine;
        private AsyncOperationHandle<Sprite> _floorBgLoadHandle;
        private bool _hasFloorBgLoadHandle;

        private SimpleTooltipView _tooltipView;
        private RectTransform _tooltipParentRect;
        private bool _statusTooltipsWired;
        private UnityAction<BaseEventData> _weatherPointerEnter;
        private UnityAction<BaseEventData> _weatherPointerExit;
        private UnityAction<BaseEventData> _buffPointerEnter;
        private UnityAction<BaseEventData> _buffPointerExit;

        /// <summary>
        /// 供 <see cref="UIManager"/> 在淡出 Loading 前等待：当前楼层背景 Addressables 加载协程结束（含 <see cref="OnOpen"/> 内再次触发的刷新）。
        /// </summary>
        public IEnumerator WaitForInitialHubPresentationReady()
        {
            while (_floorBgRoutine != null)
                yield return null;
        }

        public override void Initialize()
        {
            SetPanelId(UIPanelId.MainHub);
            _game = GameManager.Instance;
            _persistence = PersistenceManager.Instance;
            _events = EventManager.Instance;

            WireButtons();
            SubscribeEvents();
            RefreshAll();
            EnsureAndWireStatusTooltips();
            _wired = true;
        }

        public override void Dispose()
        {
            StopBuffDraftOfferRoutine();
            CancelFloorBackgroundLoad();
            UnwireStatusTooltips();
            UnsubscribeEvents();
            UnwireButtons();
            base.Dispose();
        }

        private void OnDestroy()
        {
            StopBuffDraftOfferRoutine();
            CancelFloorBackgroundLoad();
            UnwireStatusTooltips();
            if (_wired)
            {
                UnsubscribeEvents();
                UnwireButtons();
            }
        }

        protected override void OnOpen()
        {
            RefreshAll();
            ScheduleBuffDraftIfNeeded();
        }

        private void WireButtons()
        {
            UnwireButtons();

            _onDeck = OnDeckClicked;
            _onSettings = OnSettingsClicked;
            _onBoss = () =>
            {
                UiButtonSfx.PlayDefaultClick();
                StartCoroutine(OpenBossBattleRoutine());
            };
            _onVisitHero = () =>
            {
                UiButtonSfx.PlayDefaultClick();
                StartCoroutine(OpenPanelRoutine(UIPanelId.HeroRoom));
            };
            _onVisitNpc = () =>
            {
                UiButtonSfx.PlayDefaultClick();
                StartCoroutine(OpenPanelRoutine(UIPanelId.NpcHub));
            };
            _onVisitedNpc = () =>
            {
                UiButtonSfx.PlayDefaultClick();
                StartCoroutine(OpenPanelRoutine(UIPanelId.NpcRecord));
            };
            _onShop = () =>
            {
                UiButtonSfx.PlayDefaultClick();
                StartCoroutine(OpenPanelRoutine(UIPanelId.Shop));
            };
            _onNextDay = OnNextDayClicked;

            AddClick(deckButton, _onDeck);
            AddClick(settingsButton, _onSettings);
            AddClick(bossButton, _onBoss);
            AddClick(visitHeroButton, _onVisitHero);
            AddClick(visitNpcButton, _onVisitNpc);
            AddClick(visitedNpcButton, _onVisitedNpc);
            AddClick(shopButton, _onShop);
            AddClick(nextDayButton, _onNextDay);
        }

        private static void AddClick(Button btn, UnityAction action)
        {
            if (btn == null || action == null)
                return;
            btn.onClick.AddListener(action);
        }

        private void UnwireButtons()
        {
            RemoveClick(deckButton, _onDeck);
            RemoveClick(settingsButton, _onSettings);
            RemoveClick(bossButton, _onBoss);
            RemoveClick(visitHeroButton, _onVisitHero);
            RemoveClick(visitNpcButton, _onVisitNpc);
            RemoveClick(visitedNpcButton, _onVisitedNpc);
            RemoveClick(shopButton, _onShop);
            RemoveClick(nextDayButton, _onNextDay);

            _onDeck = null;
            _onSettings = null;
            _onBoss = null;
            _onVisitHero = null;
            _onVisitNpc = null;
            _onVisitedNpc = null;
            _onShop = null;
            _onNextDay = null;
        }

        private static void RemoveClick(Button btn, UnityAction action)
        {
            if (btn == null || action == null)
                return;
            btn.onClick.RemoveListener(action);
        }

        private void SubscribeEvents()
        {
            if (_events == null)
                return;

            _events.Subscribe<DayChangedEvent>(OnDayChanged);
            _events.Subscribe<FloorChangedEvent>(OnFloorChanged);
            _events.Subscribe<GoldChangedEvent>(OnGoldChanged);
            _events.Subscribe<WeatherChangedEvent>(OnWeatherChanged);
            _events.Subscribe<SaveWrittenEvent>(OnSaveWritten);
            _events.Subscribe<BossRewardOfferedEvent>(OnBossRewardOffered);
            _events.Subscribe<NpcEncounterStartedEvent>(OnNpcEncounterStarted);
            _events.Subscribe<BuffAcquiredEvent>(OnBuffAcquired);
            _events.Subscribe<GameOverEvent>(OnGameOverForHub);
            _events.Subscribe<GameVictoryEvent>(OnGameVictoryForHub);
        }

        private void UnsubscribeEvents()
        {
            if (_events == null)
                return;

            _events.Unsubscribe<DayChangedEvent>(OnDayChanged);
            _events.Unsubscribe<FloorChangedEvent>(OnFloorChanged);
            _events.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            _events.Unsubscribe<WeatherChangedEvent>(OnWeatherChanged);
            _events.Unsubscribe<SaveWrittenEvent>(OnSaveWritten);
            _events.Unsubscribe<BossRewardOfferedEvent>(OnBossRewardOffered);
            _events.Unsubscribe<NpcEncounterStartedEvent>(OnNpcEncounterStarted);
            _events.Unsubscribe<BuffAcquiredEvent>(OnBuffAcquired);
            _events.Unsubscribe<GameOverEvent>(OnGameOverForHub);
            _events.Unsubscribe<GameVictoryEvent>(OnGameVictoryForHub);
        }

        private void OnDayChanged(DayChangedEvent _)
        {
            RefreshStatusTexts();
            RefreshVisitNpcButtonAccess();
            RefreshNextDayButtonAccess();
        }

        private void OnBuffAcquired(BuffAcquiredEvent _) => RefreshStatusTexts();

        private void OnGameOverForHub(GameOverEvent _) => RefreshNextDayButtonAccess();

        private void OnGameVictoryForHub(GameVictoryEvent _) => RefreshNextDayButtonAccess();

        private void OnFloorChanged(FloorChangedEvent _)
        {
            RefreshStatusTexts();
            RefreshFloorBackground();
            RefreshVisitNpcButtonAccess();
            RefreshNextDayButtonAccess();
            // 进层时 MainHub 通常未关闭，OnOpen 不会再次执行，需在此补一次 Buff 弹窗判定
            ScheduleBuffDraftIfNeeded();
        }

        private void ScheduleBuffDraftIfNeeded()
        {
            StopBuffDraftOfferRoutine();
            _buffDraftOfferRoutine = StartCoroutine(MaybeOfferBuffDraftAfterFloorRoutine());
        }

        private void StopBuffDraftOfferRoutine()
        {
            if (_buffDraftOfferRoutine == null)
                return;
            StopCoroutine(_buffDraftOfferRoutine);
            _buffDraftOfferRoutine = null;
        }

        private IEnumerator MaybeOfferBuffDraftAfterFloorRoutine()
        {
            try
            {
                var gm = _game ?? GameManager.Instance;
                var ui = UIManager.Instance;
                if (gm == null || ui == null)
                    yield break;
                if (!gm.ShouldOfferBuffDraft())
                    yield break;

                gm.TryBuildBuffDraftOffer();
                yield return ui.OpenAsync(UIPanelId.BuffRewardView);
                while (ui.IsPanelOpen(UIPanelId.BuffRewardView))
                    yield return null;

                RefreshStatusTexts();
            }
            finally
            {
                _buffDraftOfferRoutine = null;
            }
        }

        private void OnGoldChanged(GoldChangedEvent _) => RefreshStatusTexts();

        private void OnWeatherChanged(WeatherChangedEvent _) => RefreshStatusTexts();

        private void OnSaveWritten(SaveWrittenEvent _) => RefreshHistory();

        private void OnBossRewardOffered(BossRewardOfferedEvent _) => StartCoroutine(OpenBossRewardRoutine());

        private void OnNpcEncounterStarted(NpcEncounterStartedEvent _) => RefreshVisitNpcButtonAccess();

        private IEnumerator OpenBossRewardRoutine()
        {
            var ui = UIManager.Instance;
            if (ui == null)
                yield break;

            yield return ui.OpenAsync(UIPanelId.CardReward);
        }

        private void RefreshAll()
        {
            RefreshFloorBackground();
            RefreshStatusTexts();
            RefreshHistory();
            RefreshVisitNpcButtonAccess();
            RefreshNextDayButtonAccess();
        }

        private void RefreshVisitNpcButtonAccess()
        {
            var gm = _game ?? GameManager.Instance;
            visitNpcButton.interactable = gm != null && gm.HasNpcVisitRemainingToday();
        }

        private void RefreshNextDayButtonAccess()
        {
            var gm = _game ?? GameManager.Instance;
            if (gm == null)
            {
                nextDayButton.interactable = false;
                return;
            }

            var maxDays = gm.GetMaxDaysPerFloor();
            var canAdvance = !gm.IsGameOver && !gm.IsRunVictory &&
                gm.PlayerState.FloorDay < maxDays;
            nextDayButton.interactable = canAdvance;

            // 从本按钮 onClick 内同步置灰时，若仍保持为 EventSystem 选中对象，部分环境下 Inspector/视觉状态会异常。
            if (!canAdvance && EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == nextDayButton.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }

        /// <summary>
        /// 按当前楼层从 Addressables 加载 <c>Assets/GameAssets/Sprites/UI/FloorBg/</c> 下对应资源（扩展名按资源实际类型区分）。
        /// 主界面已可见且无战斗/奖励遮挡时，用 Loading 遮住避免取消旧图时空 Sprite 闪屏。
        /// </summary>
        private void RefreshFloorBackground()
        {
            if (_game == null)
                return;

            var ui = UIManager.Instance;
            if (ui != null && !ui.IsBlockingPanelAboveMainHub() && !ui.IsMainHubLoadingMaskHeld)
            {
                StartCoroutine(RefreshFloorBackgroundWithOptionalLoadingRoutine());
                return;
            }

            CancelFloorBackgroundLoad();
            _floorBgRoutine = StartCoroutine(LoadFloorBackgroundRoutine());
        }

        private IEnumerator RefreshFloorBackgroundWithOptionalLoadingRoutine()
        {
            var ui = UIManager.Instance;
            if (ui == null || _game == null)
                yield break;

            yield return ui.StartCoroutine(ui.PushMainHubLoadingMaskRoutine());

            CancelFloorBackgroundLoad();
            _floorBgRoutine = StartCoroutine(LoadFloorBackgroundRoutine());

            yield return WaitForInitialHubPresentationReady();
            yield return null;

            yield return ui.StartCoroutine(ui.PopMainHubLoadingMaskRoutine());
        }

        private void CancelFloorBackgroundLoad()
        {
            if (_floorBgRoutine != null)
            {
                StopCoroutine(_floorBgRoutine);
                _floorBgRoutine = null;
            }

            ReleaseFloorBgAddressableHandle();
        }

        private void ReleaseFloorBgAddressableHandle()
        {
            if (!_hasFloorBgLoadHandle)
                return;

            bg.sprite = null;

            if (_floorBgLoadHandle.IsValid())
                Addressables.Release(_floorBgLoadHandle);

            _hasFloorBgLoadHandle = false;
        }

        private IEnumerator LoadFloorBackgroundRoutine()
        {
            var floor = Mathf.Clamp(_game.FloorState.FloorIndex, 1, 7);
            var address = $"Sprites/UI/FloorBg/{floor}.jpg";

            var handle = Addressables.LoadAssetAsync<Sprite>(address);
            _floorBgLoadHandle = handle;
            _hasFloorBgLoadHandle = true;

            yield return handle;

            _floorBgRoutine = null;

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                bg.sprite = handle.Result;
            }
            else
            {
                Debug.LogWarning(
                    $"[MainHub] 楼层背景加载失败 address={address} status={handle.Status} ex={handle.OperationException}");
                ReleaseFloorBgAddressableHandle();
            }
        }

        private void RefreshStatusTexts()
        {
            var gm = _game ?? GameManager.Instance;
            var player = gm != null ? gm.PlayerState : null;
            if (player == null)
                return;

            levelText.text = $"{player.CurrentFloor}/{gm.GameConfig.TowerFloors}";
            curDayText.text = $"{player.FloorDay}/{gm.GetMaxDaysPerFloor()}";
            weatherText.text = WeatherDisplay.Format(player.CurrentWeather);
            coinText.text = player.Gold.ToString();
            buffText.text = "Buff";
        }

        private void RefreshHistory()
        {
            if (historyText == null)
                return;

            var sb = new StringBuilder();
            var save = _persistence != null ? _persistence.Load() : null;
            if (save?.History != null && save.History.Length > 0)
            {
                foreach (var h in save.History)
                {
                    if (h == null || string.IsNullOrEmpty(h.Summary))
                        continue;
                    sb.AppendLine($"第{h.Day}天：{h.Summary}");
                }
            }

            if (sb.Length == 0)
                sb.Append("暂无历史记录（推进进度或存档后将显示）。");

            historyText.text = sb.ToString().TrimEnd();
        }

        private void OnDeckClicked()
        {
            UiButtonSfx.PlayDefaultClick();
            StartCoroutine(OpenDeckStorageRoutine());
        }

        /// <summary>
        /// 打开牌组仓库交换界面（出战卡组与仓库互移）。
        /// </summary>
        private IEnumerator OpenDeckStorageRoutine()
        {
            yield return UIManager.Instance.OpenAsync(UIPanelId.DeckStorage);
            if (!UIManager.Instance.TryGetView(UIPanelId.DeckStorage, out DeckStorageView view))
                yield break;

            view.RefreshGrids();
        }

        private void OnSettingsClicked()
        {
            UiButtonSfx.PlayDefaultClick();
            var ui = UIManager.Instance;
            if (ui != null)
                ui.StartCoroutine(SettingsView.OpenSettingsRoutine());
        }

        /// <summary>进入下一天：调用 <see cref="GameManager.AdvanceDay"/>，不打开其他界面。</summary>
        private void OnNextDayClicked()
        {
            UiButtonSfx.PlayDefaultClick();
            var gm = _game ?? GameManager.Instance;
            if (gm == null)
                return;

            gm.AdvanceDay();
            RefreshNextDayButtonAccess();
        }

        private IEnumerator OpenBossBattleRoutine()
        {
            var ui = UIManager.Instance;
            var ctrl = ServiceLocator.Get<BattleController>();
            var dialogue = ServiceLocator.Get<DialogueController>();
            var cfg = ConfigManager.Instance;
            var gm = _game ?? GameManager.Instance;
            if (ui == null || ctrl == null)
                yield break;

            if (dialogue != null && gm != null && cfg != null)
            {
                var bossId = gm.FloorState != null ? gm.FloorState.BossId : null;
                var startId = DialogueController.ResolveBossPreFightStartId(cfg, gm.PlayerState.CurrentFloor, bossId);
                yield return ui.StartCoroutine(dialogue.PlayDialogue(startId, null));
            }

            if (gm != null && gm.HasBuff(BuffId.ChaoticBattlefield))
                yield return gm.RunChaoticBattlefieldPreBattlePickRoutine();

            ctrl.RequestStartBattle(true);
            yield return ui.OpenAsync(UIPanelId.Battle);
        }

        private IEnumerator OpenPanelRoutine(UIPanelId panelId)
        {
            var ui = UIManager.Instance;
            if (ui == null)
                yield break;

            yield return ui.OpenAsync(panelId);
        }

        private void EnsureAndWireStatusTooltips()
        {
            if (_statusTooltipsWired)
                return;

            if (simpleTooltipPrefab == null || weatherText == null || buffText == null)
                return;

            var parent = tooltipParentOverride != null
                ? tooltipParentOverride
                : weatherText.canvas != null
                    ? weatherText.canvas.transform as RectTransform
                    : null;
            if (parent == null)
                return;

            var instance = Instantiate(simpleTooltipPrefab, parent);
            _tooltipView = instance.GetComponent<SimpleTooltipView>();
            _tooltipParentRect = parent;
            if (_tooltipView == null)
            {
                Debug.LogWarning("[MainHubView] SimpleTooltip 预制体根节点缺少 SimpleTooltipView。");
                Destroy(instance);
                return;
            }

            weatherText.raycastTarget = true;
            buffText.raycastTarget = true;

            _weatherPointerEnter = _ => ShowWeatherStatusTooltip();
            _weatherPointerExit = _ => HideStatusTooltip();
            _buffPointerEnter = _ => ShowBuffStatusTooltip();
            _buffPointerExit = _ => HideStatusTooltip();

            RegisterPointerHover(weatherText.gameObject, _weatherPointerEnter, _weatherPointerExit);
            RegisterPointerHover(buffText.gameObject, _buffPointerEnter, _buffPointerExit);

            _statusTooltipsWired = true;
        }

        private void UnwireStatusTooltips()
        {
            if (!_statusTooltipsWired)
                return;

            _statusTooltipsWired = false;

            UnregisterPointerHover(weatherText != null ? weatherText.gameObject : null, _weatherPointerEnter, _weatherPointerExit);
            UnregisterPointerHover(buffText != null ? buffText.gameObject : null, _buffPointerEnter, _buffPointerExit);

            _weatherPointerEnter = null;
            _weatherPointerExit = null;
            _buffPointerEnter = null;
            _buffPointerExit = null;

            if (_tooltipView != null)
            {
                Destroy(_tooltipView.gameObject);
                _tooltipView = null;
            }

            _tooltipParentRect = null;
        }

        private static void RegisterPointerHover(GameObject target, UnityAction<BaseEventData> onEnter, UnityAction<BaseEventData> onExit)
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

        private static void UnregisterPointerHover(GameObject target, UnityAction<BaseEventData> onEnter, UnityAction<BaseEventData> onExit)
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

        private void ShowWeatherStatusTooltip()
        {
            if (_tooltipView == null || weatherText == null || weatherText.canvas == null || _tooltipParentRect == null)
                return;

            var gm = _game ?? GameManager.Instance;
            var player = gm != null ? gm.PlayerState : null;
            if (player == null)
                return;

            var body = WeatherDisplay.BuildTooltipBody(player.CurrentWeather);
            _tooltipView.Show(body, _tooltipParentRect, weatherText.canvas);
        }

        private void ShowBuffStatusTooltip()
        {
            if (_tooltipView == null || buffText == null || buffText.canvas == null || _tooltipParentRect == null)
                return;

            var gm = _game ?? GameManager.Instance;
            var player = gm != null ? gm.PlayerState : null;
            if (player == null)
                return;

            var body = BuildBuffTooltipBody(player);
            _tooltipView.Show(body, _tooltipParentRect, buffText.canvas);
        }

        private void HideStatusTooltip()
        {
            if (_tooltipView != null)
                _tooltipView.Hide();
        }

        private static string BuildBuffTooltipBody(PlayerData player)
        {
            var arr = player?.ActiveBuffs;
            if (arr == null || arr.Length == 0)
                return "当前无生效 Buff。";

            var cfgMgr = ConfigManager.Instance;
            var sb = new StringBuilder();
            for (var i = 0; i < arr.Length; i++)
            {
                var id = arr[i];
                if (id == BuffId.None)
                    continue;

                var name = cfgMgr != null ? cfgMgr.ResolveBuffDisplayName(id) : id.ToString();
                var desc = cfgMgr != null ? cfgMgr.ResolveBuffDescription(id) : string.Empty;
                if (sb.Length > 0)
                    sb.AppendLine().AppendLine();
                sb.Append(name);
                sb.AppendLine();
                sb.Append(string.IsNullOrWhiteSpace(desc) ? "（配置表中暂无效果说明）" : desc);
            }

            return sb.Length > 0 ? sb.ToString() : "当前无生效 Buff。";
        }
    }
}
