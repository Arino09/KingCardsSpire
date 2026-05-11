using KingCardsSpire.Core;
using KingCardsSpire.Core.Battle;
using KingCardsSpire.Managers;

namespace KingCardsSpire.Controllers
{
    /// <summary>
    /// 对战入口：由 UI 或调试代码调用，依赖 BattleManager 状态机。
    /// </summary>
    public sealed class BattleController
    {
        public void RequestStartBattle(bool vsBoss = false)
        {
            BattleManager.Instance?.StartBattleFromPlayerState(vsBoss);
        }

        /// <summary>一步出牌（等同 Prepare → Stage → Commit），供无需动画编排的调用方。</summary>
        public bool TryPlayCard(int playerHandIndex, out string error)
        {
            if (BattleManager.Instance == null)
            {
                error = "BattleManager 未就绪";
                return false;
            }

            return BattleManager.Instance.TrySubmitPlayerCard(playerHandIndex, out error);
        }

        public bool PrepareEnemyPlay(out string error)
        {
            if (BattleManager.Instance == null)
            {
                error = "BattleManager 未就绪";
                return false;
            }

            return BattleManager.Instance.PrepareEnemyPlay(out error);
        }

        public bool TryStagePlayerCard(int playerHandIndex, out string error)
        {
            if (BattleManager.Instance == null)
            {
                error = "BattleManager 未就绪";
                return false;
            }

            return BattleManager.Instance.TryStagePlayerCard(playerHandIndex, out error);
        }

        public bool CommitPendingRound(out BattleCompareResult compareResult, out string error)
        {
            compareResult = default;
            if (BattleManager.Instance == null)
            {
                error = "BattleManager 未就绪";
                return false;
            }

            return BattleManager.Instance.CommitPendingRound(out compareResult, out error);
        }

        public void RequestEndBattle()
        {
            BattleManager.Instance?.EndBattle();
        }

        public bool IsBattleActive => BattleManager.Instance != null && BattleManager.Instance.IsBattleActive;

        public int PendingEnemyHandIndex =>
            BattleManager.Instance != null ? BattleManager.Instance.PendingEnemyHandIndex : -1;

        public int PendingPlayerHandIndex =>
            BattleManager.Instance != null ? BattleManager.Instance.PendingPlayerHandIndex : -1;
    }
}
