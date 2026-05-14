using System.Collections;
using System.Collections.Generic;
using KingCardsSpire.Core;
using KingCardsSpire.Models;
using KingCardsSpire.Views.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KingCardsSpire.Managers
{
    public sealed class UIManager : PersistentMonoSingleton<UIManager>
    {
        private readonly Stack<BaseView> _stack = new();
        private readonly Dictionary<UIPanelId, BaseView> _active = new();
        private Transform _uiRoot;

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

        public IEnumerator OpenAsync(UIPanelId panelId)
        {
            if (panelId == UIPanelId.None || _active.ContainsKey(panelId))
                yield break;

            var key = panelId.ToAddress();
            if (string.IsNullOrEmpty(key))
                yield break;

            GameObject go = null;
            yield return AssetManager.Instance.InstantiateAndWait(key, _uiRoot, g => go = g);
            if (go == null)
                yield break;

            var view = go.GetComponent<BaseView>();
            if (view == null)
                view = go.AddComponent<FallbackPanelView>();

            view.Initialize();
            // 先激活再 OnOpen：避免预制体根节点默认 inactive 时，子界面在 Refresh 里 Instantiate 手牌/布局不生效。
            view.Show();
            view.NotifyOpened();

            _active[panelId] = view;
            _stack.Push(view);
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
        }

    }
}
