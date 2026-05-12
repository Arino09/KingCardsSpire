using KingCardsSpire.Configs;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>对话选项行：由 <see cref="DialogView"/> 在运行时实例化模板后绑定。</summary>
    public sealed class DialogueChoiceOptionView : MonoBehaviour
    {
        [SerializeField] private Text labelText;
        [SerializeField] private Button button;

        public void Apply(DialogueChoiceEntry entry, UnityAction onSelected)
        {
            if (labelText != null)
                labelText.text = entry != null ? entry.OptionText : string.Empty;

            if (button == null)
                return;

            button.onClick.RemoveAllListeners();
            if (onSelected != null)
                button.onClick.AddListener(onSelected);
        }
    }
}
