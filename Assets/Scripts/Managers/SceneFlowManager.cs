using System.Collections;
using KingCardsSpire.Core;
using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

namespace KingCardsSpire.Managers
{
    public sealed class SceneFlowManager : PersistentMonoSingleton<SceneFlowManager>
    {
        protected override void Awake()
        {
            base.Awake();
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            ServiceLocator.Unregister<SceneFlowManager>();
            base.OnDestroy();
        }

        public void InitializeFlow() { }

        /// <summary>
        /// Boot 场景使用 Build Settings 名称加载；其余场景走 Addressables。
        /// </summary>
        public IEnumerator LoadSceneAsync(SceneId sceneId)
        {
            if (sceneId == SceneId.Boot)
            {
                yield return SceneManager.LoadSceneAsync("Boot");
                yield break;
            }

            var address = sceneId.ToAddress();
            if (string.IsNullOrEmpty(address))
            {
                Debug.LogWarning($"[SceneFlowManager] 未配置 Addressables 场景地址: {sceneId}");
                yield break;
            }

            var handle = Addressables.LoadSceneAsync(address, LoadSceneMode.Single);
            yield return handle;
        }
    }
}
