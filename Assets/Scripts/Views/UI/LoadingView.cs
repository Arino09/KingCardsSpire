using System.Collections;
using UnityEngine;

namespace KingCardsSpire.Views.UI
{
    /// <summary>
    /// 全屏加载遮罩：由根节点 <see cref="CanvasGroup"/> 控制淡入淡出与射线阻挡。
    /// </summary>
    public sealed class LoadingView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        public float DisplayAlpha => canvasGroup != null ? canvasGroup.alpha : 0f;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        public void ApplyHiddenVisual()
        {
            if (canvasGroup == null)
                return;
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        public IEnumerator FadeTo(float targetAlpha, float durationSeconds)
        {
            if (canvasGroup == null)
                yield break;

            var clamped = Mathf.Clamp01(targetAlpha);
            if (durationSeconds <= 0f)
            {
                canvasGroup.alpha = clamped;
                ApplyRaycastsForAlpha(clamped);
                yield break;
            }

            var from = canvasGroup.alpha;
            var elapsed = 0f;
            while (elapsed < durationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / durationSeconds);
                var a = Mathf.Lerp(from, clamped, t);
                canvasGroup.alpha = a;
                ApplyRaycastsForAlpha(a);
                yield return null;
            }

            canvasGroup.alpha = clamped;
            ApplyRaycastsForAlpha(clamped);
        }

        private void ApplyRaycastsForAlpha(float alpha)
        {
            if (canvasGroup == null)
                return;
            canvasGroup.blocksRaycasts = alpha > 0.001f;
            canvasGroup.interactable = alpha > 0.001f;
        }
    }
}
