using KingCardsSpire.Core;
using KingCardsSpire.Core.Events;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Managers
{
    /// <summary>
    /// 根据当前 UI 与 Run 状态选择 BGM，并驱动 <see cref="AudioManager"/>。
    /// </summary>
    public sealed class GameAudioDirector : MonoBehaviour
    {
        public static GameAudioDirector Instance { get; private set; }

        private EventManager _events;
        private bool _endingStoryBgmActive;

        private void Awake()
        {
            Instance = this;
            _events = EventManager.Instance;
            if (_events != null)
            {
                _events.Subscribe<EndingStoryBgmActiveEvent>(OnEndingStoryBgmActive);
                _events.Subscribe<FloorChangedEvent>(OnRunContextChanged);
                _events.Subscribe<GameStartedEvent>(OnRunContextChanged);
                _events.Subscribe<GameOverEvent>(OnRunContextChanged);
            }
        }

        private void OnDestroy()
        {
            if (_events != null)
            {
                _events.Unsubscribe<EndingStoryBgmActiveEvent>(OnEndingStoryBgmActive);
                _events.Unsubscribe<FloorChangedEvent>(OnRunContextChanged);
                _events.Unsubscribe<GameStartedEvent>(OnRunContextChanged);
                _events.Unsubscribe<GameOverEvent>(OnRunContextChanged);
            }

            if (Instance == this)
                Instance = null;
        }

        private void OnEndingStoryBgmActive(EndingStoryBgmActiveEvent e)
        {
            _endingStoryBgmActive = e.Active;
            RefreshFromUiState();
        }

        private void OnRunContextChanged(FloorChangedEvent _) => RefreshFromUiState();

        private void OnRunContextChanged(GameStartedEvent _) => RefreshFromUiState();

        private void OnRunContextChanged(GameOverEvent _) => RefreshFromUiState();

        public void RefreshFromUiState()
        {
            var audio = AudioManager.Instance;
            if (audio == null)
                return;

            var key = ResolveBgmAddressableKey();
            if (string.IsNullOrEmpty(key))
            {
                audio.StopBgm();
                return;
            }

            audio.PlayBgm(key);
        }

        /// <summary>NPC 房、主角房与 Hub 子界面共用当前层 Hub 分段 BGM，不单独切曲以便沿用正在播放的同一 Address。</summary>
        private static bool IsHubTierPanelOpen(UIManager ui)
        {
            return ui.IsPanelOpen(UIPanelId.MainHub)
                   || ui.IsPanelOpen(UIPanelId.NpcHub)
                   || ui.IsPanelOpen(UIPanelId.HeroRoom)
                   || ui.IsPanelOpen(UIPanelId.Shop)
                   || ui.IsPanelOpen(UIPanelId.DailyChoice)
                   || ui.IsPanelOpen(UIPanelId.BuffRewardView)
                   || ui.IsPanelOpen(UIPanelId.DeckStorage)
                   || ui.IsPanelOpen(UIPanelId.NpcRecord)
                   || ui.IsPanelOpen(UIPanelId.DialogueRecord)
                   || ui.IsPanelOpen(UIPanelId.CardReward)
                   || ui.IsPanelOpen(UIPanelId.CardList);
        }

        private static string ResolveHubTierBgmKey(int floor1Based)
        {
            var f = Mathf.Clamp(floor1Based, 1, 7);
            if (f <= 3)
                return AudioAddressKeys.BgmHubTierLow;
            if (f <= 6)
                return AudioAddressKeys.BgmHubTierMid;
            return AudioAddressKeys.BgmHubTierHigh;
        }

        private string ResolveBgmAddressableKey()
        {
            var ui = UIManager.Instance;
            var gm = GameManager.Instance;
            var bm = BattleManager.Instance;
            if (ui == null)
                return string.Empty;

            if (_endingStoryBgmActive)
                return AudioAddressKeys.BgmEnding;

            if (ui.IsPanelOpen(UIPanelId.Battle) && bm != null && bm.IsBattleActive)
            {
                if (bm.IsTutorialBattle)
                    return AudioAddressKeys.BgmTutorial;
                return AudioAddressKeys.BgmBattle;
            }

            if (gm != null && gm.PlayerState != null && !gm.PlayerState.HasCompletedOpeningTutorial
                && gm.DeferOpeningTutorialBattleIntro)
                return AudioAddressKeys.BgmTutorial;

            if (IsHubTierPanelOpen(ui) && gm != null && gm.PlayerState != null)
                return ResolveHubTierBgmKey(gm.PlayerState.CurrentFloor);

            if (ui.IsPanelOpen(UIPanelId.MainMenu))
                return AudioAddressKeys.BgmMainMenu;

            return string.Empty;
        }
    }
}
