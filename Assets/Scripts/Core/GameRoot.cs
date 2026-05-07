using System.Collections;
using KingCardsSpire.Controllers;
using KingCardsSpire.Core;
using KingCardsSpire.Core.Events;
using KingCardsSpire.Managers;
using UnityEngine;

namespace KingCardsSpire.Core
{
    /// <summary>
    /// 场景入口：创建常驻 GameSystems 并驱动启动协程。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameRoot : MonoBehaviour
    {
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
            systems.AddComponent<SceneFlowManager>();
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
            UIManager.Instance.InitializeUi();
            SceneFlowManager.Instance.InitializeFlow();
            GameManager.Instance.InitializeGame();
            BattleManager.Instance.InitializeBattle();

            ServiceLocator.Register(new BattleController());
            ServiceLocator.Register(new DailyController(EventManager.Instance, GameManager.Instance));

            var cfg = ConfigManager.Instance;
            Debug.Log(
                $"[GameRoot] all managers initialized. Configs loaded: {cfg.CardCount} cards / {cfg.BuffCount} buffs / {cfg.WeatherCount} weathers / {cfg.ShopConfigCount} shop rows / game configs: {cfg.GameConfigCount} / tower rows: {cfg.TowerConfigCount}");

            EventManager.Instance.Publish(new GameBootedEvent());
            s_runtimeBootCompleted = true;
        }
    }
}
