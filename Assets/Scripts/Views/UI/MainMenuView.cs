using System;
using System.Collections;
using KingCardsSpire.Controllers;
using KingCardsSpire.Core;
using KingCardsSpire.Core.Events;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KingCardsSpire.Views.UI
{
    /// <summary>
    /// 主菜单：继续 / 新游戏 / 图鉴 / 设置 / 退出；控件请在编辑器中拖到下方字段。
    /// </summary>
    public sealed class MainMenuView : BaseView
    {
        [Header("按钮")]
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button cardAlbumButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;

        private MainMenuModel _model;
        private PersistenceManager _persistence;
        private GameManager _game;

        private UnityAction _onContinue;
        private UnityAction _onNewGame;
        private UnityAction _onCardAlbum;
        private UnityAction _onSettings;
        private UnityAction _onExit;

        private bool _initialized;

        public override void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            SetPanelId(UIPanelId.MainMenu);
            _model = new MainMenuModel();
            _persistence = PersistenceManager.Instance;
            _game = GameManager.Instance;

            BindListeners();
            RefreshContinueAvailability();
        }

        public override void Dispose()
        {
            UnbindListeners();
            base.Dispose();
        }

        private void OnDestroy()
        {
            UnbindListeners();
        }

        protected override void OnOpen()
        {
            RefreshContinueAvailability();
        }

        private void BindListeners()
        {
            UnbindListeners();

            _onContinue = () =>
            {
                UiButtonSfx.PlayDefaultClick();
                var ui = UIManager.Instance;
                if (ui != null)
                    ui.StartCoroutine(RunContinueThenMainHub(this));
            };
            _onNewGame = () =>
            {
                UiButtonSfx.PlayDefaultClick();
                var ui = UIManager.Instance;
                if (ui != null)
                    ui.StartCoroutine(RunNewGameThenMainHub(this));
            };
            _onCardAlbum = OnCardAlbumClicked;
            _onSettings = OnSettingsClicked;
            _onExit = OnExitClicked;

            AddClick(continueButton, _onContinue);
            AddClick(newGameButton, _onNewGame);
            AddClick(cardAlbumButton, _onCardAlbum);
            AddClick(settingsButton, _onSettings);
            AddClick(exitButton, _onExit);
        }

        private static void AddClick(Button btn, UnityAction action)
        {
            if (btn == null || action == null)
                return;
            btn.onClick.AddListener(action);
        }

        private void UnbindListeners()
        {
            RemoveClick(continueButton, _onContinue);
            RemoveClick(newGameButton, _onNewGame);
            RemoveClick(cardAlbumButton, _onCardAlbum);
            RemoveClick(settingsButton, _onSettings);
            RemoveClick(exitButton, _onExit);

            _onContinue = null;
            _onNewGame = null;
            _onCardAlbum = null;
            _onSettings = null;
            _onExit = null;
        }

        private static void RemoveClick(Button btn, UnityAction action)
        {
            if (btn == null || action == null)
                return;
            btn.onClick.RemoveListener(action);
        }

        private void RefreshContinueAvailability()
        {
            if (continueButton == null)
                return;

            var available = _model != null && _model.HasContinueAvailable(_persistence);
            continueButton.interactable = available;
        }

        /// <summary>
        /// 先打开目标界面再关闭主菜单，避免 Addressables 加载间隙闪屏；协程挂在 <see cref="UIManager"/> 上以免随菜单销毁中断。
        /// </summary>
        private static IEnumerator RunContinueThenMainHub(MainMenuView view)
        {
            if (view._persistence == null || view._game == null || view._model == null)
                yield break;

            var save = view._persistence.Load();
            if (save == null)
            {
                view.RefreshContinueAvailability();
                yield break;
            }

            view._model.ApplyContinue(view._game, save);

            var ui = UIManager.Instance;
            if (ui == null)
                yield break;

            yield return ui.OpenAsync(UIPanelId.MainHub);
            ui.Close(UIPanelId.MainMenu);
        }

        private static IEnumerator RunNewGameThenMainHub(MainMenuView view)
        {
            if (view._persistence == null || view._game == null || view._model == null)
                yield break;

            var ui = UIManager.Instance;
            if (ui == null)
                yield break;

            view._model.BeginNewRun(view._game, view._persistence);

            if (view._game != null && !view._game.PlayerState.HasCompletedOpeningTutorial)
            {
                view._game.SetDeferOpeningTutorialBattleIntro(true);
                try
                {
                    // 先关主菜单再播开场对白，避免对白结束、战斗尚未加载完成的一帧仍露出主菜单。
                    ui.Close(UIPanelId.MainMenu);

                    var dialogue = ServiceLocator.Get<DialogueController>();
                    if (dialogue != null)
                        yield return ui.StartCoroutine(dialogue.PlayDialogue("tutorial_opening", null));

                    var battleCtrl = ServiceLocator.Get<BattleController>();
                    if (battleCtrl != null)
                        battleCtrl.RequestStartTutorialBattle();

                    yield return ui.OpenAsync(UIPanelId.Battle);
                }
                finally
                {
                    if (view._game != null)
                        view._game.SetDeferOpeningTutorialBattleIntro(false);
                }

                var events = EventManager.Instance;
                if (events != null)
                {
                    var flowFinished = false;
                    var playerVictory = false;

                    void OnOpeningTutorialBattleFlowDone(OpeningTutorialBattleFlowCompletedEvent e)
                    {
                        playerVictory = e.PlayerVictory;
                        flowFinished = true;
                    }

                    events.Subscribe<OpeningTutorialBattleFlowCompletedEvent>(OnOpeningTutorialBattleFlowDone);
                    while (!flowFinished)
                        yield return null;

                    events.Unsubscribe<OpeningTutorialBattleFlowCompletedEvent>(OnOpeningTutorialBattleFlowDone);

                    if (playerVictory)
                        view._game.SetOpeningTutorialCompletedAndSave();

                    if (playerVictory)
                    {
                        yield return RunBuffDraftIfNeeded(view._game, ui);
                        yield return ui.OpenAsync(UIPanelId.MainHub);
                    }
                    else
                        yield return ui.OpenAsync(UIPanelId.MainMenu);

                    // 在下一界面已叠在战斗之上后再关战斗，避免加载间隙闪屏（见 BattleView.TutorialPostBattleFlowRoutine）。
                    ui.Close(UIPanelId.Battle);

                    yield break;
                }

                yield return ui.OpenAsync(UIPanelId.MainMenu);
                ui.Close(UIPanelId.Battle);
                yield break;
            }

            yield return RunBuffDraftIfNeeded(view._game, ui);
            yield return ui.OpenAsync(UIPanelId.MainHub);
            ui.Close(UIPanelId.MainMenu);
        }

        private static IEnumerator RunBuffDraftIfNeeded(GameManager game, UIManager ui)
        {
            if (game == null || ui == null)
                yield break;
            if (!game.ShouldOfferBuffDraft())
                yield break;

            game.TryBuildBuffDraftOffer();
            yield return ui.OpenAsync(UIPanelId.BuffRewardView);
            while (ui.IsPanelOpen(UIPanelId.BuffRewardView))
                yield return null;
        }

        private void OnSettingsClicked()
        {
            UiButtonSfx.PlayDefaultClick();
            var ui = UIManager.Instance;
            if (ui != null)
                ui.StartCoroutine(SettingsView.OpenSettingsRoutine());
        }

        private void OnCardAlbumClicked()
        {
            UiButtonSfx.PlayDefaultClick();
            var ui = UIManager.Instance;
            if (ui != null)
                ui.StartCoroutine(OpenAlbumRoutine());
        }

        private static IEnumerator OpenAlbumRoutine()
        {
            var ui = UIManager.Instance;
            if (ui == null)
                yield break;

            yield return ui.OpenAsync(UIPanelId.Album);
        }

        private static void OnExitClicked()
        {
            UiButtonSfx.PlayDefaultClick();
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
