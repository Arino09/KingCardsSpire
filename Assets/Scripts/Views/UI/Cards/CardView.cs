using System;
using System.Collections;
using KingCardsSpire.Configs;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
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
        [SerializeField] private Color normalTint = Color.white;
        [SerializeField] private Color selectedTint = new(0.85f, 0.95f, 1f, 1f);
        [SerializeField] private Color disabledTint = new(0.55f, 0.55f, 0.55f, 1f);
        
        private AsyncOperationHandle<Sprite> _artLoadHandle;
        private bool _hasArtHandle;
        private CardVisualState _visualState = CardVisualState.Normal;

        /// <summary>最近一次 <see cref="Apply"/> 绑定的模板 <see cref="CardViewModel.CardId"/>；用于同 Id 时跳过重复加载卡画。</summary>
        private string _lastAppliedCardId = string.Empty;

        private bool _faceDown;

        public int HandIndex { get; set; }

        private void Awake()
        {
            clickTarget.onClick.AddListener(OnClickTargetInvoked);
        }

        private void OnDestroy()
        {
            clickTarget.onClick.RemoveListener(OnClickTargetInvoked);

            ReleaseAddressableArtOnly();
        }

        public void SetScale(float scale)
        {
            var s = Vector3.one * scale;
            rectRoot.sizeDelta = new Vector2(842, 1180) * scale;
            rectBack.localScale = s;
            rectFront.localScale = s;
            rectImage.localScale = s;
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
            if (faceDown)
            {
                ClearCardArtVisual();
                _lastAppliedCardId = string.Empty;
            }

            clickTarget.interactable = !faceDown;

            SetVisualState(faceDown ? CardVisualState.Disabled : CardVisualState.Normal);
        }

        public void Apply(CardViewModel vm)
        {
            if (vm == null)
            {
                Clear();
                return;
            }

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

        private IEnumerator LoadCardArtFromConfigRoutine(CardConfigEntry config)
        {
            ReleaseAddressableArtOnly();
            ClearCardArtVisual();

            if (config == null || config.Icon == null || !config.Icon.RuntimeKeyIsValid())
            {
                yield break;
            }
            
            var handle = config.Icon.LoadAssetAsync<Sprite>();
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
