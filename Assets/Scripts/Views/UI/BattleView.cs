using System.Collections;
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
    /// </remarks>
    public sealed class BattleView : BaseView
    {
        private const float EnemyHandCardScale = 0.15f;
        private const float PlayerHandCardScale = 0.25f;
        private const float PlayAreaCardScale = 0.3f;

        [Header("顶部状态")]
        [SerializeField] private Text opponentNameText;
        [SerializeField] private Text turnsText;
        [SerializeField] private Text weatherText;
        [SerializeField] private Text xRayHintText;

        [Header("手牌")]
        [SerializeField] private CardView cardPrefab;
        [SerializeField] private RectTransform enemyHandRoot;
        [SerializeField] private RectTransform playerHandRoot;

        [Header("出牌区")]
        [SerializeField] private RectTransform enemyPlayArea;
        [SerializeField] private RectTransform myPlayArea;

        [Header("按钮")]
        [SerializeField] private Button enemyDiscardButton;
        [SerializeField] private Button playerDiscardButton;
        [SerializeField] private Button settingsButton;

        [Header("动画锚点")]
        [Tooltip("一般为己方弃牌堆按钮所在 RectTransform（Inspector 可与 playerDiscardButton 同一物体）。")]
        [SerializeField] private RectTransform playerDiscardPileAnchor;

        [Tooltip("一般为敌方弃牌堆按钮所在 RectTransform（可与 enemyDiscardButton 同一物体）。")]
        [SerializeField] private RectTransform enemyDiscardPileAnchor;

        private BattleController _battle;
        private EventManager _events;
        private ConfigManager _config;

        private UnityAction _onSettings;
        private UnityAction _onEnemyDiscard;
        private UnityAction _onPlayerDiscard;

        /// <summary>翻面与飞行动画进行中；期间忽略手牌点击并延后完整 Refresh。</summary>
        private bool _roundVisualBusy;

        /// <summary>本回合敌方出牌区实例（Prepare 后创建）。</summary>
        private CardView _enemyPlayCard;

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

        private void OnBattleStateChanged(BattleStateChangedEvent _)
        {
            if (_roundVisualBusy)
            {
                RefreshBattleChromeOnly();
                return;
            }

            RefreshAll();
        }

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

        private void RefreshBattleChromeOnly()
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
        }

        private void RefreshAll()
        {
            RefreshBattleChromeOnly();

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

            if (!_roundVisualBusy && _battle != null && _battle.IsBattleActive)
                SetupPlayAreaRound();
        }

        private void SetupPlayAreaRound()
        {
            if (_battle == null || !_battle.IsBattleActive)
            {
                ClearPlayAreaRoots();
                _enemyPlayCard = null;
                return;
            }

            if (!_battle.PrepareEnemyPlay(out _))
            {
                ClearPlayAreaRoots();
                _enemyPlayCard = null;
                return;
            }

            if (_enemyPlayCard != null && enemyPlayArea != null && enemyPlayArea.childCount > 0)
                return;

            ClearPlayAreaRoots();
            _enemyPlayCard = null;

            var idx = _battle.PendingEnemyHandIndex;
            var state = BattleManager.Instance.CurrentBattle;
            if (state?.EnemyHand == null || idx < 0 || idx >= state.EnemyHand.Length)
                return;

            var card = state.EnemyHand[idx];
            CardConfigEntry cfg = null;
            if (_config != null && card != null && !string.IsNullOrEmpty(card.Id))
                _config.TryGetCard(card.Id, out cfg);

            var vm = CardViewModel.FromCard(card, cfg);
            var cv = Instantiate(cardPrefab, enemyPlayArea, false);
            cv.SetScale(PlayAreaCardScale);
            cv.Apply(vm);
            if (cfg != null)
                cv.LoadCardArtFromConfig(cfg);
            cv.SetFaceDown(true);
            cv.OverrideClick(null);
            _enemyPlayCard = cv;
        }

        private void ClearPlayAreaRoots()
        {
            ClearChildTransforms(enemyPlayArea);
            ClearChildTransforms(myPlayArea);
        }

        private static void ClearChildTransforms(RectTransform root)
        {
            if (root == null)
                return;

            for (var i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
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
                cv.OverrideClick(() => OnPlayerHandCardClicked(battleRef, capturedIndex));
            }

            TrimHandTail(playerHandRoot, n);
        }

        private void OnPlayerHandCardClicked(BattleController battleRef, int capturedIndex)
        {
            if (battleRef == null || !battleRef.IsBattleActive || _roundVisualBusy)
                return;

            if (!battleRef.TryStagePlayerCard(capturedIndex, out var err))
            {
                Debug.LogWarning($"[BattleView] {err}");
                return;
            }

            StartCoroutine(RoundVisualSequenceRoutine());
        }

        private IEnumerator RoundVisualSequenceRoutine()
        {
            _roundVisualBusy = true;

            var playerIdx = _battle.PendingPlayerHandIndex;
            var state = BattleManager.Instance.CurrentBattle;
            var hand = state?.PlayerHand;
            if (hand == null || playerIdx < 0 || playerIdx >= hand.Length || _enemyPlayCard == null)
            {
                _roundVisualBusy = false;
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

            yield return null;

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

            yield return FlyOutcomeRoutine(playerPlay, _enemyPlayCard, cmp);

            Destroy(playerPlay.gameObject);
            if (_enemyPlayCard != null)
                Destroy(_enemyPlayCard.gameObject);
            _enemyPlayCard = null;

            _roundVisualBusy = false;
            RefreshAll();
        }

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
