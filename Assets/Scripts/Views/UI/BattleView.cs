using System.Collections;
using System.Text;
using KingCardsSpire.Configs;
using KingCardsSpire.Controllers;
using KingCardsSpire.Core;
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
    public sealed class BattleView : BaseView
    {
        private const float EnemyHandCardScale = 0.15f;
        private const float PlayerHandCardScale = 0.25f;

        [Header("顶部状态")]
        [SerializeField] private Text opponentNameText;
        [SerializeField] private Text turnsText;
        [SerializeField] private Text weatherText;
        [SerializeField] private Text xRayHintText;

        [Header("手牌")]
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private RectTransform enemyHandRoot;
        [SerializeField] private RectTransform playerHandRoot;

        [Header("按钮")]
        [SerializeField] private Button enemyDiscardButton;
        [SerializeField] private Button playerDiscardButton;
        [SerializeField] private Button settingsButton;

        private BattleController _battle;
        private EventManager _events;
        private ConfigManager _config;

        private UnityAction _onSettings;
        private UnityAction _onEnemyDiscard;
        private UnityAction _onPlayerDiscard;

        public override void Initialize()
        {
            SetPanelId(UIPanelId.Battle);
            _battle = ServiceLocator.Get<BattleController>();
            _events = EventManager.Instance;
            _config = ConfigManager.Instance;

            WireButtons();
        }

        public override void Dispose()
        {
            UnwireButtons();
            if (_events != null)
                _events.Unsubscribe<BattleStateChangedEvent>(OnBattleStateChanged);

            base.Dispose();
        }

        protected override void OnOpen()
        {
            if (_events != null)
            {
                // 避免重复 Open 时叠加订阅；同一回合内 BattleManager 会先发 <see cref="BattleRoundResolvedEvent"/> 再发
                // <see cref="BattleStateChangedEvent"/>，手牌刷新只响应后者即可，防止同帧内 Refresh 两次。
                _events.Unsubscribe<BattleStateChangedEvent>(OnBattleStateChanged);
                _events.Subscribe<BattleStateChangedEvent>(OnBattleStateChanged);
            }

            _battle ??= ServiceLocator.Get<BattleController>();
            if (_battle != null && !_battle.IsBattleActive)
                _battle.RequestStartBattle();

            RefreshAll();
        }

        private void WireButtons()
        {
            UnwireButtons();

            _onSettings = OnSettingsClicked;
            _onEnemyDiscard = ShowEnemyDiscardClicked;
            _onPlayerDiscard = ShowPlayerDiscardClicked;

            AddClick(settingsButton, _onSettings);
            AddClick(enemyDiscardButton, _onEnemyDiscard);
            AddClick(playerDiscardButton, _onPlayerDiscard);
        }

        private void UnwireButtons()
        {
            RemoveClick(settingsButton, _onSettings);
            RemoveClick(enemyDiscardButton, _onEnemyDiscard);
            RemoveClick(playerDiscardButton, _onPlayerDiscard);

            _onSettings = null;
            _onEnemyDiscard = null;
            _onPlayerDiscard = null;
        }

        private static void AddClick(Button btn, UnityAction action)
        {
            if (action == null)
                return;
            btn.onClick.AddListener(action);
        }

        private static void RemoveClick(Button btn, UnityAction action)
        {
            if (action == null)
                return;
            btn.onClick.RemoveListener(action);
        }

        private void OnBattleStateChanged(BattleStateChangedEvent _) => RefreshAll();

        private void OnSettingsClicked()
        {
            Debug.Log("[BattleView] 设置：占位。");
        }

        private void ShowEnemyDiscardClicked()
        {
            StartCoroutine(OpenDiscardPanelAsync(true));
        }

        private void ShowPlayerDiscardClicked()
        {
            StartCoroutine(OpenDiscardPanelAsync(false));
        }

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
        private void RefreshAll()
        {
            var state = BattleManager.Instance.CurrentBattle;

            if (state != null)
            {
                var cap = state.NoRoundLimit ? "无限制" : state.MaxRound.ToString();
                turnsText.text = $"回合 {state.Round} / {cap}";
                weatherText.text = WeatherDisplay.Format(state.BattleWeather);
                opponentNameText.text = string.IsNullOrEmpty(state.OpponentDisplayName)
                    ? "对手"
                    : state.OpponentDisplayName;
            }

            if (state?.EnemyVisible != null && state.EnemyVisible.Length > 0)
            {
                var sb = new StringBuilder("透视: ");
                foreach (var c in state.EnemyVisible)
                {
                    if (c == null)
                        continue;
                    sb.Append(string.IsNullOrEmpty(c.Name) ? c.Id : c.Name).Append(' ');
                }

                xRayHintText.text = sb.ToString().TrimEnd();
            }
            else
            {
                xRayHintText.text = string.Empty;
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
        }

        private void RebuildEnemyHand(Card[] hand)
        {
            var n = hand?.Length ?? 0;

            for (var i = 0; i < n; i++)
            {
                CardView cv;
                if (i < enemyHandRoot.childCount)
                    cv = enemyHandRoot.GetChild(i).GetComponent<CardView>();
                else
                {
                    cv = Instantiate(cardPrefab, enemyHandRoot, false);
                    cv.SetScale(EnemyHandCardScale);
                }

                cv.Clear();
                cv.SetFaceDown(true);
            }

            TrimHandTail(enemyHandRoot, n);
        }

        private void RebuildPlayerHand(Card[] hand)
        {
            if (_battle == null)
                return;

            var n = hand?.Length ?? 0;
            var battleRef = _battle;

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
                cv.OverrideClick(() =>
                {
                    if (battleRef == null || !battleRef.IsBattleActive)
                        return;
                    if (!battleRef.TryPlayCard(capturedIndex, out var err))
                        Debug.LogWarning($"[BattleView] {err}");
                });
            }

            TrimHandTail(playerHandRoot, n);
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
