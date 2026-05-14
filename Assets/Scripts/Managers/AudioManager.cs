using KingCardsSpire.Core;
using UnityEngine;

namespace KingCardsSpire.Managers
{
    public sealed class AudioManager : PersistentMonoSingleton<AudioManager>
    {
        private AudioSource _bgm;
        private AudioSource _sfx;

        protected override void Awake()
        {
            base.Awake();
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            ServiceLocator.Unregister<AudioManager>();
            base.OnDestroy();
        }

        public void InitializeAudio()
        {
            var root = new GameObject("AudioSources");
            root.transform.SetParent(transform, false);
            _bgm = root.AddComponent<AudioSource>();
            _bgm.loop = true;
            _bgm.volume = 1f;
            _sfx = root.AddComponent<AudioSource>();
            _sfx.loop = false;
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

        /// <summary>通过 Addressables 键加载 BGM（占位）。</summary>
        public void PlayBgm(string addressableKey)
        {
            if (_bgm == null || string.IsNullOrEmpty(addressableKey))
                return;
            // TODO: 使用 AssetManager 加载 AudioClip 后赋值 clip
            Debug.Log($"[AudioManager] PlayBgm 占位 key={addressableKey}");
        }

        public void PlaySfx(string addressableKey)
        {
            if (_sfx == null || string.IsNullOrEmpty(addressableKey))
                return;
            Debug.Log($"[AudioManager] PlaySfx 占位 key={addressableKey}");
        }
    }
}
