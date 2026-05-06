using KingCardsSpire.Core;
using KingCardsSpire.Core.Singleton;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Managers
{
    public sealed class BattleManager : PersistentMonoSingleton<BattleManager>
    {
        public BattleState CurrentBattle { get; private set; } = new();

        protected override void Awake()
        {
            base.Awake();
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            ServiceLocator.Unregister<BattleManager>();
            base.OnDestroy();
        }

        public void InitializeBattle() { }

        public void StartBattle()
        {
            // TODO: 进入战斗
        }

        public void EndBattle()
        {
            // TODO: 结束战斗
        }
    }
}
