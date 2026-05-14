using UnityEngine;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>
    /// 简单悬浮提示：在 Inspector 绑定根 <see cref="RectTransform"/> 与正文 <see cref="Text"/>。
    /// </summary>
    public sealed class SimpleTooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform rootRect;
        [SerializeField] private Text bodyText;
        [SerializeField] private Vector2 screenOffset = new(16f, -16f);

        private void Awake()
        {
            if (rootRect == null)
                rootRect = transform as RectTransform;

            var ownCanvas = GetComponent<Canvas>();
            if (ownCanvas != null)
            {
                ownCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                ownCanvas.worldCamera = null;
            }

            var rootGraphic = rootRect != null ? rootRect.GetComponent<Graphic>() : null;
            if (rootGraphic != null)
                rootGraphic.raycastTarget = false;

            if (bodyText != null)
                bodyText.raycastTarget = false;

            var cg = GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.blocksRaycasts = false;
                cg.interactable = false;
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// 在 <paramref name="parentRect"/> 的局部坐标系内，将提示锚定到当前鼠标屏幕位置。
        /// </summary>
        public void Show(string body, RectTransform parentRect, Canvas referenceCanvas)
        {
            if (bodyText != null)
                bodyText.text = body ?? string.Empty;

            Canvas.ForceUpdateCanvases();
            if (rootRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);

            if (parentRect != null && referenceCanvas != null && rootRect != null)
            {
                var cam = referenceCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : referenceCanvas.worldCamera;
                var screenPoint = (Vector2)Input.mousePosition + screenOffset;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    screenPoint,
                    cam,
                    out var localPoint);
                rootRect.anchoredPosition = localPoint;
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
