#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace KingCardsSpire.Editor
{
    /// <summary>
    /// 首次打开工程时确保 Addressables 设置与四个分组存在（Configs / UI / Audio / Dialogues）。
    /// </summary>
    [InitializeOnLoad]
    public static class KingCardsAddressablesBootstrap
    {
        static KingCardsAddressablesBootstrap()
        {
            EditorApplication.delayCall += EnsureAddressablesSetup;
        }

        private static void EnsureAddressablesSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
                return;

            var dirty = false;
            dirty |= EnsureGroup(settings, "Configs", BundledAssetGroupSchema.BundlePackingMode.PackTogether);
            dirty |= EnsureGroup(settings, "UI", BundledAssetGroupSchema.BundlePackingMode.PackSeparately);
            dirty |= EnsureGroup(settings, "Audio", BundledAssetGroupSchema.BundlePackingMode.PackTogetherByLabel);
            dirty |= EnsureGroup(settings, "Dialogues", BundledAssetGroupSchema.BundlePackingMode.PackTogether);

            if (dirty)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        private static bool EnsureGroup(AddressableAssetSettings settings, string groupName,
            BundledAssetGroupSchema.BundlePackingMode packingMode)
        {
            var group = settings.FindGroup(groupName);
            var created = false;
            if (group == null)
            {
                group = settings.CreateGroup(groupName, false, false, true, null,
                    typeof(ContentUpdateGroupSchema), typeof(BundledAssetGroupSchema));
                created = true;
            }

            var schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema == null)
                return created;

            var changed = created;
            if (created)
            {
                schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                changed = true;
            }

            if (schema.BundleMode != packingMode)
            {
                schema.BundleMode = packingMode;
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(group);

            return changed;
        }
    }
}
#endif
