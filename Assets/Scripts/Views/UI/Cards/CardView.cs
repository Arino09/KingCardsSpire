using System.Collections;
using KingCardsSpire.Configs;
using KingCardsSpire.Models;
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
        [SerializeField] private Text levelBadgeText;
        [SerializeField] private Text typeSubText;
        [SerializeField] private Text nameText;
        [SerializeField] private Text effectText;
        [SerializeField] private Image cardArtImage;
        [SerializeField] private GameObject artPlaceholder;
        [SerializeField] private Button clickTarget;
        [SerializeField] private UnityEvent onClicked;
        [SerializeField] private Image visualRoot;
        [SerializeField] private Color normalTint = Color.white;
        [SerializeField] private Color selectedTint = new(0.85f, 0.95f, 1f, 1f);
        [SerializeField] private Color disabledTint = new(0.55f, 0.55f, 0.55f, 1f);
        [Tooltip("勾选后进入 Play 时在 Start 注入 Mock 卡牌数据；正式场景用的预制体请取消勾选。")]
        [SerializeField] private bool applyMockDataOnStart = true;

        private AsyncOperationHandle<Sprite> _artLoadHandle;
        private bool _hasArtHandle;
        private CardVisualState _visualState = CardVisualState.Normal;

        public int HandIndex { get; set; }

        private void Awake()
        {
            if (clickTarget != null)
                clickTarget.onClick.AddListener(OnClickTargetInvoked);
        }

        private void OnDestroy()
        {
            if (clickTarget != null)
                clickTarget.onClick.RemoveListener(OnClickTargetInvoked);

            ReleaseAddressableArtOnly();
        }

        private void Start()
        {
            if (!applyMockDataOnStart)
                return;

            var mockCard = new Card
            {
                Id = "mock_ui",
                Name = "名称（Mock）",
                Level = 3f,
                Type = CardType.Basic,
                EffectDesc = "效果区 Mock：用于检查换行与占位。\n第二行文案。",
                IsUnique = false,
                BattleInstanceId = string.Empty
            };

            Apply(CardViewModel.FromCard(mockCard));
        }

        public void Apply(CardViewModel vm)
        {
            if (vm == null)
            {
                Clear();
                return;
            }

            if (levelBadgeText != null)
                levelBadgeText.text = vm.LevelDisplay;

            if (typeSubText != null)
                typeSubText.text = vm.TypeDisplay;

            if (nameText != null)
                nameText.text = vm.Name;

            if (effectText != null)
                effectText.text = vm.Effect;

            ReleaseAddressableArtOnly();

            if (vm.Art != null)
            {
                if (cardArtImage != null)
                {
                    cardArtImage.sprite = vm.Art;
                    cardArtImage.enabled = true;
                }

                SetPlaceholderVisible(false);
            }
            else
            {
                ClearCardArtVisual();
            }

            ApplyVisualTint();
        }

        public void Clear()
        {
            if (levelBadgeText != null)
                levelBadgeText.text = string.Empty;

            if (typeSubText != null)
                typeSubText.text = string.Empty;

            if (nameText != null)
                nameText.text = string.Empty;

            if (effectText != null)
                effectText.text = string.Empty;

            ReleaseAddressableArtOnly();
            ClearCardArtVisual();
            HandIndex = 0;
            _visualState = CardVisualState.Normal;
            ApplyVisualTint();
        }

        /// <summary>
        /// 从 <see cref="CardConfig.Icon"/> 异步加载卡画；加载期间可先 <see cref="Apply(CardViewModel)"/> 展示文字。
        /// </summary>
        public void LoadCardArtFromConfig(CardConfig config)
        {
            if (!isActiveAndEnabled)
                return;

            StopAllCoroutines();
            StartCoroutine(LoadCardArtFromConfigRoutine(config));
        }

        public void SetVisualState(CardVisualState state)
        {
            _visualState = state;

            if (clickTarget != null)
                clickTarget.interactable = state != CardVisualState.Disabled;

            ApplyVisualTint();
        }

        private IEnumerator LoadCardArtFromConfigRoutine(CardConfig config)
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

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                if (cardArtImage != null)
                {
                    cardArtImage.sprite = handle.Result;
                    cardArtImage.enabled = true;
                }

                SetPlaceholderVisible(false);
            }
            else
            {
                ClearCardArtVisual();
                ReleaseAddressableArtOnly();
            }
        }

        private void ClearCardArtVisual()
        {
            if (cardArtImage != null)
            {
                cardArtImage.sprite = null;
                cardArtImage.enabled = false;
            }

            SetPlaceholderVisible(true);
        }

        private void SetPlaceholderVisible(bool visible)
        {
            if (artPlaceholder != null)
                artPlaceholder.SetActive(visible);
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
            if (visualRoot == null)
                return;

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
