using System;
using System.Collections;
using System.Collections.Generic;
using KingCardsSpire.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace KingCardsSpire.Managers
{
    /// <summary>
    /// Addressables 统一入口，跟踪句柄以便统一释放。
    /// </summary>
    public sealed class AssetManager : PersistentMonoSingleton<AssetManager>
    {
        private bool _initialized;
        private readonly List<AsyncOperationHandle> _tracked = new();

        protected override void Awake()
        {
            base.Awake();
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            ReleaseAll();
            ServiceLocator.Unregister<AssetManager>();
            base.OnDestroy();
        }

        public IEnumerator InitializeAsync()
        {
            if (_initialized)
                yield break;

            var op = Addressables.InitializeAsync();
            yield return op;
            _initialized = true;
        }

        public IEnumerator LoadAssetsAsync<T>(string label, Action<IList<T>> onComplete) where T : UnityEngine.Object
        {
            var handle = Addressables.LoadAssetsAsync<T>(label, null);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _tracked.Add(handle);
                onComplete?.Invoke(handle.Result);
            }
            else
            {
                Debug.LogWarning(
                    $"[AssetManager] 按标签加载失败 label={label} status={handle.Status} ex={handle.OperationException}");
                onComplete?.Invoke(Array.Empty<T>());
            }
        }

        public IEnumerator LoadAssetAsync<T>(string key, Action<T> onComplete) where T : UnityEngine.Object
        {
            var handle = Addressables.LoadAssetAsync<T>(key);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _tracked.Add(handle);
                onComplete?.Invoke(handle.Result);
            }
            else
            {
                Debug.LogWarning(
                    $"[AssetManager] 按地址加载失败 key={key} status={handle.Status} ex={handle.OperationException}");
                onComplete?.Invoke(null);
            }
        }

        /// <summary>
        /// 实例化并等待完成（供协程 yield）。
        /// </summary>
        public IEnumerator InstantiateAndWait(string key, Transform parent, Action<GameObject> onComplete)
        {
            var handle = Addressables.InstantiateAsync(key, parent, false);
            yield return handle;

            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                _tracked.Add(handle);
                onComplete?.Invoke(handle.Result);
            }
            else
            {
                Debug.LogWarning(
                    $"[AssetManager] 实例化失败 key={key} status={handle.Status} ex={handle.OperationException}");
                onComplete?.Invoke(null);
            }
        }

        public void ReleaseAll()
        {
            foreach (var h in _tracked)
            {
                if (h.IsValid())
                    Addressables.Release(h);
            }

            _tracked.Clear();
        }
    }
}
