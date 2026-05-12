using System;
using System.Collections;
using System.Text;
using KingCardsSpire.Controllers;
using KingCardsSpire.Core;
using KingCardsSpire.Core.Events;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.AddressableAssets;
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
        [SerializeField] private Text dayText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text curDayText;
        [SerializeField] private Text weatherText;
        [SerializeField] private Text coinText;
        [SerializeField] private Text buffText;

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
        private AsyncOperationHandle<Sprite> _floorBgLoadHandle;
        private bool _hasFloorBgLoadHandle;

        public override void Initialize()
        {
            SetPanelId(UIPanelId.MainHub);
            _game = GameManager.Instance;
            _persistence = PersistenceManager.Instance;
            _events = EventManager.Instance;

            WireButtons();
            SubscribeEvents();
            RefreshAll();
            _wired = true;
        }

        public override void Dispose()
        {
            CancelFloorBackgroundLoad();
            UnsubscribeEvents();
            UnwireButtons();
            base.Dispose();
        }

        private void OnDestroy()
        {
            CancelFloorBackgroundLoad();
            if (_wired)
            {
                UnsubscribeEvents();
                UnwireButtons();
            }
        }

        protected override void OnOpen()
        {
            RefreshAll();
        }

        private void WireButtons()
        {
            UnwireButtons();

            _onDeck = OnDeckClicked;
            _onSettings = OnSettingsClicked;
            _onBoss = () => StartCoroutine(OpenBossBattleRoutine());
            _onVisitHero = () => StartCoroutine(OpenPanelRoutine(UIPanelId.HeroRoom));
            _onVisitNpc = () => StartCoroutine(OpenPanelRoutine(UIPanelId.NpcHub));
            _onVisitedNpc = () => StartCoroutine(OpenPanelRoutine(UIPanelId.UnlockedDialogues));
            _onShop = () => StartCoroutine(OpenPanelRoutine(UIPanelId.Shop));
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
            _events.Unsubscribe<GameOverEvent>(OnGameOverForHub);
            _events.Unsubscribe<GameVictoryEvent>(OnGameVictoryForHub);
        }

        private void OnDayChanged(DayChangedEvent _)
        {
            RefreshStatusTexts();
            RefreshVisitNpcButtonAccess();
        }

        private void OnGameOverForHub(GameOverEvent _) => RefreshNextDayButtonAccess();

        private void OnGameVictoryForHub(GameVictoryEvent _) => RefreshNextDayButtonAccess();

        private void OnFloorChanged(FloorChangedEvent _)
        {
            RefreshStatusTexts();
            RefreshFloorBackground();
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
            nextDayButton.interactable = gm != null && !gm.IsGameOver && !gm.IsRunVictory;
        }

        /// <summary>
        /// 按当前楼层从 Addressables 加载 <c>Assets/GameAssets/Sprites/UI/FloorBg/</c> 下对应资源（扩展名按资源实际类型区分）。
        /// </summary>
        private void RefreshFloorBackground()
        {
            if (_game == null)
                return;

            CancelFloorBackgroundLoad();
            _floorBgRoutine = StartCoroutine(LoadFloorBackgroundRoutine());
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
            var player = _game != null ? _game.PlayerState : null;
            if (player == null)
                return;

            if (dayText != null)
                dayText.text = $"第{player.CurrentDay}天";

            if (levelText != null)
                levelText.text = $"层级{player.CurrentFloor}";

            if (curDayText != null)
            {
                var remain = _game != null ? _game.GetEstimatedRemainingDaysOnFloor() : 0;
                curDayText.text = string.Format("当前层级剩余{0}天", remain);
            }

            if (weatherText != null)
                weatherText.text = WeatherDisplay.Format(player.CurrentWeather);

            if (coinText != null)
                coinText.text = $"${player.Gold}";

            if (buffText != null)
                buffText.text = $"初始Buff：{FormatBuff(player.SelectedBuff)}";
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

        private static string FormatBuff(BuffId buff)
        {
            return buff switch
            {
                BuffId.None => "无",
                BuffId.Socialite => "社交达人",
                BuffId.RichSecondGen => "富二代",
                BuffId.UnlimitedSupply => "无限供应",
                BuffId.RandomCommoner => "随机平民",
                BuffId.RandomKing => "随机国王",
                BuffId.SurprisePack => "惊喜卡包",
                BuffId.HighSalaryJob => "高薪工作",
                BuffId.ThiefInstinct => "盗贼本能",
                BuffId.XRayBoost => "透视增强",
                BuffId.ChaoticBattlefield => "混乱战场",
                _ => buff.ToString()
            };
        }

        private void OnDeckClicked()
        {
            StartCoroutine(OpenDeckCardListRoutine());
        }

        /// <summary>
        /// 打开通用卡牌列表弹窗，展示当前存档中的「持有卡组」(<see cref="PlayerData.OwnedCards"/>)。
        /// </summary>
        private IEnumerator OpenDeckCardListRoutine()
        {
            yield return UIManager.Instance.OpenAsync(UIPanelId.CardList);
            if (!UIManager.Instance.TryGetView(UIPanelId.CardList, out CardListView view))
                yield break;

            var player = _game != null ? _game.PlayerState : null;
            var cards = player?.OwnedCards;
            view.Apply(new CardListViewModel("我的卡组", cards ?? Array.Empty<Card>()));
        }

        private void OnSettingsClicked()
        {
            Debug.Log("[MainHub] 设置面板尚未接入。");
        }

        /// <summary>进入下一天：调用 <see cref="GameManager.AdvanceDay"/>，不打开其他界面。</summary>
        private void OnNextDayClicked()
        {
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
    }
}
