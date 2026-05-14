using KingCardsSpire.Configs;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>
    /// Buff 3 选 1：由 <see cref="UIManager"/> 以运行时节点打开；选项数据来自 <see cref="GameManager"/> 当前草案。
    /// </summary>
    public sealed class BuffDraftView : BaseView
    {
        private const int OptionCount = 3;

        private readonly BuffId[] _optionBuffIds = new BuffId[OptionCount];
        private readonly Button[] _optionButtons = new Button[OptionCount];
        private readonly Text[] _optionTitleTexts = new Text[OptionCount];
        private readonly Text[] _optionDescTexts = new Text[OptionCount];

        private bool _wired;

        public override void Initialize()
        {
            SetPanelId(UIPanelId.BuffDraft);
            EnsureRuntimeHierarchy();
        }

        public override void Dispose()
        {
            UnwireButtons();
            base.Dispose();
        }

        protected override void OnOpen()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.TryGetBuffDraftOfferCopy(_optionBuffIds))
            {
                UIManager.Instance?.Close(UIPanelId.BuffDraft);
                return;
            }

            for (var i = 0; i < OptionCount; i++)
            {
                var id = _optionBuffIds[i];
                ResolveDisplay(id, out var title, out var desc);
                if (_optionTitleTexts[i] != null)
                    _optionTitleTexts[i].text = title;
                if (_optionDescTexts[i] != null)
                    _optionDescTexts[i].text = desc;
                if (_optionButtons[i] != null)
                    _optionButtons[i].gameObject.SetActive(id != BuffId.None);
            }

            WireButtons();
        }

        private void EnsureRuntimeHierarchy()
        {
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            if (root.childCount > 0)
                return;

            var bgGo = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(root, false);
            var bgRt = bgGo.GetComponent<RectTransform>();
            StretchFull(bgRt);
            bgGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(root, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(760f, 560f);
            panelRt.anchoredPosition = Vector2.zero;
            panelGo.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f, 1f);

            var titleGo = CreateUiText("Title", panelRt, 26, TextAnchor.UpperCenter, new Vector2(0f, -20f),
                new Vector2(700f, 44f));
            titleGo.text = "选择一项 Buff（3 选 1）";

            var y = -80f;
            for (var i = 0; i < OptionCount; i++)
            {
                var block = new GameObject($"Option{i}", typeof(RectTransform));
                block.transform.SetParent(panelRt, false);
                var blockRt = block.GetComponent<RectTransform>();
                blockRt.anchorMin = new Vector2(0.5f, 1f);
                blockRt.anchorMax = new Vector2(0.5f, 1f);
                blockRt.pivot = new Vector2(0.5f, 1f);
                blockRt.sizeDelta = new Vector2(700f, 130f);
                blockRt.anchoredPosition = new Vector2(0f, y);
                y -= 145f;

                var nameT = CreateUiText("Name", blockRt, 22, TextAnchor.UpperLeft, new Vector2(0f, 0f),
                    new Vector2(700f, 32f));
                var descT = CreateUiText("Desc", blockRt, 15, TextAnchor.UpperLeft, new Vector2(0f, -36f),
                    new Vector2(700f, 56f));
                descT.horizontalOverflow = HorizontalWrapMode.Wrap;
                descT.verticalOverflow = VerticalWrapMode.Truncate;

                var btnGo = new GameObject("PickButton", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(block.transform, false);
                var btnRt = btnGo.GetComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(1f, 0f);
                btnRt.anchorMax = new Vector2(1f, 0f);
                btnRt.pivot = new Vector2(1f, 0f);
                btnRt.sizeDelta = new Vector2(120f, 40f);
                btnRt.anchoredPosition = new Vector2(0f, 4f);
                var btn = btnGo.GetComponent<Button>();
                btn.targetGraphic = btnGo.GetComponent<Image>();
                btnGo.GetComponent<Image>().color = new Color(0.25f, 0.45f, 0.7f, 1f);
                var btnLabel = CreateUiText("Label", btnRt, 16, TextAnchor.MiddleCenter, Vector2.zero,
                    new Vector2(120f, 40f));
                btnLabel.text = "选择";

                _optionTitleTexts[i] = nameT;
                _optionDescTexts[i] = descT;
                _optionButtons[i] = btn;
            }
        }

        private static Text CreateUiText(string name, RectTransform parent, int fontSize, TextAnchor alignment,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPos;
            var t = go.GetComponent<Text>();
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.color = Color.white;
            t.supportRichText = false;
            return t;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private void WireButtons()
        {
            if (_wired)
                return;
            _wired = true;
            for (var i = 0; i < OptionCount; i++)
            {
                var captured = i;
                var btn = _optionButtons[i];
                if (btn == null)
                    continue;
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
                var btn = _optionButtons[i];
                if (btn != null)
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
            UIManager.Instance?.Close(UIPanelId.BuffDraft);
        }

        private static void ResolveDisplay(BuffId id, out string title, out string desc)
        {
            title = FormatBuffName(id);
            desc = string.Empty;
            var cfgMgr = ConfigManager.Instance;
            if (cfgMgr != null && cfgMgr.TryGetBuffByBuffId(id, out var cfg))
            {
                if (!string.IsNullOrEmpty(cfg.DisplayName))
                    title = cfg.DisplayName;
                desc = cfg.Description ?? string.Empty;
            }
        }

        private static string FormatBuffName(BuffId buff)
        {
            return buff switch
            {
                BuffId.None => "无",
                BuffId.Socialite => "社交达人",
                BuffId.RichSecondGen => "富二代",
                BuffId.UnlimitedSupply => "无限供应",
                BuffId.RandomCommoner => "随机平民",
                BuffId.RandomKing => "随机国王",
                BuffId.SurprisePack => "惊喜卡包",
                BuffId.HighSalaryJob => "高薪工作",
                BuffId.ThiefInstinct => "盗贼本能",
                BuffId.XRayBoost => "透视增强",
                BuffId.ChaoticBattlefield => "混乱战场",
                _ => buff.ToString()
            };
        }
    }
}
