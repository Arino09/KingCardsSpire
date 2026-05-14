using System;
using KingCardsSpire.Managers;

namespace KingCardsSpire.Models
{
    /// <summary>设置界面：从 GameManager 读写音量并持久化（分支逻辑集中在 GameManager）。</summary>
    public sealed class SettingsViewModel
    {
        private readonly GameManager _game;

        public SettingsViewModel(GameManager game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        public (float bgmVolume, float sfxVolume) ReadEffectiveVolumes() =>
            _game.GetEffectiveAudioVolumesForSettingsUi();

        public void ApplyVolumes(float bgmVolume, float sfxVolume) =>
            _game.ApplyAudioVolumesFromUiAndPersist(bgmVolume, sfxVolume);

        public bool TryPersistFromSaveButton() => _game.TryPersistCurrentRunFromSettingsSaveButton();
    }
}
