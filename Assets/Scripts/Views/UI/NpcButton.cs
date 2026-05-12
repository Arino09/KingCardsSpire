using System.Collections;
using System.Text.RegularExpressions;
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
        private const string AvatarAddressPrefix = "Sprites/Character/NPC/Avatar/";

        [Header("绑定")]
        [SerializeField] private Image avatar;
        [SerializeField] private Button button;
        private const string NewEncounterAvatarName = "0_1.png";

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
                    if (string.IsNullOrEmpty(NewEncounterAvatarName))
                    {
                        SetAvatarSprite(null);
                        return;
                    }

                    _loadRoutine = StartCoroutine(LoadAvatarRoutine(AvatarAddressPrefix + NewEncounterAvatarName));
                    break;
                case NpcHubButtonKind.MetNpc:
                    if (!TryResolveAvatarFileName(spec.NpcId, out var fileName))
                    {
                        Debug.LogWarning($"[NpcButton] 无法将 npcId 解析为头像文件名: {spec.NpcId}");
                        SetAvatarSprite(null);
                        return;
                    }

                    _loadRoutine = StartCoroutine(LoadAvatarRoutine(AvatarAddressPrefix + fileName));
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

        /// <summary>
        /// 将塔层配置的 <c>npcId</c> 解析为 Avatar 目录下的文件名（含扩展名）。
        /// 支持：<c>npc_2_1</c>→<c>2_1.png</c>、<c>npc_floor_5</c>→<c>5_1.png</c>、首层中文名映射、以及已为 <c>N_M.png</c> 形式的 id。
        /// </summary>
        private static bool TryResolveAvatarFileName(string npcId, out string fileName)
        {
            fileName = null;
            if (string.IsNullOrEmpty(npcId))
                return false;

            var m = Regex.Match(npcId, @"^npc_(\d+)_(\d+)$");
            if (m.Success)
            {
                fileName = $"{m.Groups[1].Value}_{m.Groups[2].Value}.png";
                return true;
            }

            m = Regex.Match(npcId, @"^npc_floor_(\d+)$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                fileName = $"{m.Groups[1].Value}_1.png";
                return true;
            }

            if (Regex.IsMatch(npcId, @"^\d+_\d+$"))
            {
                fileName = npcId + ".png";
                return true;
            }

            if (npcId.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) &&
                npcId.IndexOf('/') < 0 &&
                npcId.IndexOf('\\') < 0)
            {
                fileName = npcId;
                return true;
            }

            return false;
        }
    }
}
