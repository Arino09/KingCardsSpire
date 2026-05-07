using KingCardsSpire.Core;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Managers
{
    public sealed class GameManager : PersistentMonoSingleton<GameManager>
    {
        public PlayerData PlayerState { get; private set; } = new();
        public FloorState FloorState { get; private set; } = new();

        protected override void Awake()
        {
            base.Awake();
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            ServiceLocator.Unregister<GameManager>();
            base.OnDestroy();
        }

        public void InitializeGame() { }

        public void StartNewGame()
        {
            // TODO: 新游戏流程
        }

        public void EnterNextFloor()
        {
            // TODO: 进入下一层
        }

        public void AdvanceDay()
        {
            // TODO: 结束当日
        }
    }
}
