using KingCardsSpire.Core.MVC;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Views.UI
{
    /// <summary>
    /// UI 面板基类，具体逻辑后续实现。
    /// </summary>
    public abstract class BaseView : MonoBehaviour, IView
    {
        [SerializeField] private UIPanelId panelId = UIPanelId.None;

        public UIPanelId PanelId => panelId;

        protected void SetPanelId(UIPanelId id) => panelId = id;

        public virtual void Initialize() { }

        public virtual void Dispose() { }

        public virtual void Show() => gameObject.SetActive(true);

        public virtual void Hide() => gameObject.SetActive(false);

        protected virtual void OnOpen() { }

        protected virtual void OnClose() { }

        public void NotifyOpened()
        {
            OnOpen();
        }

        public void NotifyClosed()
        {
            OnClose();
        }
    }
}
