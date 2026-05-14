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
                var ui = UIManager.Instance;
                if (ui != null)
                    ui.StartCoroutine(RunContinueThenMainHub(this));
            };
            _onNewGame = () =>
            {
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
        /// 关闭主菜单后动态加载游戏主界面（MainHub）；协程挂在 <see cref="UIManager"/> 上以免随菜单销毁中断。
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

            view._model.BeginNewRun(view._game, view._persistence);

            var ui = UIManager.Instance;
            if (ui == null)
                yield break;

            ui.Close(UIPanelId.MainMenu);

            if (view._game != null && !view._game.PlayerState.HasCompletedOpeningTutorial)
            {
                var dialogue = ServiceLocator.Get<DialogueController>();
                if (dialogue != null)
                    yield return ui.StartCoroutine(dialogue.PlayDialogue("tutorial_opening", null));

                var battleCtrl = ServiceLocator.Get<BattleController>();
                if (battleCtrl != null)
                    battleCtrl.RequestStartTutorialBattle();

                yield return ui.OpenAsync(UIPanelId.Battle);

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

                    ui.Close(UIPanelId.Battle);

                    if (playerVictory)
                    {
                        yield return RunBuffDraftIfNeeded(view._game, ui);
                        yield return ui.OpenAsync(UIPanelId.MainHub);
                    }
                    else
                        yield return ui.OpenAsync(UIPanelId.MainMenu);

                    yield break;
                }

                ui.Close(UIPanelId.Battle);
                yield return ui.OpenAsync(UIPanelId.MainMenu);
                yield break;
            }

            yield return RunBuffDraftIfNeeded(view._game, ui);
            yield return ui.OpenAsync(UIPanelId.MainHub);
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
            var ui = UIManager.Instance;
            if (ui != null)
                ui.StartCoroutine(SettingsView.OpenSettingsRoutine());
        }

        private void OnCardAlbumClicked()
        {
            Debug.Log("[MainMenu] 卡牌图鉴：尚未接入 UI 面板。");
        }

        private static void OnExitClicked()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
