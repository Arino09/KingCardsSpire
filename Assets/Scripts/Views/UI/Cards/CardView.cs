using System;
using System.Collections;
using System.Collections.Generic;
using KingCardsSpire.Configs;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI.Cards
{
    /// <summary>
    /// 卡牌预制体绑定：将 <see cref="CardViewModel"/> 同步到 UI；卡画可另通过 Addressables 异步加载。
    /// </summary>
    public sealed class CardView : MonoBehaviour
    {
        [SerializeField] private RectTransform rectRoot, rectImage, rectFront, rectBack;
        [SerializeField] private Text levelBadgeText;
        [SerializeField] private Text typeSubText;
        [SerializeField] private Text nameText;
        [SerializeField] private Text effectText;
        [SerializeField] private Image cardArtImage;
        [SerializeField] private Button clickTarget;
        [SerializeField] private UnityEvent onClicked;
        [SerializeField] private Image visualRoot;
        [SerializeField] private Image albumMaskImage;
        [SerializeField] private Color normalTint = Color.white;
        [SerializeField] private Color selectedTint = new(0.85f, 0.95f, 1f, 1f);
        [SerializeField] private Color disabledTint = new(0.55f, 0.55f, 0.55f, 1f);

        [Tooltip("战斗手牌悬停：放大/还原的缓动时长（秒）。")]
        [SerializeField] private float battleHandHoverTweenDurationSeconds = 0.16f;

        private AsyncOperationHandle<Sprite> _artLoadHandle;
        private bool _hasArtHandle;
        private CardVisualState _visualState = CardVisualState.Normal;

        /// <summary>最近一次 <see cref="Apply"/> 绑定的模板 <see cref="CardViewModel.CardId"/>；用于同 Id 时跳过重复加载卡画。</summary>
        private string _lastAppliedCardId = string.Empty;

        private bool _faceDown;

        /// <summary>最近一次 <see cref="SetScale"/> 的基准值；悬停放大结束后再设回该值。</summary>
        private float _handBaseScale = 1f;

        private bool _battleHandHoverEnabled;
        private float _battleHandHoverScaleMultiplier = 1.35f;
        private Vector2 _battleHandHoverAnchoredDelta;
        private Func<bool> _battleHandHoverCanHover;
        private bool _battleHandHoverActive;
        private int _battleHandHoverSavedSiblingIndex;
        private Vector2 _battleHandHoverSavedAnchoredPosition;
        private Vector3 _battleHandHoverSavedLocalEuler;
        private EventTrigger _handHoverEventTrigger;
        private Coroutine _handHoverRoutine;

        public int HandIndex { get; set; }

        private void Awake()
        {
            clickTarget.onClick.AddListener(OnClickTargetInvoked);
        }

        private void OnDestroy()
        {
            clickTarget.onClick.RemoveListener(OnClickTargetInvoked);

            TeardownBattleHandHoverForDestroy();
            ReleaseAddressableArtOnly();
        }

        public void SetScale(float scale)
        {
            var s = Vector3.one * scale;
            rectRoot.sizeDelta = new Vector2(842, 1180) * scale;
            rectBack.localScale = s;
            rectFront.localScale = s;
            if (!_battleHandHoverActive)
                _handBaseScale = scale;
        }

        /// <summary>
        /// 战斗手牌区：悬停时放大、置顶、旋转归零便于阅读；由 <see cref="BattleView"/> 在重建手牌时配置。手牌位置由战斗界面代码排布。
        /// </summary>
        public void ConfigureBattleHandHover(
            bool enabled,
            float scaleMultiplier,
            Vector2 anchoredPositionDelta,
            Func<bool> canHover)
        {
            if (!enabled)
            {
                ForceEndBattleHandHover();
                _battleHandHoverEnabled = false;
                _battleHandHoverCanHover = null;
                ClearBattleHandHoverEventTrigger();
                return;
            }

            ForceEndBattleHandHover();
            _battleHandHoverEnabled = true;
            _battleHandHoverScaleMultiplier = Mathf.Max(1.01f, scaleMultiplier);
            _battleHandHoverAnchoredDelta = anchoredPositionDelta;
            _battleHandHoverCanHover = canHover;
            WireBattleHandHoverEventTrigger();
        }

        /// <summary>回合动画等占用 <see cref="rectRoot"/> 前调用，立即结束悬停预览并恢复布局。</summary>
        public void ForceEndBattleHandHover()
        {
            SnapBattleHandHoverPreviewEnd();
        }

        private void TeardownBattleHandHoverForDestroy()
        {
            StopHandHoverRoutine();
            SnapBattleHandHoverPreviewEnd();
            ClearBattleHandHoverEventTrigger();
            _battleHandHoverEnabled = false;
            _battleHandHoverCanHover = null;
        }

        private void WireBattleHandHoverEventTrigger()
        {
            if (clickTarget == null)
                return;

            var go = clickTarget.gameObject;
            _handHoverEventTrigger = go.GetComponent<EventTrigger>();
            if (_handHoverEventTrigger == null)
                _handHoverEventTrigger = go.AddComponent<EventTrigger>();

            _handHoverEventTrigger.triggers ??= new List<EventTrigger.Entry>();
            _handHoverEventTrigger.triggers.Clear();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(OnBattleHandHoverPointerEnter);
            _handHoverEventTrigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(OnBattleHandHoverPointerExit);
            _handHoverEventTrigger.triggers.Add(exit);
        }

        private void ClearBattleHandHoverEventTrigger()
        {
            if (_handHoverEventTrigger != null)
                _handHoverEventTrigger.triggers.Clear();
        }

        private void OnBattleHandHoverPointerEnter(BaseEventData _)
        {
            if (!_battleHandHoverEnabled || _battleHandHoverCanHover == null || !_battleHandHoverCanHover())
                return;

            BeginBattleHandHoverPreview();
        }

        private void OnBattleHandHoverPointerExit(BaseEventData _)
        {
            StartBattleHandHoverHideTween();
        }

        private void StopHandHoverRoutine()
        {
            if (_handHoverRoutine == null)
                return;

            StopCoroutine(_handHoverRoutine);
            _handHoverRoutine = null;
        }

        private void BeginBattleHandHoverPreview()
        {
            if (_battleHandHoverActive || rectRoot == null)
                return;

            var parent = rectRoot.parent as RectTransform;
            if (parent == null)
                return;

            StopHandHoverRoutine();

            _battleHandHoverSavedSiblingIndex = rectRoot.GetSiblingIndex();
            _battleHandHoverSavedAnchoredPosition = rectRoot.anchoredPosition;
            _battleHandHoverSavedLocalEuler = rectRoot.localEulerAngles;

            rectRoot.SetAsLastSibling();
            _battleHandHoverActive = true;

            var startScale = _handBaseScale;
            var endScale = _handBaseScale * _battleHandHoverScaleMultiplier;
            var startPos = _battleHandHoverSavedAnchoredPosition;
            var endPos = _battleHandHoverSavedAnchoredPosition + _battleHandHoverAnchoredDelta;
            var startEu = _battleHandHoverSavedLocalEuler;
            var endEu = Vector3.zero;

            _handHoverRoutine = StartCoroutine(HandHoverTweenRoutine(
                startScale,
                endScale,
                startPos,
                endPos,
                startEu,
                endEu,
                () => { _handHoverRoutine = null; }));
        }

        private void StartBattleHandHoverHideTween()
        {
            if (!_battleHandHoverEnabled || rectRoot == null || !_battleHandHoverActive)
                return;

            StopHandHoverRoutine();

            var startScale = ReadCurrentHandVisualScale();
            var endScale = _handBaseScale;
            var startPos = rectRoot.anchoredPosition;
            var endPos = _battleHandHoverSavedAnchoredPosition;
            var startEu = rectRoot.localEulerAngles;
            var endEu = _battleHandHoverSavedLocalEuler;

            _handHoverRoutine = StartCoroutine(HandHoverTweenRoutine(
                startScale,
                endScale,
                startPos,
                endPos,
                startEu,
                endEu,
                () =>
                {
                    _handHoverRoutine = null;
                    SnapBattleHandHoverPreviewEnd();
                }));
        }

        private float ReadCurrentHandVisualScale()
        {
            if (rectRoot == null || rectRoot.sizeDelta.x <= 1f)
                return _handBaseScale;

            return rectRoot.sizeDelta.x / 842f;
        }

        private IEnumerator HandHoverTweenRoutine(
            float startScale,
            float endScale,
            Vector2 startPos,
            Vector2 endPos,
            Vector3 startEu,
            Vector3 endEu,
            Action onComplete)
        {
            var dur = Mathf.Max(0.01f, battleHandHoverTweenDurationSeconds);
            var elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / dur);
                var k = t * t * (3f - 2f * t);
                ApplyHandVisualScale(Mathf.Lerp(startScale, endScale, k));
                rectRoot.anchoredPosition = Vector2.Lerp(startPos, endPos, k);
                rectRoot.localEulerAngles = Vector3.Lerp(startEu, endEu, k);
                yield return null;
            }

            ApplyHandVisualScale(endScale);
            rectRoot.anchoredPosition = endPos;
            rectRoot.localEulerAngles = endEu;
            onComplete?.Invoke();
        }

        private void ApplyHandVisualScale(float scale)
        {
            var s = Vector3.one * scale;
            rectRoot.sizeDelta = new Vector2(842, 1180) * scale;
            rectBack.localScale = s;
            rectFront.localScale = s;
        }

        /// <summary>立即结束悬停（无缓动），用于强制刷新或销毁。</summary>
        private void SnapBattleHandHoverPreviewEnd()
        {
            StopHandHoverRoutine();

            if (!_battleHandHoverActive)
                return;

            _battleHandHoverActive = false;

            if (rectRoot == null)
                return;

            var parent = rectRoot.parent as RectTransform;
            var maxIdx = parent != null ? Mathf.Max(0, parent.childCount - 1) : 0;
            rectRoot.SetSiblingIndex(Mathf.Clamp(_battleHandHoverSavedSiblingIndex, 0, maxIdx));
            rectRoot.anchoredPosition = _battleHandHoverSavedAnchoredPosition;
            rectRoot.localEulerAngles = _battleHandHoverSavedLocalEuler;
            SetScale(_handBaseScale);
        }

        /// <summary>
        /// 战斗：替换默认点击（否则使用 Inspector 配置的 <see cref="onClicked"/>）。
        /// </summary>
        public void OverrideClick(UnityAction handler)
        {
            clickTarget.onClick.RemoveAllListeners();
            if (handler != null)
                clickTarget.onClick.AddListener(handler);
        }

        /// <summary>
        /// true 时仅展示卡背并禁用交互（用于敌方暗牌行）。
        /// </summary>
        public void SetFaceDown(bool faceDown)
        {
            _faceDown = faceDown;

            rectFront.gameObject.SetActive(!faceDown);
            rectBack.gameObject.SetActive(faceDown);

            levelBadgeText.gameObject.SetActive(!faceDown);
            typeSubText.gameObject.SetActive(!faceDown);
            nameText.gameObject.SetActive(!faceDown);
            effectText.gameObject.SetActive(!faceDown);

            // 卡背仅隐藏正面与禁用点击，不使用 Disabled 色调（否则出牌区翻面前的牌会一直发灰）。
            if (faceDown)
            {
                ConfigureBattleHandHover(false, 1f, Vector2.zero, null);
                _visualState = CardVisualState.Normal;
                ApplyVisualTint();
                clickTarget.interactable = false;
                return;
            }

            clickTarget.interactable = true;
            SetVisualState(CardVisualState.Normal);
        }

        /// <summary>不改变翻面状态；用于教程遮罩下禁用部分手牌点击。</summary>
        public void SetClickInteractionEnabled(bool enabled)
        {
            if (_faceDown)
                return;

            clickTarget.interactable = enabled;
        }

        public void Apply(CardViewModel vm)
        {
            if (vm == null)
            {
                Clear();
                return;
            }

            ResetAlbumMaskGraphic();

            var sameCardId = string.Equals(vm.CardId, _lastAppliedCardId, StringComparison.Ordinal);

            levelBadgeText.text = vm.LevelDisplay;
            typeSubText.text = vm.TypeDisplay;
            nameText.text = vm.Name;
            effectText.text = vm.Effect;

            if (vm.Art != null)
            {
                ReleaseAddressableArtOnly();
                cardArtImage.sprite = vm.Art;
                cardArtImage.enabled = true;
            }
            else
            {
                if (!sameCardId)
                {
                    ReleaseAddressableArtOnly();
                    ClearCardArtVisual();
                }
            }

            _lastAppliedCardId = vm.CardId ?? string.Empty;

            ApplyVisualTint();
        }

        public void Clear()
        {
            _faceDown = false;
            rectFront.gameObject.SetActive(true);
            rectBack.gameObject.SetActive(false);

            levelBadgeText.text = string.Empty;
            typeSubText.text = string.Empty;
            nameText.text = string.Empty;
            effectText.text = string.Empty;

            ReleaseAddressableArtOnly();
            ClearCardArtVisual();
            _lastAppliedCardId = string.Empty;
            HandIndex = 0;
            _visualState = CardVisualState.Normal;
            ApplyVisualTint();

            levelBadgeText.gameObject.SetActive(true);
            typeSubText.gameObject.SetActive(true);
            nameText.gameObject.SetActive(true);
            effectText.gameObject.SetActive(true);
            clickTarget.interactable = true;
            ResetAlbumMaskGraphic();

            ConfigureBattleHandHover(false, 1f, Vector2.zero, null);
        }

        /// <summary>图鉴：未解锁时在卡面 Mask 上使用 <see cref="disabledTint"/>；已解锁时隐藏遮罩。</summary>
        public void SetAlbumMaskRevealed(bool revealed)
        {
            if (albumMaskImage == null)
                return;

            albumMaskImage.color = revealed ? new Color(1f, 1f, 1f, 0f) : disabledTint;
        }

        private void ResetAlbumMaskGraphic()
        {
            if (albumMaskImage == null)
                return;

            albumMaskImage.color = new Color(1f, 1f, 1f, 0f);
        }

        /// <summary>
        /// 从 <see cref="CardConfigEntry.Icon"/> 异步加载卡画；加载期间可先 <see cref="Apply(CardViewModel)"/> 展示文字。
        /// </summary>
        public void LoadCardArtFromConfig(CardConfigEntry config)
        {
            if (!isActiveAndEnabled)
                return;

            if (config != null && !string.IsNullOrEmpty(config.Id)
                                && string.Equals(config.Id, _lastAppliedCardId, StringComparison.Ordinal)
                                && cardArtImage.sprite != null && cardArtImage.enabled)
                return;

            StopAllCoroutines();
            StartCoroutine(LoadCardArtFromConfigRoutine(config));
        }

        public void SetVisualState(CardVisualState state)
        {
            _visualState = state;
            clickTarget.interactable = state != CardVisualState.Disabled;

            ApplyVisualTint();
        }

        /// <summary>
        /// 暗牌→正面展示翻转动效：前半段绕 Y 0°→90° 仅卡背；在侧立（90°）处切换正面并将角度接为 -90°，
        /// 后半段 -90°→0°，避免整段 0°→180° 时后半程正面被镜像。
        /// 结束时复位旋转并 <see cref="SetFaceDown"/> false。翻面前应已 <see cref="Apply"/> 正面数据。
        /// </summary>
        public IEnumerator PlayRevealFlipRoutine(float durationSeconds = 0.35f)
        {
            var halfDur = Mathf.Max(0.02f, durationSeconds * 0.5f);

            // 前半：卡背旋转至侧立
            var elapsed = 0f;
            while (elapsed < halfDur)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / halfDur);
                var angle = Mathf.Lerp(0f, 90f, t);
                rectRoot.localEulerAngles = new Vector3(0f, angle, 0f);
                yield return null;
            }

            rectRoot.localEulerAngles = new Vector3(0f, 90f, 0f);

            // 侧立瞬间换正面，并接 -90° 使后半段与前半段在视觉上连续且正面不镜像
            SetFaceDown(false);
            rectRoot.localEulerAngles = new Vector3(0f, -90f, 0f);

            elapsed = 0f;
            while (elapsed < halfDur)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / halfDur);
                var angle = Mathf.Lerp(-90f, 0f, t);
                rectRoot.localEulerAngles = new Vector3(0f, angle, 0f);
                yield return null;
            }

            rectRoot.localEulerAngles = Vector3.zero;
            SetFaceDown(false);
        }

        /// <summary>将卡牌实例移动到目标 UI 矩形的世界坐标位置（线性插值）；销毁由调用方处理。</summary>
        public IEnumerator FlyToRectTransformRoutine(RectTransform target, float durationSeconds = 0.45f)
        {
            var rt = rectRoot;
            var startWorld = rt.position;
            var endWorld = target.position;
            var elapsed = 0f;
            var dur = Mathf.Max(0.05f, durationSeconds);
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / dur);
                rt.position = Vector3.Lerp(startWorld, endWorld, t);
                yield return null;
            }

            rt.position = endWorld;
        }

        private IEnumerator LoadCardArtFromConfigRoutine(CardConfigEntry config)
        {
            ReleaseAddressableArtOnly();
            ClearCardArtVisual();

            if (config == null || config.Icon == null || !config.Icon.RuntimeKeyIsValid())
            {
                yield break;
            }

            // 使用 RuntimeKey 经 Addressables 独立加载：同一 Icon 的 AssetReference 被多张牌 UI 同时使用时，
            // 再次调用 AssetReference.LoadAssetAsync 会触发「already been loaded」警告。
            var handle = Addressables.LoadAssetAsync<Sprite>(config.Icon.RuntimeKey);
            _artLoadHandle = handle;
            _hasArtHandle = true;

            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result)
            {
                cardArtImage.sprite = handle.Result;
                cardArtImage.enabled = true;
            }
            else
            {
                ClearCardArtVisual();
                ReleaseAddressableArtOnly();
            }
        }

        private void ClearCardArtVisual()
        {
            cardArtImage.sprite = null;
            cardArtImage.enabled = false;
        }

        private void ReleaseAddressableArtOnly()
        {
            if (!_hasArtHandle)
                return;

            if (_artLoadHandle.IsValid())
                Addressables.Release(_artLoadHandle);

            _hasArtHandle = false;
        }

        private void ApplyVisualTint()
        {
            visualRoot.color = _visualState switch
            {
                CardVisualState.Selected => selectedTint,
                CardVisualState.Disabled => disabledTint,
                _ => normalTint
            };
        }

        private void OnClickTargetInvoked()
        {
            onClicked?.Invoke();
        }
    }

    public enum CardVisualState
    {
        Normal = 0,
        Selected = 1,
        Disabled = 2
    }
}
