using KingCardsSpire.Managers;

namespace KingCardsSpire.Models
{
    /// <summary>
    /// 主菜单数据与操作意图（不含协程与 UnityEngine API，便于测试与复用）。
    /// </summary>
    public sealed class MainMenuModel
    {
        public bool HasContinueAvailable(PersistenceManager persistence)
        {
            return persistence != null && persistence.HasSave();
        }

        /// <summary>
        /// 继续游戏：将存档恢复到进行中 Run。
        /// </summary>
        public void ApplyContinue(GameManager game, SaveData save)
        {
            if (game == null || save == null)
                return;

            game.RestoreRunFromSave(save);
        }

        /// <summary>
        /// 新档开局：清空默认槽位存档并重置 Run。
        /// </summary>
        public void BeginNewRun(GameManager game, PersistenceManager persistence)
        {
            if (game == null || persistence == null)
                return;

            persistence.Delete();
            game.StartNewGame();
        }
    }
}
