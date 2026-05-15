using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>对话历史正文：对应预制体 RecordView。</summary>
    public sealed class RecordView : BaseView
    {
        [SerializeField] private Text bodyText;
        [SerializeField] private Button closeButton;

        private UnityAction _onCloseClicked;

        public override void Initialize()
        {
            SetPanelId(UIPanelId.DialogueRecord);
            _onCloseClicked = OnCloseClicked;
            closeButton.onClick.AddListener(_onCloseClicked);
        }

        public void Apply(string body)
        {
            bodyText.text = body ?? string.Empty;
        }

        private void OnCloseClicked()
        {
            UiButtonSfx.PlayDefaultClick();
            UIManager.Instance.Close(UIPanelId.DialogueRecord);
        }

        public override void Dispose()
        {
            closeButton.onClick.RemoveListener(_onCloseClicked);
            base.Dispose();
        }

        private void OnDestroy()
        {
            if (closeButton != null && _onCloseClicked != null)
                closeButton.onClick.RemoveListener(_onCloseClicked);
        }
    }
}
