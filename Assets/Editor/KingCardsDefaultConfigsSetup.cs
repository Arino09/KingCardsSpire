#if UNITY_EDITOR
using System.IO;
using KingCardsSpire.Configs;
using KingCardsSpire.Core;
using KingCardsSpire.Models;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace KingCardsSpire.Editor
{
    /// <summary>
    /// 生成最小占位配置 ScriptableObject 并写入 Addressables「Configs」组与策划标签，消除 InvalidKeyException。
    /// </summary>
    [InitializeOnLoad]
    public static class KingCardsDefaultConfigsSetup
    {
        const string BootstrapDir = "Assets/GameAssets/Configs/Bootstrap";
        const string MarkerAsset = BootstrapDir + "/GameConfig_Default.asset";

        static KingCardsDefaultConfigsSetup()
        {
            EditorApplication.delayCall += EnsureBootstrapConfigsExist;
        }

        static void EnsureBootstrapConfigsExist()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (File.Exists(MarkerAsset))
                return;

            try
            {
                CreateBootstrapConfigsAndRegister();
                AssetDatabase.Refresh();
                Debug.Log("[KingCards] 已生成占位配置并注册 Addressables（Configs / 标签 config_*）。");
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        [MenuItem("KingCardsSpire/Addressables/生成占位配置资源")]
        public static void MenuForceCreate()
        {
            CreateBootstrapConfigsAndRegister();
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();
        }

        static void CreateBootstrapConfigsAndRegister()
        {
            EnsureDirectory(BootstrapDir);

            SaveGameConfig();
            SaveCardConfig("Card_King.asset", WellKnownCardIds.King, "国王", 3f);
            SaveCardConfig("Card_Commoner.asset", WellKnownCardIds.Commoner, "平民", 1f);
            SaveBuffConfig();
            SaveWeatherConfig();
            SaveShopConfig();

            AssetDatabase.SaveAssets();
        }

        static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Replace("Assets/", "").Split('/');
            var cur = "Assets";
            foreach (var p in parts)
            {
                if (string.IsNullOrEmpty(p))
                    continue;
                var next = cur + "/" + p;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, p);
                cur = next;
            }
        }

        static void SaveGameConfig()
        {
            var path = $"{BootstrapDir}/GameConfig_Default.asset";
            var asset = ScriptableObject.CreateInstance<GameConfig>();
            ApplySerialized(asset, so =>
            {
                so.FindProperty("id").stringValue = "default";
                so.FindProperty("initialGold").intValue = 50;
                so.FindProperty("towerFloors").intValue = 7;
                so.FindProperty("maxDaysPerFloor").intValue = 3;
                so.FindProperty("initialXRayCount").intValue = 1;
            });
            AssetDatabase.CreateAsset(asset, path);
            RegisterConfigsEntry(path, AddressableLabels.ConfigGame);
        }

        static void SaveCardConfig(string fileName, string id, string displayName, float level)
        {
            var path = $"{BootstrapDir}/{fileName}";
            var asset = ScriptableObject.CreateInstance<CardConfig>();
            ApplySerialized(asset, so =>
            {
                so.FindProperty("id").stringValue = id;
                so.FindProperty("displayName").stringValue = displayName;
                so.FindProperty("level").floatValue = level;
                so.FindProperty("type").enumValueIndex = (int)CardType.Basic;
                so.FindProperty("description").stringValue = "";
                so.FindProperty("isUnique").boolValue =
                    id == WellKnownCardIds.King || id == WellKnownCardIds.Commoner;
            });
            AssetDatabase.CreateAsset(asset, path);
            RegisterConfigsEntry(path, AddressableLabels.ConfigCard);
        }

        static void SaveBuffConfig()
        {
            var path = $"{BootstrapDir}/Buff_Placeholder.asset";
            var asset = ScriptableObject.CreateInstance<BuffConfig>();
            ApplySerialized(asset, so =>
            {
                so.FindProperty("id").stringValue = "buff_placeholder";
                so.FindProperty("buffId").enumValueIndex = (int)BuffId.Socialite;
                so.FindProperty("displayName").stringValue = "占位 Buff";
                so.FindProperty("description").stringValue = "";
                so.FindProperty("effectParamA").floatValue = 0f;
                so.FindProperty("effectParamB").floatValue = 0f;
            });
            AssetDatabase.CreateAsset(asset, path);
            RegisterConfigsEntry(path, AddressableLabels.ConfigBuff);
        }

        static void SaveWeatherConfig()
        {
            var path = $"{BootstrapDir}/Weather_WarmWind.asset";
            var asset = ScriptableObject.CreateInstance<WeatherConfig>();
            ApplySerialized(asset, so =>
            {
                so.FindProperty("id").stringValue = "weather_warm_wind";
                so.FindProperty("weatherType").enumValueIndex = (int)WeatherType.WarmWind;
                so.FindProperty("displayName").stringValue = "暖风";
                so.FindProperty("description").stringValue = "";
                so.FindProperty("modifier").floatValue = 0f;
            });
            AssetDatabase.CreateAsset(asset, path);
            RegisterConfigsEntry(path, AddressableLabels.ConfigWeather);
        }

        static void SaveShopConfig()
        {
            var path = $"{BootstrapDir}/Shop_Default.asset";
            var asset = ScriptableObject.CreateInstance<ShopConfig>();
            ApplySerialized(asset, so =>
            {
                so.FindProperty("id").stringValue = "default";
            });
            AssetDatabase.CreateAsset(asset, path);
            RegisterConfigsEntry(path, AddressableLabels.ConfigShop);
        }

        static void ApplySerialized(ScriptableObject asset, System.Action<SerializedObject> fill)
        {
            var so = new SerializedObject(asset);
            fill(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void RegisterConfigsEntry(string assetPath, string label)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return;

            var group = settings.FindGroup("Configs");
            if (group == null)
            {
                Debug.LogWarning("[KingCards] Addressables 中缺少 Configs 分组，请在 Window → Asset Management → Addressables 中创建。");
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                return;

            var entry = settings.FindAssetEntry(guid);
            if (entry == null)
                entry = settings.CreateOrMoveEntry(guid, group, false, false);

            if (entry == null)
                return;

            entry.SetLabel(label, true, true, true);
            EditorUtility.SetDirty(settings);
        }
    }
}
#endif
