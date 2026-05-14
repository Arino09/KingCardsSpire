using System.Collections;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>全屏设置：音乐/音效滑条与返回、存档、回主菜单；控件请在预制体 Inspector 绑定。</summary>
    public sealed class SettingsView : BaseView
    {
        [Header("音量")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("按钮")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button titleButton;

        private SettingsViewModel _viewModel;
        private bool _uiApplyInProgress;
        private UnityAction<float> _onMusicChanged;
        private UnityAction<float> _onSfxChanged;
        private UnityAction _onResume;
        private UnityAction _onSave;
        private UnityAction _onTitle;

        public override void Initialize()
        {
            SetPanelId(UIPanelId.Settings);
            var gm = GameManager.Instance;
            _viewModel = new SettingsViewModel(gm);

            _onMusicChanged = OnMusicSliderChanged;
            _onSfxChanged = OnSfxSliderChanged;
            _onResume = OnResumeClicked;
            _onSave = OnSaveClicked;
            _onTitle = OnTitleClicked;

            musicSlider.onValueChanged.AddListener(_onMusicChanged);
            sfxSlider.onValueChanged.AddListener(_onSfxChanged);
            resumeButton.onClick.AddListener(_onResume);
            saveButton.onClick.AddListener(_onSave);
            titleButton.onClick.AddListener(_onTitle);
        }

        public override void Dispose()
        {
            musicSlider.onValueChanged.RemoveListener(_onMusicChanged);
            sfxSlider.onValueChanged.RemoveListener(_onSfxChanged);
            resumeButton.onClick.RemoveListener(_onResume);
            saveButton.onClick.RemoveListener(_onSave);
            titleButton.onClick.RemoveListener(_onTitle);

            _onMusicChanged = null;
            _onSfxChanged = null;
            _onResume = null;
            _onSave = null;
            _onTitle = null;

            base.Dispose();
        }

        protected override void OnOpen()
        {
            RefreshSaveButtonInteractable();
            ApplySlidersFromModel();
        }

        private void RefreshSaveButtonInteractable()
        {
            var ui = UIManager.Instance;
            var onMainMenu = ui != null && ui.IsPanelOpen(UIPanelId.MainMenu);
            saveButton.interactable = !onMainMenu;
        }

        private void ApplySlidersFromModel()
        {
            if (_viewModel == null)
                return;

            var (bgm, sfx) = _viewModel.ReadEffectiveVolumes();
            _uiApplyInProgress = true;
            musicSlider.SetValueWithoutNotify(bgm);
            sfxSlider.SetValueWithoutNotify(sfx);
            _uiApplyInProgress = false;
        }

        private void OnMusicSliderChanged(float value)
        {
            if (_uiApplyInProgress || _viewModel == null)
                return;

            _viewModel.ApplyVolumes(value, sfxSlider.value);
        }

        private void OnSfxSliderChanged(float value)
        {
            if (_uiApplyInProgress || _viewModel == null)
                return;

            _viewModel.ApplyVolumes(musicSlider.value, value);
        }

        private void OnResumeClicked()
        {
            UIManager.Instance?.Close(UIPanelId.Settings);
        }

        private void OnSaveClicked()
        {
            _viewModel?.TryPersistFromSaveButton();
        }

        private void OnTitleClicked()
        {
            var ui = UIManager.Instance;
            var gm = GameManager.Instance;
            if (ui == null || gm == null)
                return;

            ui.StartCoroutine(gm.ReturnToTitleFromSettingsRoutine());
        }

        /// <summary>供主菜单 / 主界面 / 战斗协程调用：打开设置面板。</summary>
        public static IEnumerator OpenSettingsRoutine()
        {
            var ui = UIManager.Instance;
            if (ui == null)
                yield break;

            yield return ui.OpenAsync(UIPanelId.Settings);
        }
    }
}
