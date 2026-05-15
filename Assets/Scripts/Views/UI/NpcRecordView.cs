using System.Collections;
using System.Collections.Generic;
using KingCardsSpire.Configs;
using KingCardsSpire.Core;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>已遇原住民对话历史入口：仅头像列表，对应预制体 NPCRecordView。</summary>
    public sealed class NpcRecordView : BaseView
    {
        [Header("布局")]
        [SerializeField] private RectTransform groupRoot;

        [Header("预制体")]
        [SerializeField] private GameObject npcButtonPrefab;

        [Header("操作")]
        [SerializeField] private Button closeButton;

        private GameManager _game;
        private UnityAction _onCloseClicked;
        private readonly List<Button> _entryButtons = new();

        public override void Initialize()
        {
            SetPanelId(UIPanelId.NpcRecord);
            _game = GameManager.Instance;
            _onCloseClicked = OnCloseClicked;
            closeButton.onClick.AddListener(_onCloseClicked);
        }

        protected override void OnOpen()
        {
            RebuildList();
        }

        public override void Dispose()
        {
            closeButton.onClick.RemoveListener(_onCloseClicked);
            ClearSpawnedEntries();
            base.Dispose();
        }

        private void OnDestroy()
        {
            closeButton.onClick.RemoveListener(_onCloseClicked);
            ClearSpawnedEntries();
        }

        private void OnCloseClicked()
        {
            UiButtonSfx.PlayDefaultClick();
            UIManager.Instance.Close(UIPanelId.NpcRecord);
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
                newEncounterPoolEmpty: true,
                id => ResolveNpcDisplayName(cfg, id),
                id => ResolveNpcAvatarId(cfg, id));

            for (var i = 0; i < vm.Buttons.Count; i++)
            {
                var spec = vm.Buttons[i];
                if (spec.Kind != NpcHubButtonKind.MetNpc)
                    continue;

                var go = Instantiate(npcButtonPrefab, groupRoot);
                var npcButton = go.GetComponent<NpcButton>();
                if (npcButton != null)
                    npcButton.Apply(spec);
                else
                    Debug.LogWarning("[NpcRecordView] npcButtonPrefab 缺少 NpcButton。");

                var btn = npcButton != null && npcButton.Button != null
                    ? npcButton.Button
                    : go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>(true);
                if (btn == null)
                {
                    Debug.LogError("[NpcRecordView] npcButtonPrefab 上未找到 Button。");
                    Destroy(go);
                    continue;
                }

                var label = go.GetComponentInChildren<Text>(true);
                if (label != null)
                    label.text = spec.LabelText;

                _entryButtons.Add(btn);
                var capturedId = spec.NpcId;
                btn.onClick.AddListener(() => OnMetNpcClicked(capturedId));
            }
        }

        private void OnMetNpcClicked(string npcId)
        {
            UiButtonSfx.PlayDefaultClick();
            StartCoroutine(OpenRecordRoutine(npcId));
        }

        private IEnumerator OpenRecordRoutine(string npcId)
        {
            var ui = UIManager.Instance;
            var gm = _game ?? GameManager.Instance;
            var cfg = ConfigManager.Instance;
            if (ui == null || gm == null || cfg == null || string.IsNullOrEmpty(npcId))
                yield break;

            var completed = gm.GetNpcDialogueCompletedCount(npcId);
            var body = NpcDialogueHistoryText.BuildForNpc(npcId, cfg, gm.PlayerState, completed);
            if (string.IsNullOrEmpty(body))
                body = "暂无对话记录";

            yield return ui.OpenAsync(UIPanelId.DialogueRecord);
            if (ui.TryGetView(UIPanelId.DialogueRecord, out RecordView recordView))
                recordView.Apply(body);
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
