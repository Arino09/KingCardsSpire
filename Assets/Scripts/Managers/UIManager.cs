using System.Collections;
using System.Collections.Generic;
using KingCardsSpire.Controllers;
using KingCardsSpire.Core;
using KingCardsSpire.Models;
using KingCardsSpire.Views.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KingCardsSpire.Managers
{
    public sealed class UIManager : PersistentMonoSingleton<UIManager>
    {
        private const string LoadingViewAddress = "UI/LoadingView";
        private const string HintTipAddress = "UI/HintTip";

        private readonly Stack<BaseView> _stack = new();
        private readonly Dictionary<UIPanelId, BaseView> _active = new();
        private Transform _uiRoot;

        [Header("Loading 遮罩（仅打开 MainHub / MainView 时）")]
        [SerializeField] private float _loadingFadeInDuration = 0.15f;
        [SerializeField] private float _loadingFadeOutDuration = 0.2f;
        [Tooltip("嵌套 Canvas 的 overrideSorting，保证 Loading 盖过同场景内其它 UI Canvas。")]
        [SerializeField] private int _loadingOverlayCanvasSortOrder = 32000;

        [Header("战败提示")]
        [SerializeField] private float battleDefeatHintDurationSeconds = 3f;

        private GameObject _loadingOverlayRoot;
        private LoadingView _loadingView;
        private int _loadingOverlayRefCount;

        protected override void Awake()
        {
            base.Awake();
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            ServiceLocator.Unregister<UIManager>();
            base.OnDestroy();
        }

        /// <summary>
        /// 绑定 UI 挂载点。若传入场景中预设的 <paramref name="sceneUiRoot"/>，则所有通过本管理器打开的界面均为其子物体（不改变其在 Canvas 下的层级，以免脱离 Canvas 导致无法渲染）。
        /// 否则在运行时创建名为 UIRoot 的空节点并挂在 UIManager 下。
        /// </summary>
        public void InitializeUi(Transform sceneUiRoot = null)
        {
            if (sceneUiRoot != null)
                _uiRoot = sceneUiRoot;
            else
            {
                var canvasGo = new GameObject("UIRoot");
                canvasGo.transform.SetParent(transform, false);
                _uiRoot = canvasGo.transform;
            }

            EnsureEventSystem(transform);
        }

        /// <summary>
        /// uGUI 点击依赖全局 EventSystem；场景未放置时使用此处生成的实例。
        /// </summary>
        private static void EnsureEventSystem(Transform uiSystemsParent)
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem");
            go.transform.SetParent(uiSystemsParent, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        public bool IsPanelOpen(UIPanelId panelId) =>
            panelId != UIPanelId.None && _active.ContainsKey(panelId);

        /// <summary>
        /// 战斗或驻守奖励等全屏层仍打开时，不在 MainHub 内单独推 Loading（由关闭面板前的衔接协程处理）。
        /// </summary>
        public bool IsBlockingPanelAboveMainHub() =>
            IsPanelOpen(UIPanelId.Battle) || IsPanelOpen(UIPanelId.CardReward);

        /// <summary>
        /// 是否已有 MainHub 用的 Loading 遮罩引用（例如 <see cref="OpenAsync"/> 打开 MainHub 过程中）。
        /// </summary>
        public bool IsMainHubLoadingMaskHeld => _loadingOverlayRefCount > 0;

        /// <summary>战败 HintTip 展示时长（秒）。</summary>
        public float BattleDefeatHintDurationSeconds => battleDefeatHintDurationSeconds;

        /// <summary>
        /// 通过 Addressables（<c>{HintTipAddress}</c>）实例化战败提示，展示约 <see cref="BattleDefeatHintDurationSeconds"/> 秒后销毁。
        /// </summary>
        public IEnumerator CoShowBattleDefeatHintRoutine(string message)
        {
            if (string.IsNullOrEmpty(message) || _uiRoot == null)
                yield break;

            var assets = AssetManager.Instance;
            if (assets == null)
            {
                Debug.LogWarning("[UIManager] AssetManager 未就绪，跳过战败 HintTip。");
                yield break;
            }

            GameObject go = null;
            yield return assets.InstantiateAndWait(HintTipAddress, _uiRoot, g => go = g);
            if (go == null)
            {
                Debug.LogWarning($"[UIManager] HintTip 实例化失败 address={HintTipAddress}");
                yield break;
            }

            go.transform.SetAsLastSibling();
            EnsureMainHubAtBack();
            if (_loadingOverlayRoot != null)
                BringLoadingOverlayToFront();
            var tip = go.GetComponent<HintTipView>();
            if (tip == null)
                tip = go.AddComponent<HintTipView>();
            tip.Show(message, battleDefeatHintDurationSeconds);
            yield return new WaitForSeconds(battleDefeatHintDurationSeconds);
        }

        /// <summary>
        /// 淡入并抬高 MainHub 专用 Loading 遮罩；与 <see cref="PopMainHubLoadingMaskRoutine"/> 成对使用，支持嵌套 refCount。
        /// </summary>
        public IEnumerator PushMainHubLoadingMaskRoutine()
        {
            _loadingOverlayRefCount++;
            var isFirstOverlayUser = _loadingOverlayRefCount == 1;

            if (isFirstOverlayUser)
            {
                yield return EnsureLoadingOverlayRoutine();
                if (_loadingOverlayRoot != null && _loadingView != null)
                {
                    _loadingOverlayRoot.SetActive(true);
                    BringLoadingOverlayToFront();
                    yield return _loadingView.FadeTo(1f, _loadingFadeInDuration);
                }
            }
            else
            {
                yield return EnsureLoadingOverlayRoutine();
                if (_loadingOverlayRoot != null && _loadingView != null)
                {
                    _loadingOverlayRoot.SetActive(true);
                    BringLoadingOverlayToFront();
                    if (!Mathf.Approximately(_loadingView.DisplayAlpha, 1f))
                        yield return _loadingView.FadeTo(1f, _loadingFadeInDuration);
                }
            }
        }

        /// <summary>
        /// 与 <see cref="PushMainHubLoadingMaskRoutine"/> 成对：ref 归零时淡出并隐藏遮罩。
        /// </summary>
        public IEnumerator PopMainHubLoadingMaskRoutine()
        {
            _loadingOverlayRefCount--;
            if (_loadingOverlayRefCount < 0)
                _loadingOverlayRefCount = 0;

            if (_loadingOverlayRefCount > 0 || _loadingView == null || _loadingOverlayRoot == null)
                yield break;

            yield return _loadingView.FadeTo(0f, _loadingFadeOutDuration);
            _loadingOverlayRoot.SetActive(false);
        }

        /// <summary>
        /// 先遮罩，再关闭驻守奖励与战斗，等待 <see cref="MainHubView.WaitForInitialHubPresentationReady"/> 后淡出。
        /// </summary>
        /// <param name="requestEndBattleBeforeClosingBattleUi">若提供则在关闭战斗界面前调用 <see cref="Controllers.BattleController.RequestEndBattle"/>（最后一层 BOSS 胜利收尾需要）。</param>
        public IEnumerator CoCloseBossRewardBattleRevealMainHubRoutine(
            BattleController requestEndBattleBeforeClosingBattleUi = null)
        {
            yield return StartCoroutine(PushMainHubLoadingMaskRoutine());

            Close(UIPanelId.CardReward);
            if (requestEndBattleBeforeClosingBattleUi != null)
                requestEndBattleBeforeClosingBattleUi.RequestEndBattle();
            Close(UIPanelId.Battle);

            if (TryGetView(UIPanelId.MainHub, out MainHubView mainHub))
            {
                yield return mainHub.WaitForInitialHubPresentationReady();
                yield return null;
                yield return StartCoroutine(PopMainHubLoadingMaskRoutine());
                EnsureMainHubAtBack();
            }
            else
                yield return StartCoroutine(PopMainHubLoadingMaskRoutine());

            GameAudioDirector.Instance?.RefreshFromUiState();
        }

        public IEnumerator OpenAsync(UIPanelId panelId)
        {
            if (panelId == UIPanelId.None || _active.ContainsKey(panelId))
                yield break;

            var key = panelId.ToAddress();
            if (string.IsNullOrEmpty(key))
                yield break;

            // Loading 仅用于进入主塔 hub（MainView）：主菜单或其它流程打开 MainHub 时遮挡异步实例化间隙。
            var useMainHubLoading = panelId == UIPanelId.MainHub;
            if (useMainHubLoading)
                yield return StartCoroutine(PushMainHubLoadingMaskRoutine());

            GameObject go = null;
            yield return AssetManager.Instance.InstantiateAndWait(key, _uiRoot, g => go = g);

            if (useMainHubLoading && _loadingOverlayRoot != null)
                BringLoadingOverlayToFront();

            if (go == null)
            {
                if (useMainHubLoading)
                    yield return StartCoroutine(PopMainHubLoadingMaskRoutine());
                yield break;
            }

            if (!useMainHubLoading)
                ApplyPanelSiblingOrder(go.transform, panelId);

            var view = go.GetComponent<BaseView>();
            if (view == null)
                view = go.AddComponent<FallbackPanelView>();

            view.Initialize();
            // 先激活再 OnOpen：避免预制体根节点默认 inactive 时，子界面在 Refresh 里 Instantiate 手牌/布局不生效。
            view.Show();
            view.NotifyOpened();

            _active[panelId] = view;
            _stack.Push(view);
            if (useMainHubLoading && _loadingOverlayRoot != null)
                BringLoadingOverlayToFront();
            // 给布局与部分子控件一帧完成首帧刷新，减轻「预制体默认态 → 首帧后布局」的跳变感。
            yield return null;

            if (useMainHubLoading && view is MainHubView mainHub)
                yield return mainHub.WaitForInitialHubPresentationReady();

            // 背景 Sprite 赋值后再留一帧，减轻淡出开始时 Image 尚未完成绘制的闪断。
            if (useMainHubLoading)
                yield return null;

            if (useMainHubLoading)
            {
                yield return StartCoroutine(PopMainHubLoadingMaskRoutine());
                ApplyPanelSiblingOrder(go.transform, panelId);
            }

            GameAudioDirector.Instance?.RefreshFromUiState();
        }

        /// <summary>
        /// MainHub 固定在最底层；其它面板按打开顺序叠在上层（后开在上）。
        /// Loading 遮罩由 <see cref="BringLoadingOverlayToFront"/> 单独置顶。
        /// </summary>
        private void ApplyPanelSiblingOrder(Transform panelTransform, UIPanelId panelId)
        {
            if (panelTransform == null)
                return;

            if (panelId == UIPanelId.MainHub)
                panelTransform.SetAsFirstSibling();
            else
            {
                panelTransform.SetAsLastSibling();
                EnsureMainHubAtBack();
            }

            if (_loadingOverlayRoot != null)
                BringLoadingOverlayToFront();
        }

        private void EnsureMainHubAtBack()
        {
            if (!_active.TryGetValue(UIPanelId.MainHub, out var hub) || hub == null)
                return;

            hub.transform.SetAsFirstSibling();
        }

        private IEnumerator EnsureLoadingOverlayRoutine()
        {
            if (_loadingOverlayRoot != null)
                yield break;

            if (_uiRoot == null)
            {
                Debug.LogWarning("[UIManager] EnsureLoadingOverlay：_uiRoot 未初始化，跳过 Loading。");
                yield break;
            }

            GameObject created = null;
            yield return AssetManager.Instance.InstantiateAndWait(LoadingViewAddress, _uiRoot, g => created = g);
            if (created == null)
            {
                Debug.LogWarning($"[UIManager] LoadingView 实例化失败 address={LoadingViewAddress}");
                yield break;
            }

            _loadingOverlayRoot = created;
            _loadingView = _loadingOverlayRoot.GetComponent<LoadingView>();
            if (_loadingView == null)
                _loadingView = _loadingOverlayRoot.AddComponent<LoadingView>();

            _loadingView.ApplyHiddenVisual();
            BringLoadingOverlayToFront();
            _loadingOverlayRoot.SetActive(false);
        }

        /// <summary>
        /// 将 Loading 置于兄弟节点最末，并配置独立 Canvas 的 <see cref="Canvas.sortingOrder"/>，避免被其它 overrideSorting 的面板压住。
        /// </summary>
        private void BringLoadingOverlayToFront()
        {
            if (_loadingOverlayRoot == null)
                return;

            var canvas = _loadingOverlayRoot.GetComponent<Canvas>();
            if (canvas == null)
                canvas = _loadingOverlayRoot.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = _loadingOverlayCanvasSortOrder;

            if (_loadingOverlayRoot.GetComponent<GraphicRaycaster>() == null)
                _loadingOverlayRoot.AddComponent<GraphicRaycaster>();

            _loadingOverlayRoot.transform.SetAsLastSibling();
        }

        /// <summary>
        /// 获取当前已通过 <see cref="OpenAsync"/> 打开且仍存活的面板（用于在打开后注入数据，例如 <see cref="CardListView.Apply"/>）。
        /// </summary>
        public bool TryGetView<T>(UIPanelId panelId, out T view) where T : BaseView
        {
            view = null;
            if (!_active.TryGetValue(panelId, out var baseView) || baseView == null)
                return false;
            view = baseView as T;
            return view != null;
        }

        public void Close(UIPanelId panelId)
        {
            if (!_active.TryGetValue(panelId, out var view) || view == null)
                return;

            view.NotifyClosed();
            view.Dispose();
            Destroy(view.gameObject);
            _active.Remove(panelId);
            if (_stack.Count > 0 && _stack.Peek() == view)
                _stack.Pop();

            GameAudioDirector.Instance?.RefreshFromUiState();
        }

        public void CloseAll()
        {
            foreach (var kv in _active)
            {
                if (kv.Value == null)
                    continue;
                kv.Value.NotifyClosed();
                kv.Value.Dispose();
                Destroy(kv.Value.gameObject);
            }

            _active.Clear();
            _stack.Clear();
            GameAudioDirector.Instance?.RefreshFromUiState();
        }
    }
}
