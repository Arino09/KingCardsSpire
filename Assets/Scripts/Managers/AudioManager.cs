using KingCardsSpire.Core;
using UnityEngine;

namespace KingCardsSpire.Managers
{
    public sealed class AudioManager : PersistentMonoSingleton<AudioManager>
    {
        AudioSource _bgm;
        AudioSource _sfx;

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
            _sfx = root.AddComponent<AudioSource>();
            _sfx.loop = false;
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
