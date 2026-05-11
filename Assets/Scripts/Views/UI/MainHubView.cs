using System.Collections;
using System.Text;
using KingCardsSpire.Controllers;
using KingCardsSpire.Core;
using KingCardsSpire.Core.Events;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.Events;
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

        [Header("中部 · 按钮")]
        [SerializeField] private Button bossButton;
        [SerializeField] private Button visitHeroButton;
        [SerializeField] private Button visitNpcButton;
        [SerializeField] private Button visitedNpcButton;
        [SerializeField] private Button shopButton;

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

        private bool _wired;

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
            UnsubscribeEvents();
            UnwireButtons();
            base.Dispose();
        }

        private void OnDestroy()
        {
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
            _onVisitNpc = () => StartCoroutine(OpenPanelRoutine(UIPanelId.DailyChoice));
            _onVisitedNpc = () => StartCoroutine(OpenPanelRoutine(UIPanelId.UnlockedDialogues));
            _onShop = () => StartCoroutine(OpenPanelRoutine(UIPanelId.Shop));

            AddClick(deckButton, _onDeck);
            AddClick(settingsButton, _onSettings);
            AddClick(bossButton, _onBoss);
            AddClick(visitHeroButton, _onVisitHero);
            AddClick(visitNpcButton, _onVisitNpc);
            AddClick(visitedNpcButton, _onVisitedNpc);
            AddClick(shopButton, _onShop);
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

            _onDeck = null;
            _onSettings = null;
            _onBoss = null;
            _onVisitHero = null;
            _onVisitNpc = null;
            _onVisitedNpc = null;
            _onShop = null;
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
        }

        private void OnDayChanged(DayChangedEvent _) => RefreshStatusTexts();

        private void OnFloorChanged(FloorChangedEvent _) => RefreshStatusTexts();

        private void OnGoldChanged(GoldChangedEvent _) => RefreshStatusTexts();

        private void OnWeatherChanged(WeatherChangedEvent _) => RefreshStatusTexts();

        private void OnSaveWritten(SaveWrittenEvent _) => RefreshHistory();

        private void OnBossRewardOffered(BossRewardOfferedEvent _) => StartCoroutine(OpenBossRewardRoutine());

        private IEnumerator OpenBossRewardRoutine()
        {
            var ui = UIManager.Instance;
            if (ui == null)
                yield break;

            yield return ui.OpenAsync(UIPanelId.CardReward);
        }

        private void RefreshAll()
        {
            RefreshStatusTexts();
            RefreshHistory();
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
            var player = _game != null ? _game.PlayerState : null;
            if (player?.OwnedCards == null || player.OwnedCards.Length == 0)
            {
                Debug.Log("[MainHub] 卡组：当前尚无持有卡牌。");
                return;
            }

            var sb = new StringBuilder("卡组：");
            foreach (var c in player.OwnedCards)
            {
                if (c == null)
                    continue;
                sb.Append(c.Name).Append(' ');
            }

            Debug.Log(sb.ToString());
        }

        private void OnSettingsClicked()
        {
            Debug.Log("[MainHub] 设置面板尚未接入。");
        }

        private IEnumerator OpenBossBattleRoutine()
        {
            var ui = UIManager.Instance;
            var ctrl = ServiceLocator.Get<BattleController>();
            if (ui == null || ctrl == null)
                yield break;

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
