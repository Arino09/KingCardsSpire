using System.Collections;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>BOSS 战败等场景：全屏提示，约 2 秒后自动关闭或点击后立即关闭，随后由 GameManager 衔接回主菜单。</summary>
    public sealed class GameOverView : BaseView
    {
        [SerializeField] private float dismissSeconds = 2f;

        [SerializeField] private Button dismissButton;

        private UnityAction _onDismiss;
        private Coroutine _autoDismissRoutine;
        private bool _finishRequested;

        public override void Initialize()
        {
            SetPanelId(UIPanelId.GameOver);
            _onDismiss = OnDismissClicked;
            dismissButton.onClick.AddListener(_onDismiss);
        }

        protected override void OnOpen()
        {
            _finishRequested = false;
            if (_autoDismissRoutine != null)
            {
                StopCoroutine(_autoDismissRoutine);
                _autoDismissRoutine = null;
            }

            _autoDismissRoutine = StartCoroutine(AutoDismissRoutine());
        }

        protected override void OnClose()
        {
            if (_autoDismissRoutine != null)
            {
                StopCoroutine(_autoDismissRoutine);
                _autoDismissRoutine = null;
            }
        }

        public override void Dispose()
        {
            if (dismissButton != null && _onDismiss != null)
                dismissButton.onClick.RemoveListener(_onDismiss);
            base.Dispose();
        }

        private void OnDestroy()
        {
            if (dismissButton != null && _onDismiss != null)
                dismissButton.onClick.RemoveListener(_onDismiss);
        }

        private IEnumerator AutoDismissRoutine()
        {
            var end = Time.unscaledTime + dismissSeconds;
            while (Time.unscaledTime < end && !_finishRequested)
                yield return null;

            RequestClose();
        }

        private void OnDismissClicked() => RequestClose();

        private void RequestClose()
        {
            if (_finishRequested)
                return;
            _finishRequested = true;
            if (_autoDismissRoutine != null)
            {
                StopCoroutine(_autoDismissRoutine);
                _autoDismissRoutine = null;
            }

            UIManager.Instance?.Close(UIPanelId.GameOver);
        }
    }
}
