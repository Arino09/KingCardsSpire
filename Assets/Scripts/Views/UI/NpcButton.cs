using System.Collections;
using KingCardsSpire.Core;
using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>
    /// NPC 列表单项：通过 Addressables 从 <c>Assets/GameAssets/Sprites/Character/NPC/Avatar/</c> 动态加载头像；
    /// 与塔配置中的 <c>npcId</c> 约定见 <see cref="TryResolveAvatarFileName"/>。
    /// </summary>
    public sealed class NpcButton : MonoBehaviour
    {
        [Header("绑定")]
        [SerializeField] private Image avatar;
        [SerializeField] private Button button;
        private const string NewEncounterAvatarId = "NPC/Avatar/0_1.png";

        private Coroutine _loadRoutine;
        private AsyncOperationHandle<Sprite> _avatarLoadHandle;
        private bool _hasAvatarLoadHandle;

        public Button Button => button;

        private void Start()
        {
            avatar.enabled = false;
        }

        /// <summary>将列表项外观绑定到视图模型（异步拉取头像）。</summary>
        public void Apply(NpcHubButtonSpec spec)
        {
            CancelLoadAndRelease();

            switch (spec.Kind)
            {
                case NpcHubButtonKind.NewEncounter:
                    if (string.IsNullOrEmpty(NewEncounterAvatarId))
                    {
                        SetAvatarSprite(null);
                        return;
                    }

                    _loadRoutine = StartCoroutine(LoadAvatarRoutine(
                        DialogueArtResolver.ResolveCharacterAddress(NewEncounterAvatarId)));
                    break;
                case NpcHubButtonKind.MetNpc:
                    var address = DialogueArtResolver.ResolveCharacterAddress(spec.AvatarId);
                    if (string.IsNullOrEmpty(address))
                    {
                        Debug.LogWarning($"[NpcButton] NPC 表未配置头像: {spec.NpcId}");
                        SetAvatarSprite(null);
                        return;
                    }

                    _loadRoutine = StartCoroutine(LoadAvatarRoutine(address));
                    break;
                default:
                    SetAvatarSprite(null);
                    break;
            }
        }

        private void OnDestroy()
        {
            CancelLoadAndRelease();
        }

        private void CancelLoadAndRelease()
        {
            if (_loadRoutine != null)
            {
                StopCoroutine(_loadRoutine);
                _loadRoutine = null;
            }

            ReleaseAvatarHandle();
        }

        private IEnumerator LoadAvatarRoutine(string address)
        {
            var handle = Addressables.LoadAssetAsync<Sprite>(address);
            _avatarLoadHandle = handle;
            _hasAvatarLoadHandle = true;

            yield return handle;

            _loadRoutine = null;

            if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
            {
                SetAvatarSprite(handle.Result);
            }
            else
            {
                Debug.LogWarning(
                    $"[NpcButton] 头像加载失败 address={address} status={handle.Status} ex={handle.OperationException}");
                SetAvatarSprite(null);
                ReleaseAvatarHandle();
            }
        }

        private void SetAvatarSprite(Sprite sprite)
        {
            avatar.sprite = sprite;
            avatar.enabled = sprite != null;
        }

        private void ReleaseAvatarHandle()
        {
            if (!_hasAvatarLoadHandle)
                return;

            avatar.sprite = null;

            if (_avatarLoadHandle.IsValid())
                Addressables.Release(_avatarLoadHandle);

            _hasAvatarLoadHandle = false;
        }

    }
}
