using System.Collections;
using System.Collections.Generic;
using KingCardsSpire.Controllers;
using KingCardsSpire.Core;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>原住民访问界面：对应预制体 NPCView。</summary>
    public sealed class NpcView : BaseView
    {
        private const string EmptyNewPoolMessage = "暂无新原住民";
        private const string NoAvailableDialogueMessage = "暂无可推进剧情";

        [Header("布局")]
        [SerializeField] private RectTransform groupRoot;

        [Header("预制体")]
        [SerializeField] private GameObject npcButtonPrefab;

        [Header("操作")]
        [SerializeField] private Button closeButton;

        [Header("提示（仅空池）")]
        [SerializeField] private GameObject tipDialogRoot;

        [SerializeField] private Text tipText;

        private GameManager _game;

        private UnityAction _onCloseClicked;
        private readonly List<Button> _entryButtons = new();
        private Coroutine _hideTipRoutine;

        public override void Initialize()
        {
            SetPanelId(UIPanelId.NpcHub);
            _game = GameManager.Instance;

            _onCloseClicked = OnCloseClicked;
            closeButton.onClick.AddListener(_onCloseClicked);
        }

        protected override void OnOpen()
        {
            HideTipPanel();
            RebuildList();
        }

        public override void Dispose()
        {
            StopHideTipRoutine();
            closeButton.onClick.RemoveListener(_onCloseClicked);
            ClearSpawnedEntries();
            base.Dispose();
        }

        private void OnDestroy()
        {
            StopHideTipRoutine();
            closeButton.onClick.RemoveListener(_onCloseClicked);
            ClearSpawnedEntries();
        }

        private void OnCloseClicked()
        {
            UiButtonSfx.PlayDefaultClick();
            UIManager.Instance.Close(UIPanelId.NpcHub);
        }

        private IEnumerator PlayNpcDialogueRoutine(string npcId, string startId)
        {
            var ui = UIManager.Instance;
            var dialogue = ServiceLocator.Get<DialogueController>();
            if (ui == null || dialogue == null)
                yield break;

            yield return ui.StartCoroutine(dialogue.PlayDialogue(startId, null));
            var gm = _game ?? GameManager.Instance;
            gm?.CompleteNpcDialogue(npcId);

            var openedCardReward = false;
            if (gm != null &&
                gm.GetNpcDialogueCompletedCount(npcId) >= StoryDialogueRules.MaxNpcStoryCount &&
                gm.TryPrepareNpcStoryCompletionRewards())
            {
                yield return ui.OpenAsync(UIPanelId.CardReward);
                openedCardReward = true;
            }

            if (openedCardReward)
            {
                while (ui.IsPanelOpen(UIPanelId.CardReward))
                    yield return null;
            }

            RebuildList();
        }

        private void RebuildList()
        {
            ClearSpawnedEntries();

            var gm = _game ?? GameManager.Instance;
            if (gm == null)
                return;

            var metSorted = gm.GetMetNpcIdsSortedCopy();
            var cfg = ConfigManager.Instance;
            var vm = NpcHubViewModel.Build(
                metSorted,
                gm.IsNewNpcEncounterPoolEmpty(),
                id => ResolveNpcDisplayName(cfg, id),
                id => ResolveNpcAvatarId(cfg, id));

            for (var i = 0; i < vm.Buttons.Count; i++)
            {
                var spec = vm.Buttons[i];
                var go = Instantiate(npcButtonPrefab, groupRoot);
                var npcButton = go.GetComponent<NpcButton>();
                if (npcButton != null)
                    npcButton.Apply(spec);
                else
                    Debug.LogWarning("[NpcView] npcButtonPrefab 缺少 NpcButton，无法加载 NPC 头像。");

                var btn = npcButton != null && npcButton.Button != null
                    ? npcButton.Button
                    : go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>(true);
                if (btn == null)
                {
                    Debug.LogError("[NpcView] npcButtonPrefab 上未找到 Button。");
                    Destroy(go);
                    continue;
                }

                var label = go.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = spec.LabelText;
                }

                _entryButtons.Add(btn);
                if (spec.Kind == NpcHubButtonKind.NewEncounter)
                    btn.onClick.AddListener(OnNewEncounterClicked);
                else
                {
                    var capturedId = spec.NpcId;
                    btn.onClick.AddListener(() => OnMetNpcClicked(capturedId));
                }
            }
        }

        private void OnNewEncounterClicked()
        {
            UiButtonSfx.PlayDefaultClick();
            var gm = _game ?? GameManager.Instance;
            if (gm == null)
                return;

            if (gm.IsNewNpcEncounterPoolEmpty())
            {
                ShowTipMessage(EmptyNewPoolMessage);
                return;
            }

            if (!gm.TryPrepareRandomNewNpcDialogue(out var picked, out var startId) || string.IsNullOrEmpty(picked))
            {
                ShowTipMessage(NoAvailableDialogueMessage);
                return;
            }

            LockAllEntryButtons();
            StartCoroutine(PlayNpcDialogueRoutine(picked, startId));
        }

        private void OnMetNpcClicked(string npcId)
        {
            UiButtonSfx.PlayDefaultClick();
            var gm = _game ?? GameManager.Instance;
            if (gm == null)
                return;

            if (!gm.TryPrepareNpcDialogue(npcId, out var startId))
            {
                ShowTipMessage(NoAvailableDialogueMessage);
                return;
            }

            LockAllEntryButtons();
            StartCoroutine(PlayNpcDialogueRoutine(npcId, startId));
        }

        private void LockAllEntryButtons()
        {
            for (var i = 0; i < _entryButtons.Count; i++)
            {
                var b = _entryButtons[i];
                if (b != null)
                    b.interactable = false;
            }
        }

        private void ClearSpawnedEntries()
        {
            for (var i = 0; i < _entryButtons.Count; i++)
            {
                var b = _entryButtons[i];
                if (b != null)
                    b.onClick.RemoveAllListeners();
            }

            _entryButtons.Clear();

            if (groupRoot == null)
                return;

            for (var c = groupRoot.childCount - 1; c >= 0; c--)
            {
                var child = groupRoot.GetChild(c);
                if (child != null)
                    Destroy(child.gameObject);
            }
        }

        private void ShowTipMessage(string message)
        {
            if (tipText != null)
                tipText.text = message;

            if (tipDialogRoot != null)
                tipDialogRoot.SetActive(true);

            StopHideTipRoutine();
            _hideTipRoutine = StartCoroutine(HideTipAfterDelay(2.5f));
        }

        private IEnumerator HideTipAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            HideTipPanel();
            _hideTipRoutine = null;
        }

        private void HideTipPanel()
        {
            StopHideTipRoutine();
            if (tipDialogRoot != null)
                tipDialogRoot.SetActive(false);
        }

        private void StopHideTipRoutine()
        {
            if (_hideTipRoutine == null)
                return;

            StopCoroutine(_hideTipRoutine);
            _hideTipRoutine = null;
        }

        private static string ResolveNpcDisplayName(ConfigManager cfg, string npcId)
        {
            if (cfg != null && cfg.TryGetNpc(npcId, out var npc) && !string.IsNullOrEmpty(npc.DisplayName))
                return npc.DisplayName;

            return npcId;
        }

        private static string ResolveNpcAvatarId(ConfigManager cfg, string npcId)
        {
            if (cfg != null && cfg.TryGetNpc(npcId, out var npc))
                return npc.AvatarId;

            return string.Empty;
        }
    }
}
