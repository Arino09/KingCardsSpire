using System.Collections;
using KingCardsSpire.Controllers;
using KingCardsSpire.Core;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>主角房（访问参赛者）：对应预制体 HeroView。</summary>
    public sealed class HeroView : BaseView
    {
        [Header("主角入口（顺序：Hero2 / Heroine1 / Heroine2）")]
        [SerializeField] private Button heroSlot0Button;
        [SerializeField] private Button heroSlot1Button;
        [SerializeField] private Button heroSlot2Button;

        [Header("操作")]
        [SerializeField] private Button closeButton;

        [Header("选择层（对话 / 战斗 / 返回）")]
        [SerializeField] private GameObject choiceDialogRoot;
        [SerializeField] private Button dialogTalkButton;
        [SerializeField] private Button dialogBattleButton;
        [SerializeField] private Button dialogReturnButton;

        [Header("主角显示名（与按钮顺序一致）")]
        [SerializeField] private string[] heroDisplayNames =
        {
            "参赛者一",
            "参赛者二",
            "参赛者三"
        };

        [SerializeField] private Text[] heroDisplayNameTexts;

        private UnityAction _onClose;
        private UnityAction _onHero0;
        private UnityAction _onHero1;
        private UnityAction _onHero2;
        private UnityAction _onTalk;
        private UnityAction _onBattle;
        private UnityAction _onReturn;

        private int _selectedSlot = -1;
        private bool _wired;

        public override void Initialize()
        {
            SetPanelId(UIPanelId.HeroRoom);
            WireButtons();
            _wired = true;
        }

        protected override void OnOpen()
        {
            HideChoiceDialog();
            _selectedSlot = -1;
            RefreshHeroDisplayNames();
        }

        public override void Dispose()
        {
            UnwireButtons();
            _wired = false;
            base.Dispose();
        }

        private void OnDestroy()
        {
            if (_wired)
                UnwireButtons();
        }

        private void WireButtons()
        {
            _onClose = OnCloseClicked;
            _onHero0 = () => OnHeroSlotClicked(0);
            _onHero1 = () => OnHeroSlotClicked(1);
            _onHero2 = () => OnHeroSlotClicked(2);
            _onTalk = OnDialogTalkClicked;
            _onBattle = OnDialogBattleClicked;
            _onReturn = OnDialogReturnClicked;

            closeButton.onClick.AddListener(_onClose);
            heroSlot0Button.onClick.AddListener(_onHero0);
            heroSlot1Button.onClick.AddListener(_onHero1);
            heroSlot2Button.onClick.AddListener(_onHero2);
            dialogTalkButton.onClick.AddListener(_onTalk);
            dialogBattleButton.onClick.AddListener(_onBattle);
            dialogReturnButton.onClick.AddListener(_onReturn);
        }

        private void UnwireButtons()
        {
            closeButton.onClick.RemoveListener(_onClose);
            heroSlot0Button.onClick.RemoveListener(_onHero0);
            heroSlot1Button.onClick.RemoveListener(_onHero1);
            heroSlot2Button.onClick.RemoveListener(_onHero2);
            dialogTalkButton.onClick.RemoveListener(_onTalk);
            dialogBattleButton.onClick.RemoveListener(_onBattle);
            dialogReturnButton.onClick.RemoveListener(_onReturn);

            _onClose = null;
            _onHero0 = null;
            _onHero1 = null;
            _onHero2 = null;
            _onTalk = null;
            _onBattle = null;
            _onReturn = null;
        }

        private void OnCloseClicked()
        {
            UiButtonSfx.PlayDefaultClick();
            UIManager.Instance.Close(UIPanelId.HeroRoom);
        }

        private void OnHeroSlotClicked(int slotIndex)
        {
            UiButtonSfx.PlayDefaultClick();
            _selectedSlot = slotIndex;
            choiceDialogRoot.SetActive(true);
        }

        private void OnDialogReturnClicked()
        {
            UiButtonSfx.PlayDefaultClick();
            HideChoiceDialog();
        }

        private void OnDialogTalkClicked()
        {
            UiButtonSfx.PlayDefaultClick();
            if (_selectedSlot < 0)
                return;

            StartCoroutine(PlayHeroTalkRoutine());
        }

        private IEnumerator PlayHeroTalkRoutine()
        {
            var ui = UIManager.Instance;
            var dialogue = ServiceLocator.Get<DialogueController>();
            var gm = GameManager.Instance;
            if (ui == null || dialogue == null)
                yield break;

            var floor = gm != null ? gm.PlayerState.CurrentFloor : 1;
            if (gm == null || !gm.TryPrepareHeroDialogue(_selectedSlot, out var startId))
            {
                Debug.LogWarning($"[HeroView] 今日已交谈或没有可推进的参赛者剧情 slot={_selectedSlot} floor={floor}");
                HideChoiceDialog();
                yield break;
            }

            yield return ui.StartCoroutine(dialogue.PlayDialogue(startId, null));
            gm.CompleteHeroDialogue(_selectedSlot);
            HideChoiceDialog();
        }

        private void OnDialogBattleClicked()
        {
            UiButtonSfx.PlayDefaultClick();
            if (_selectedSlot < 0)
                return;

            var name = ResolveDisplayName(_selectedSlot);
            var slotId = _selectedSlot.ToString();
            StartCoroutine(StartHeroBattleRoutine(slotId, name));
        }

        private IEnumerator StartHeroBattleRoutine(string heroSlotId, string opponentDisplayName)
        {
            var ui = UIManager.Instance;
            var ctrl = ServiceLocator.Get<BattleController>();
            if (ui == null || ctrl == null)
                yield break;

            var gm = GameManager.Instance;
            if (gm != null && gm.HasBuff(BuffId.ChaoticBattlefield))
                yield return gm.RunChaoticBattlefieldPreBattlePickRoutine();

            ctrl.RequestStartHeroDuel(heroSlotId, opponentDisplayName);
            yield return ui.OpenAsync(UIPanelId.Battle);
            ui.Close(UIPanelId.HeroRoom);
        }

        private void HideChoiceDialog()
        {
            choiceDialogRoot.SetActive(false);
        }

        private string ResolveDisplayName(int slotIndex)
        {
            var cfg = ConfigManager.Instance;
            if (cfg != null && cfg.TryGetHeroBySlot(slotIndex, out var hero) && !string.IsNullOrEmpty(hero.DisplayName))
                return hero.DisplayName;

            if (heroDisplayNames != null && slotIndex >= 0 && slotIndex < heroDisplayNames.Length)
            {
                var n = heroDisplayNames[slotIndex];
                if (!string.IsNullOrEmpty(n))
                    return n;
            }

            return $"参赛者{slotIndex + 1}";
        }

        private void RefreshHeroDisplayNames()
        {
            if (heroDisplayNameTexts == null)
                return;

            for (var i = 0; i < heroDisplayNameTexts.Length; i++)
            {
                var label = heroDisplayNameTexts[i];
                if (label != null)
                    label.text = ResolveDisplayName(i);
            }
        }
    }
}
