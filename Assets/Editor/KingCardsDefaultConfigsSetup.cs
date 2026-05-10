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
        private const string BootstrapDir = "Assets/GameConfigs/Bootstrap";
        private const string MarkerAsset = BootstrapDir + "/GameConfig_Default.asset";
        private const string TowerMarkerAsset = BootstrapDir + "/Tower_Default.asset";

        static KingCardsDefaultConfigsSetup()
        {
            EditorApplication.delayCall += EnsureBootstrapConfigsExist;
        }

        private static void EnsureBootstrapConfigsExist()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            try
            {
                if (!File.Exists(MarkerAsset))
                {
                    CreateBootstrapConfigsAndRegister();
                    AssetDatabase.Refresh();
                    Debug.Log("[KingCards] 已生成占位配置并注册 Addressables（Configs / 标签 config_*）。");
                }
                else if (!File.Exists(TowerMarkerAsset))
                {
                    EnsureDirectory(BootstrapDir);
                    SaveTowerConfig();
                    AssetDatabase.Refresh();
                    Debug.Log("[KingCards] 已补全 Tower_Default 占位配置。");
                }
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

        private static void CreateBootstrapConfigsAndRegister()
        {
            EnsureDirectory(BootstrapDir);

            SaveGameConfig();
            SaveCardDatabase();
            SaveBuffConfig();
            SaveWeatherConfig();
            SaveShopConfig();
            SaveTowerConfig();

            AssetDatabase.SaveAssets();
        }

        private static void EnsureDirectory(string path)
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

        private static void SaveGameConfig()
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

        private static void SaveCardDatabase()
        {
            var path = $"{BootstrapDir}/Cards_Default.asset";
            var asset = ScriptableObject.CreateInstance<CardConfig>();
            ApplySerialized(asset, so =>
            {
                var cardsProp = so.FindProperty("cards");
                cardsProp.arraySize = 2;
                FillCardEntry(cardsProp.GetArrayElementAtIndex(0), WellKnownCardIds.King, "国王", 3f);
                FillCardEntry(cardsProp.GetArrayElementAtIndex(1), WellKnownCardIds.Commoner, "平民", 1f);
            });
            AssetDatabase.CreateAsset(asset, path);
            RegisterConfigsEntry(path, AddressableLabels.ConfigCard);
        }

        private static void FillCardEntry(SerializedProperty element, string id, string displayName, float level)
        {
            element.FindPropertyRelative("id").stringValue = id;
            element.FindPropertyRelative("displayName").stringValue = displayName;
            element.FindPropertyRelative("level").floatValue = level;
            element.FindPropertyRelative("type").enumValueIndex = (int)CardType.Basic;
            element.FindPropertyRelative("description").stringValue = "";
            element.FindPropertyRelative("isUnique").boolValue =
                id == WellKnownCardIds.King || id == WellKnownCardIds.Commoner;
        }

        private static void SaveBuffConfig()
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

        private static void SaveWeatherConfig()
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

        private static void SaveShopConfig()
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

        private static void SaveTowerConfig()
        {
            var path = $"{BootstrapDir}/Tower_Default.asset";
            var asset = ScriptableObject.CreateInstance<TowerConfig>();
            ApplySerialized(asset, so =>
            {
                so.FindProperty("id").stringValue = "default";
                var floorsProp = so.FindProperty("floors");
                floorsProp.arraySize = 7;
                for (var i = 0; i < 7; i++)
                {
                    var floorEl = floorsProp.GetArrayElementAtIndex(i);
                    floorEl.FindPropertyRelative("bossId").stringValue = $"floor_{i + 1}_boss";
                    var deck = floorEl.FindPropertyRelative("enemyDeckCardIds");
                    deck.arraySize = 2;
                    deck.GetArrayElementAtIndex(0).stringValue = WellKnownCardIds.King;
                    deck.GetArrayElementAtIndex(1).stringValue = WellKnownCardIds.Commoner;
                    var npc = floorEl.FindPropertyRelative("npcIds");
                    npc.arraySize = 1;
                    npc.GetArrayElementAtIndex(0).stringValue = $"npc_floor_{i + 1}";
                    var pool = floorEl.FindPropertyRelative("rewardCardPoolIds");
                    pool.arraySize = 2;
                    pool.GetArrayElementAtIndex(0).stringValue = WellKnownCardIds.King;
                    pool.GetArrayElementAtIndex(1).stringValue = WellKnownCardIds.Commoner;
                    floorEl.FindPropertyRelative("goldBonusPerSpareDay").intValue = 5;
                }
            });
            AssetDatabase.CreateAsset(asset, path);
            RegisterConfigsEntry(path, AddressableLabels.ConfigTower);
        }

        private static void ApplySerialized(ScriptableObject asset, System.Action<SerializedObject> fill)
        {
            var so = new SerializedObject(asset);
            fill(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterConfigsEntry(string assetPath, string label)
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
