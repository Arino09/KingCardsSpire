using System.Text;
using KingCardsSpire.Controllers;
using KingCardsSpire.Core;
using KingCardsSpire.Core.Events;
using KingCardsSpire.Managers;
using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.UI;

namespace KingCardsSpire.Views.UI
{
    /// <summary>
    /// 最小可玩战斗面板：展示手牌、透视敌牌、回合与日志；依赖 Addressables 地址 UI/Panel_Battle。
    /// </summary>
    public sealed class BattlePanelView : BaseView
    {
        private Text _headerText;
        private Text _enemyVisibleText;
        private Text _logText;
        private RectTransform _handRow;
        private BattleController _battle;
        private EventManager _events;
        private readonly StringBuilder _logBuilder = new();

        public override void Initialize()
        {
            SetPanelId(UIPanelId.Battle);
            EnsureUiBuilt();
            _battle = ServiceLocator.Get<BattleController>();
            _events = EventManager.Instance;
        }

        public override void Dispose()
        {
            if (_events != null)
            {
                _events.Unsubscribe<BattleStateChangedEvent>(OnBattleStateChanged);
                _events.Unsubscribe<BattleRoundResolvedEvent>(OnRoundResolved);
                _events.Unsubscribe<BattleEndedEvent>(OnBattleEnded);
            }

            base.Dispose();
        }

        protected override void OnOpen()
        {
            if (_events != null)
            {
                _events.Subscribe<BattleStateChangedEvent>(OnBattleStateChanged);
                _events.Subscribe<BattleRoundResolvedEvent>(OnRoundResolved);
                _events.Subscribe<BattleEndedEvent>(OnBattleEnded);
            }

            _battle ??= ServiceLocator.Get<BattleController>();
            _battle?.RequestStartBattle();
            RefreshAll();
        }

        private void OnBattleStateChanged(BattleStateChangedEvent _) => RefreshAll();

        private void OnRoundResolved(BattleRoundResolvedEvent e)
        {
            _logBuilder.AppendLine(e.Summary);
            if (_logText != null)
                _logText.text = _logBuilder.ToString();
            RefreshAll();
        }

        private void OnBattleEnded(BattleEndedEvent e)
        {
            _logBuilder.AppendLine(
                $"战斗结束 己方{(e.PlayerVictory ? "胜" : "败")} {e.Reason}{(e.IsBossBattle ? " [BOSS]" : "")}");
            if (_logText != null)
                _logText.text = _logBuilder.ToString();
            RefreshAll();
        }

        private void RefreshAll()
        {
            var bm = BattleManager.Instance;
            var state = bm != null ? bm.CurrentBattle : null;
            if (_headerText != null && state != null)
            {
                var cap = state.NoRoundLimit ? "无限制" : state.MaxRound.ToString();
                _headerText.text =
                    $"回合 {state.Round} / {cap}  天气 {state.BattleWeather}  己方 {state.PlayerHand?.Length ?? 0} 张  敌 {state.EnemyHand?.Length ?? 0} 张";
            }

            if (_enemyVisibleText != null && state?.EnemyVisible != null)
            {
                var sb = new StringBuilder("透视敌牌: ");
                foreach (var c in state.EnemyVisible)
                    sb.Append(c.Name).Append(' ');
                _enemyVisibleText.text = sb.ToString();
            }

            RebuildHandButtons(state?.PlayerHand);
        }

        private void RebuildHandButtons(Card[] hand)
        {
            if (_handRow == null)
                return;

            for (var i = _handRow.childCount - 1; i >= 0; i--)
                Destroy(_handRow.GetChild(i).gameObject);

            if (hand == null || hand.Length == 0 || _battle == null)
                return;

            for (var i = 0; i < hand.Length; i++)
            {
                var idx = i;
                var card = hand[i];
                var btnGo = new GameObject($"Card_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(_handRow, false);

                var rect = btnGo.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(140f, 48f);

                var img = btnGo.GetComponent<Image>();
                img.color = new Color(0.25f, 0.35f, 0.55f, 1f);

                var txtGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                txtGo.transform.SetParent(btnGo.transform, false);
                var tr = txtGo.GetComponent<RectTransform>();
                tr.anchorMin = Vector2.zero;
                tr.anchorMax = Vector2.one;
                tr.offsetMin = Vector2.zero;
                tr.offsetMax = Vector2.zero;
                var label = txtGo.GetComponent<Text>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = 16;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.white;
                label.text = $"{card.Name} ({card.Level})";

                var btn = btnGo.GetComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    if (!_battle.IsBattleActive)
                        return;
                    if (_battle.TryPlayCard(idx, out var err))
                        RefreshAll();
                    else
                        Debug.LogWarning($"[BattlePanel] {err}");
                });
            }
        }

        private void EnsureUiBuilt()
        {
            if (_handRow != null)
                return;

            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (gameObject.GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null)
                gameObject.AddComponent<RectTransform>();

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            var bg = panel.GetComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
            bg.raycastTarget = false;

            _headerText = CreateText(panel.transform, "Header", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -24f), new Vector2(-40f, -80f), 22);

            _enemyVisibleText = CreateText(panel.transform, "EnemyVis", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -88f), new Vector2(-40f, -140f), 18);

            _logText = CreateText(panel.transform, "Log", new Vector2(0f, 0f), new Vector2(1f, 0.55f),
                new Vector2(20f, 20f), new Vector2(-20f, -20f), 14);

            var handArea = new GameObject("HandRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            handArea.transform.SetParent(panel.transform, false);
            var hr = handArea.GetComponent<RectTransform>();
            hr.anchorMin = new Vector2(0f, 0f);
            hr.anchorMax = new Vector2(1f, 0.22f);
            hr.offsetMin = new Vector2(20f, 20f);
            hr.offsetMax = new Vector2(-20f, -20f);
            var hlg = handArea.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            _handRow = hr;

            CreateFooterButtons(panel.transform);
        }

        private static Text CreateText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin,
            Vector2 offsetMax, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.color = Color.white;
            t.alignment = TextAnchor.UpperLeft;
            return t;
        }

        private void CreateFooterButtons(Transform panel)
        {
            var row = new GameObject("Footer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(panel, false);
            var rt = row.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 24f);
            rt.sizeDelta = new Vector2(600f, 44f);
            var lg = row.GetComponent<HorizontalLayoutGroup>();
            lg.spacing = 12f;
            lg.childAlignment = TextAnchor.MiddleCenter;

            MakeFooterButton(row.transform, "关闭", () =>
            {
                UIManager.Instance?.Close(UIPanelId.Battle);
            });

            MakeFooterButton(row.transform, "再来一局", () =>
            {
                _logBuilder.Clear();
                _battle?.RequestStartBattle();
                RefreshAll();
            });
        }

        private static void MakeFooterButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(160f, 40f);
            go.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.5f, 1f);
            var txtGo = new GameObject("T", typeof(RectTransform), typeof(Text));
            txtGo.transform.SetParent(go.transform, false);
            var tr = txtGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            var te = txtGo.GetComponent<Text>();
            te.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            te.fontSize = 18;
            te.alignment = TextAnchor.MiddleCenter;
            te.color = Color.white;
            te.text = label;
            go.GetComponent<Button>().onClick.AddListener(onClick);
        }
    }
}
