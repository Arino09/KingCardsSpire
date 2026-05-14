using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>
    /// Buff 3 选 1
    /// </summary>
    public sealed class BuffRewardView : BaseView
    {
        private const int OptionCount = 3;

        private readonly BuffId[] _optionBuffIds = new BuffId[OptionCount];
        [SerializeField] private Button[] optionButtons = new Button[OptionCount];
        [SerializeField] private Text[] optionTitleTexts = new Text[OptionCount];
        [SerializeField] private Text[] optionDescTexts = new Text[OptionCount];

        private bool _wired;

        public override void Initialize()
        {
            SetPanelId(UIPanelId.BuffRewardView);
        }

        public override void Dispose()
        {
            UnwireButtons();
            base.Dispose();
        }

        protected override void OnOpen()
        {
            if (!GameManager.Instance.TryGetBuffDraftOfferCopy(_optionBuffIds))
            {
                UIManager.Instance?.Close(UIPanelId.BuffRewardView);
                return;
            }

            for (var i = 0; i < OptionCount; i++)
            {
                var id = _optionBuffIds[i];
                ResolveDisplay(id, out var title, out var desc);
                optionTitleTexts[i].text = title;
                optionDescTexts[i].text = desc;
                optionButtons[i].gameObject.SetActive(id != BuffId.None);
            }

            WireButtons();
        }

        private void WireButtons()
        {
            if (_wired)
                return;
            _wired = true;
            for (var i = 0; i < OptionCount; i++)
            {
                var captured = i;
                var btn = optionButtons[i];
                btn.onClick.AddListener(() => OnOptionClicked(captured));
            }
        }

        private void UnwireButtons()
        {
            if (!_wired)
                return;
            _wired = false;
            for (var i = 0; i < OptionCount; i++)
            {
                var btn = optionButtons[i];
                btn.onClick.RemoveAllListeners();
            }
        }

        private void OnOptionClicked(int index)
        {
            if (index < 0 || index >= OptionCount)
                return;
            var pick = _optionBuffIds[index];
            if (pick == BuffId.None)
                return;

            var gm = GameManager.Instance;
            gm?.ApplyBuffChoice(pick);
            UIManager.Instance?.Close(UIPanelId.BuffRewardView);
        }

        private void ResolveDisplay(BuffId id, out string title, out string desc)
        {
            title = string.Empty;
            desc = string.Empty;
            if (id == BuffId.None)
                return;

            var cfgMgr = ConfigManager.Instance;
            if (cfgMgr != null)
            {
                title = cfgMgr.ResolveBuffDisplayName(id);
                desc = cfgMgr.ResolveBuffDescription(id);
                return;
            }

            title = id.ToString();
        }
    }
}
