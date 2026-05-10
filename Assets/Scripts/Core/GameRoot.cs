using System.Collections;
using KingCardsSpire.Controllers;
using KingCardsSpire.Core;
using KingCardsSpire.Core.Events;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Core
{
    /// <summary>
    /// 场景入口：创建常驻 GameSystems 并驱动启动协程。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameRoot : MonoBehaviour
    {
        [Header("UI")]
        [Tooltip("单场景中用于挂载所有 UI 面板的 RectTransform（通常为 Canvas 下的容器）；游戏流程仅通过 UIManager 切换面板。")]
        [SerializeField] private RectTransform sceneUiRoot;

        private static bool s_runtimeBootCompleted;

        private void Awake()
        {
            if (EventManager.Instance != null)
                return;

            var systems = new GameObject("GameSystems");
            DontDestroyOnLoad(systems);
            systems.AddComponent<EventManager>();
            systems.AddComponent<AssetManager>();
            systems.AddComponent<PersistenceManager>();
            systems.AddComponent<ConfigManager>();
            systems.AddComponent<AudioManager>();
            systems.AddComponent<UIManager>();
            systems.AddComponent<GameManager>();
            systems.AddComponent<BattleManager>();
        }

        private IEnumerator Start()
        {
            if (s_runtimeBootCompleted)
                yield break;

            if (EventManager.Instance == null)
                yield break;

            yield return AssetManager.Instance.InitializeAsync();
            PersistenceManager.Instance.InitializeSync();
            yield return ConfigManager.Instance.InitializeConfigsAsync();

            AudioManager.Instance.InitializeAudio();
            UIManager.Instance.InitializeUi(sceneUiRoot);
            GameManager.Instance.InitializeGame();
            BattleManager.Instance.InitializeBattle();

            ServiceLocator.Register(new BattleController());
            ServiceLocator.Register(new DailyController(EventManager.Instance, GameManager.Instance));

            var cfg = ConfigManager.Instance;
            Debug.Log(
                $"[GameRoot] all managers initialized. Configs loaded: {cfg.CardCount} cards / {cfg.BuffCount} buffs / {cfg.WeatherCount} weathers / {cfg.ShopConfigCount} shop rows / game configs: {cfg.GameConfigCount} / tower rows: {cfg.TowerConfigCount}");

            EventManager.Instance.Publish(new GameBootedEvent());

            yield return UIManager.Instance.OpenAsync(UIPanelId.MainMenu);

            s_runtimeBootCompleted = true;
        }
    }
}
