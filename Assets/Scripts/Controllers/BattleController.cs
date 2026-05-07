using KingCardsSpire.Core;
using KingCardsSpire.Managers;

namespace KingCardsSpire.Controllers
{
    /// <summary>
    /// 对战入口：由 UI 或调试代码调用，依赖 BattleManager 状态机。
    /// </summary>
    public sealed class BattleController
    {
        public void RequestStartBattle()
        {
            BattleManager.Instance?.StartBattleFromPlayerState();
        }

        public bool TryPlayCard(int playerHandIndex, out string error)
        {
            if (BattleManager.Instance == null)
            {
                error = "BattleManager 未就绪";
                return false;
            }

            return BattleManager.Instance.TrySubmitPlayerCard(playerHandIndex, out error);
        }

        public void RequestEndBattle()
        {
            BattleManager.Instance?.EndBattle();
        }

        public bool IsBattleActive => BattleManager.Instance != null && BattleManager.Instance.IsBattleActive;
    }
}
