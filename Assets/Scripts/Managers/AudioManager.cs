using System.Collections;
using System.Collections.Generic;
using KingCardsSpire.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace KingCardsSpire.Managers
{
    public sealed class AudioManager : PersistentMonoSingleton<AudioManager>
    {
        private AudioSource _bgm;
        private AudioSource _sfx;

        private string _currentBgmKey = string.Empty;
        private AsyncOperationHandle<AudioClip> _bgmClipHandle;
        private bool _bgmClipHandleValid;
        private int _bgmGeneration;

        private readonly Dictionary<string, AudioClip> _sfxClipCache = new();
        private readonly Dictionary<string, AsyncOperationHandle<AudioClip>> _sfxLoadHandles = new();

        private Coroutine _pendingBgmCoroutine;

        protected override void Awake()
        {
            base.Awake();
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            ReleaseBgmClip();
            ReleaseAllSfxClips();
            ServiceLocator.Unregister<AudioManager>();
            base.OnDestroy();
        }

        public void InitializeAudio()
        {
            var root = new GameObject("AudioSources");
            root.transform.SetParent(transform, false);
            _bgm = root.AddComponent<AudioSource>();
            _bgm.loop = true;
            _bgm.playOnAwake = false;
            _bgm.volume = 1f;
            _sfx = root.AddComponent<AudioSource>();
            _sfx.loop = false;
            _sfx.playOnAwake = false;
            _sfx.volume = 1f;
        }

        /// <summary>将 BGM / 音效总线音量应用到 AudioSource（0～1）。</summary>
        public void ApplyBgmSfxVolumes(float bgmVolume, float sfxVolume)
        {
            if (_bgm != null)
                _bgm.volume = Mathf.Clamp01(bgmVolume);
            if (_sfx != null)
                _sfx.volume = Mathf.Clamp01(sfxVolume);
        }

        /// <summary>读取当前 AudioSource 上的音量（未初始化时返回 1,1）。</summary>
        public void GetCurrentMixLevels(out float bgmVolume, out float sfxVolume)
        {
            bgmVolume = _bgm != null ? _bgm.volume : 1f;
            sfxVolume = _sfx != null ? _sfx.volume : 1f;
        }

        /// <summary>停止 BGM 并释放当前 BGM 资源句柄。</summary>
        public void StopBgm()
        {
            _bgmGeneration++;

            if (_pendingBgmCoroutine != null)
            {
                StopCoroutine(_pendingBgmCoroutine);
                _pendingBgmCoroutine = null;
            }

            if (_bgm != null)
            {
                _bgm.Stop();
                _bgm.clip = null;
            }

            ReleaseBgmClip();
            _currentBgmKey = string.Empty;
        }

        /// <summary>通过 Addressables 键加载并循环播放 BGM；相同键且已在播放则忽略。</summary>
        public void PlayBgm(string addressableKey)
        {
            if (_bgm == null || string.IsNullOrEmpty(addressableKey))
                return;

            if (string.Equals(_currentBgmKey, addressableKey, System.StringComparison.Ordinal)
                && _bgm.clip != null
                && _bgm.isPlaying)
                return;

            _bgmGeneration++;
            var generation = _bgmGeneration;

            if (_pendingBgmCoroutine != null)
                StopCoroutine(_pendingBgmCoroutine);

            _pendingBgmCoroutine = StartCoroutine(LoadAndPlayBgmRoutine(addressableKey, generation));
        }

        private IEnumerator LoadAndPlayBgmRoutine(string addressableKey, int generation)
        {
            _pendingBgmCoroutine = null;

            if (_bgm != null)
                _bgm.Stop();

            ReleaseBgmClip();

            var handle = Addressables.LoadAssetAsync<AudioClip>(addressableKey);
            yield return handle;

            if (generation != _bgmGeneration)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);

                yield break;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);

                Debug.LogWarning(
                    $"[AudioManager] BGM 加载失败 key={addressableKey} status={handle.Status} ex={handle.OperationException}");
                yield break;
            }

            _bgmClipHandle = handle;
            _bgmClipHandleValid = true;
            _currentBgmKey = addressableKey;

            if (_bgm != null)
            {
                _bgm.clip = handle.Result;
                _bgm.Play();
            }
        }

        /// <summary>通过 Addressables 键播放一次性音效（<see cref="AudioSource.PlayOneShot"/>）。</summary>
        public void PlaySfx(string addressableKey)
        {
            if (_sfx == null || string.IsNullOrEmpty(addressableKey))
                return;

            if (_sfxClipCache.TryGetValue(addressableKey, out var cached) && cached != null)
            {
                _sfx.PlayOneShot(cached, _sfx.volume);
                return;
            }

            StartCoroutine(LoadSfxAndPlayOneShotRoutine(addressableKey));
        }

        private IEnumerator LoadSfxAndPlayOneShotRoutine(string addressableKey)
        {
            var handle = Addressables.LoadAssetAsync<AudioClip>(addressableKey);
            yield return handle;

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);

                Debug.LogWarning(
                    $"[AudioManager] SFX 加载失败 key={addressableKey} status={handle.Status} ex={handle.OperationException}");
                yield break;
            }

            _sfxLoadHandles[addressableKey] = handle;
            _sfxClipCache[addressableKey] = handle.Result;
            if (_sfx != null)
                _sfx.PlayOneShot(handle.Result, _sfx.volume);
        }

        private void ReleaseBgmClip()
        {
            if (!_bgmClipHandleValid)
                return;

            if (_bgmClipHandle.IsValid())
                Addressables.Release(_bgmClipHandle);

            _bgmClipHandle = default;
            _bgmClipHandleValid = false;
        }

        private void ReleaseAllSfxClips()
        {
            foreach (var kv in _sfxLoadHandles)
            {
                if (kv.Value.IsValid())
                    Addressables.Release(kv.Value);
            }

            _sfxLoadHandles.Clear();
            _sfxClipCache.Clear();
        }
    }
}
