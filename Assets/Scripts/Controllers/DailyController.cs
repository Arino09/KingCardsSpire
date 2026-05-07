using KingCardsSpire.Core.Events;
using KingCardsSpire.Managers;
using UnityEngine;

namespace KingCardsSpire.Controllers
{
    /// <summary>
    /// 每日循环入口：订阅天数与结算事件；推进昼夜请调用 <see cref="GameManager.AdvanceDay"/>。
    /// </summary>
    public sealed class DailyController
    {
        private readonly EventManager _events;

        public DailyController(EventManager events, GameManager _)
        {
            _events = events;
            if (_events != null)
            {
                _events.Subscribe<DayChangedEvent>(OnDayChanged);
                _events.Subscribe<GameOverEvent>(OnGameOver);
            }
        }

        private void OnDayChanged(DayChangedEvent e)
        {
            Debug.Log($"[DailyController] 天数变更为 {e.Day}（结束当日请调用 GameManager.AdvanceDay）");
        }

        private void OnGameOver(GameOverEvent e)
        {
            Debug.Log($"[DailyController] 游戏结束: {e.Reason}");
        }
    }
}
