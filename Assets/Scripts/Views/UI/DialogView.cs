using System.Collections.Generic;
using KingCardsSpire.Configs;
using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>全屏对话面板：背景、立绘、说话者、正文、继续/跳过与选项（对应 DialogView 预制体）。</summary>
    public sealed class DialogView : BaseView
    {
        [Header("画面")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image portraitImage;

        [Header("文本")]
        [SerializeField] private GameObject speakerNameObj;
        [SerializeField] private Text speakerNameText;
        [SerializeField] private Text bodyText;

        [Header("操作")]
        [SerializeField] private Button continueButton;
        [SerializeField] private Button skipButton;

        [Header("选择支")]
        [SerializeField] private RectTransform choiceRoot;
        [SerializeField] private GameObject choiceOptionPrefab;

        private readonly List<GameObject> _spawnedChoices = new();
        private bool _continueRequested;
        private bool _skipRequested;
        private DialogueChoiceEntry _pendingChoice;

        private UnityAction _onContinue;
        private UnityAction _onSkip;

        public bool SkipRequested => _skipRequested;

        public DialogueChoiceEntry ConsumedChoice => _pendingChoice;

        public override void Initialize()
        {
            SetPanelId(UIPanelId.Dialog);
            _onContinue = OnContinueClicked;
            _onSkip = OnSkipClicked;
            continueButton.onClick.AddListener(_onContinue);
            skipButton.onClick.AddListener(_onSkip);
        }

        protected override void OnOpen()
        {
            ResetSignals();
            HideChoiceRoot();
        }

        public override void Dispose()
        {
            continueButton.onClick.RemoveListener(_onContinue);
            skipButton.onClick.RemoveListener(_onSkip);
            ClearChoices();
            base.Dispose();
        }

        private void OnDestroy()
        {
            if (continueButton != null && _onContinue != null)
                continueButton.onClick.RemoveListener(_onContinue);
            if (skipButton != null && _onSkip != null)
                skipButton.onClick.RemoveListener(_onSkip);
            ClearChoices();
        }

        /// <summary>绑定当前行 UI（不加载资源）；由 <see cref="Controllers.DialogueController"/> 负责贴图。</summary>
        public void ApplyTexts(DialogueLineEntry line)
        {
            speakerNameText.text = line != null ? line.SpeakerName : string.Empty;
            speakerNameObj.SetActive(line.SpeakerName != string.Empty);
            bodyText.text = line != null ? line.BodyText : string.Empty;
        }

        public void SetBackgroundSprite(Sprite sprite)
        {
            backgroundImage.sprite = sprite;
            backgroundImage.enabled = sprite != null;
        }

        public void SetPortraitSprite(Sprite sprite)
        {
            portraitImage.sprite = sprite;
            portraitImage.enabled = sprite != null;
        }

        public void ConfigureMode(bool isChoice, IReadOnlyList<DialogueChoiceEntry> choices)
        {
            ResetSignals();
            continueButton.gameObject.SetActive(!isChoice);
            skipButton.gameObject.SetActive(!isChoice);
            ClearChoices();

            if (!isChoice)
            {
                choiceRoot.gameObject.SetActive(false);
                return;
            }

            choiceRoot.gameObject.SetActive(true);
            if (choices == null || choices.Count == 0)
                return;

            if (choiceOptionPrefab != null)
            {
                for (var i = 0; i < choices.Count; i++)
                {
                    var ch = choices[i];
                    if (ch == null)
                        continue;

                    var go = Instantiate(choiceOptionPrefab, choiceRoot, false);
                    var row = go.GetComponent<DialogueChoiceOptionView>();
                    if (row != null)
                    {
                        var captured = ch;
                        row.Apply(captured, () => OnChoiceClicked(captured));
                    }

                    _spawnedChoices.Add(go);
                }

                return;
            }

            for (var i = 0; i < choices.Count; i++)
            {
                var ch = choices[i];
                if (ch == null)
                    continue;

                var go = CreateRuntimeChoiceRow(ch);
                _spawnedChoices.Add(go);
            }
        }

        private GameObject CreateRuntimeChoiceRow(DialogueChoiceEntry entry)
        {
            var go = new GameObject("ChoiceRow", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(choiceRoot, false);
            rt.sizeDelta = new Vector2(600f, 56f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.2f, 0.92f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(12f, 4f);
            trt.offsetMax = new Vector2(-12f, -4f);

            var t = textGo.AddComponent<Text>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.font = font;
            t.fontSize = 22;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            t.text = entry.OptionText;

            var captured = entry;
            btn.onClick.AddListener(() => OnChoiceClicked(captured));
            return go;
        }

        public void HideChoiceRoot()
        {
            choiceRoot.gameObject.SetActive(false);
        }

        public void ShowChoiceRoot()
        {
            choiceRoot.gameObject.SetActive(true);
        }

        public void ResetSignals()
        {
            _continueRequested = false;
            _skipRequested = false;
            _pendingChoice = null;
        }

        public bool TryConsumeContinue(out bool skipAll)
        {
            skipAll = _skipRequested;
            if (!_continueRequested && !_skipRequested)
                return false;
            var ok = _continueRequested || _skipRequested;
            _continueRequested = false;
            if (!skipAll)
                _skipRequested = false;
            return ok;
        }

        public bool TryConsumeChoice(out DialogueChoiceEntry choice)
        {
            choice = _pendingChoice;
            if (_pendingChoice == null)
                return false;
            _pendingChoice = null;
            return true;
        }

        private void OnContinueClicked()
        {
            _continueRequested = true;
        }

        private void OnSkipClicked()
        {
            _skipRequested = true;
        }

        private void OnChoiceClicked(DialogueChoiceEntry entry)
        {
            _pendingChoice = entry;
        }

        private void ClearChoices()
        {
            for (var i = 0; i < _spawnedChoices.Count; i++)
            {
                var go = _spawnedChoices[i];
                if (go != null)
                    Destroy(go);
            }

            _spawnedChoices.Clear();
        }
    }
}
