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

        /// <summary>本回合敌方出牌区实例（Prepare 后创建）。</summary>
        private CardView _enemyPlayCard;

        /// <summary>
        /// 面板初始化：解析服务、注册按钮；战斗状态订阅在 <see cref="OnOpen"/> 中进行。
        /// </summary>
        public override void Initialize()
        {
            SetPanelId(UIPanelId.Battle);
            _battle = ServiceLocator.Get<BattleController>();
            _events = EventManager.Instance;
            _config = ConfigManager.Instance;

            WireButtons();
        }

        /// <summary>
        /// 释放时取消事件订阅与按钮监听，避免泄漏与重复回调。
        /// </summary>
        public override void Dispose()
        {
            UnwireButtons();
            if (_events != null)
                _events.Unsubscribe<BattleStateChangedEvent>(OnBattleStateChanged);

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
            if (_battle != null && !_battle.IsBattleActive)
                _battle.RequestStartBattle();

            RefreshAll();
        }

        /// <summary>
        /// 根据是否选中手牌、战斗是否进行中、动画是否占用，更新确认出牌按钮可点状态。
        /// </summary>
        private void UpdateConfirmPlayButtonInteractable()
        {
            if (confirmPlayButton == null)
                return;

            var canConfirm = _battle != null && _battle.IsBattleActive && !_roundVisualBusy &&
                             _selectedPlayerHandIndex >= 0;
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

        /// <summary>
        /// 全量刷新：先更新外壳，再在暂时关闭 LayoutGroup 的前提下重建双方手牌，最后布置本回合出牌区。
        /// </summary>
        /// <remarks>
        /// 重建手牌时禁用 LayoutGroup，避免每帧布局与 Instantiate/Destroy 交错导致抖动或错误尺寸。
        /// </remarks>
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

            UpdateConfirmPlayButtonInteractable();
        }

        /// <summary>
        /// 本回合需在敌方出牌区展示一张背面朝上的待翻开牌：由控制器 <c>PrepareEnemyPlay</c> 驱动，
        /// 从敌方手牌按下标实例化；若无有效回合或准备失败则清空出牌区。
        /// </summary>
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
        /// 按模型长度重建敌方手牌槽位：复用或新增 <see cref="CardView"/>，一律背面朝上并清空展示数据。
        /// </summary>
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

                cv.SetVisualState(i == _selectedPlayerHandIndex ? CardVisualState.Selected : CardVisualState.Normal);
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

            _selectedPlayerHandIndex = _selectedPlayerHandIndex == capturedIndex ? -1 : capturedIndex;
            SyncPlayerHandSelectionVisuals();
            UpdateConfirmPlayButtonInteractable();
        }

        /// <summary>按 <see cref="_selectedPlayerHandIndex"/> 刷新己方手牌的高亮，不重建节点。</summary>
        private void SyncPlayerHandSelectionVisuals()
        {
            var n = playerHandRoot != null ? playerHandRoot.childCount : 0;
            for (var i = 0; i < n; i++)
            {
                var cv = playerHandRoot.GetChild(i).GetComponent<CardView>();
                if (cv == null)
                    continue;

                cv.SetVisualState(i == _selectedPlayerHandIndex ? CardVisualState.Selected : CardVisualState.Normal);
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

            // 等待一帧，确保出牌区布局与渲染就绪后再同时翻面。
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

            _roundVisualBusy = false;
            RefreshAll();
            TryExitCasualBattleToMainHub();
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
