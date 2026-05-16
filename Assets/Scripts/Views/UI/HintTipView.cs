using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>
    /// 挂到 <c>HintTip.prefab</c> 根节点；若未绑定 <see cref="messageText"/> 则尝试使用子物体上唯一的 <see cref="Text"/>。
    /// </summary>
    public sealed class HintTipView : MonoBehaviour
    {
        [SerializeField] private Text messageText;

        private void Awake()
        {
            if (messageText == null)
                messageText = GetComponentInChildren<Text>(true);

            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;

            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        /// <summary>显示文案并在 <paramref name="autoCloseSeconds"/> 后销毁所在实例。</summary>
        public void Show(string message, float autoCloseSeconds)
        {
            if (messageText != null)
                messageText.text = message ?? string.Empty;

            gameObject.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(CoAutoClose(Mathf.Max(0.05f, autoCloseSeconds)));
        }

        private IEnumerator CoAutoClose(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            Destroy(gameObject);
        }
    }
}
