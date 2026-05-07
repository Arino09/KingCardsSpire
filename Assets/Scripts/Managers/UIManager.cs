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
        readonly Stack<BaseView> _stack = new();
        readonly Dictionary<UIPanelId, BaseView> _active = new();
        Transform _uiRoot;

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

        public void InitializeUi()
        {
            var canvasGo = new GameObject("UIRoot");
            canvasGo.transform.SetParent(transform, false);
            _uiRoot = canvasGo.transform;
            EnsureEventSystem(transform);
        }

        /// <summary>
        /// uGUI 点击依赖全局 EventSystem；场景未放置时使用此处生成的实例。
        /// </summary>
        static void EnsureEventSystem(Transform uiSystemsParent)
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem");
            go.transform.SetParent(uiSystemsParent, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

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
            view.NotifyOpened();
            view.Show();

            _active[panelId] = view;
            _stack.Push(view);
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
