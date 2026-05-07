#if UNITY_EDITOR
using System.IO;
using KingCardsSpire.Views.UI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace KingCardsSpire.Editor
{
    /// <summary>
    /// 确保存在战斗 UI 预制体并注册 Addressables 键 UI/Panel_Battle、标签 ui_panel。
    /// </summary>
    [InitializeOnLoad]
    public static class KingCardsBattlePanelAddressablesSetup
    {
        private const string PrefabPath = "Assets/GameAssets/UI/Panel_Battle.prefab";
        private const string Address = "UI/Panel_Battle";
        private const string Label = "ui_panel";

        static KingCardsBattlePanelAddressablesSetup()
        {
            EditorApplication.delayCall += RunOnce;
        }

        private static void RunOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var existed = File.Exists(PrefabPath);
            if (!EnsurePrefab())
                return;

            if (!existed)
                AssetDatabase.Refresh();

            RegisterIfNeeded();
        }

        private static bool EnsurePrefab()
        {
            if (File.Exists(PrefabPath))
                return true;

            var dir = Path.GetDirectoryName(PrefabPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var go = new GameObject("Panel_Battle", typeof(RectTransform), typeof(BattlePanelView));
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            Debug.Log($"[KingCards] 已创建战斗 UI 预制体: {PrefabPath}");
            return true;
        }

        private static void RegisterIfNeeded()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return;

            var guid = AssetDatabase.AssetPathToGUID(PrefabPath);
            if (string.IsNullOrEmpty(guid))
                return;

            var group = settings.FindGroup("UI");
            if (group == null)
            {
                Debug.LogWarning("[KingCards] Addressables 中未找到 UI 分组，已跳过注册。");
                return;
            }

            var entry = settings.FindAssetEntry(guid);
            if (entry == null)
                entry = settings.CreateOrMoveEntry(guid, group, false, false);

            if (entry == null)
            {
                Debug.LogWarning("[KingCards] 无法为 Panel_Battle 创建 Addressable 条目。");
                return;
            }

            if (entry.address != Address)
            {
                entry.address = Address;
                EditorUtility.SetDirty(settings);
            }

            if (!entry.labels.Contains(Label))
            {
                entry.SetLabel(Label, true, true, true);
                EditorUtility.SetDirty(settings);
            }
        }

        [MenuItem("KingCardsSpire/Addressables/注册战斗面板")]
        public static void MenuRegisterBattlePanel()
        {
            EnsurePrefab();
            RegisterIfNeeded();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("KingCardsSpire/Play Mode/打开战斗面板")]
        public static void MenuOpenBattlePanelInPlayMode()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[KingCards] 请在运行游戏后通过本菜单打开战斗面板。");
                return;
            }

            var ui = Object.FindObjectOfType<KingCardsSpire.Managers.UIManager>();
            if (ui == null)
            {
                Debug.LogError("[KingCards] 未找到 UIManager，请从含 GameRoot 的场景启动。");
                return;
            }

            ui.StartCoroutine(ui.OpenAsync(KingCardsSpire.Models.UIPanelId.Battle));
        }
    }
}
#endif
