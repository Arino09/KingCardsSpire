using System;
using System.Collections.Generic;
using System.IO;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Managers
{
    /// <summary>
    /// 卡牌图鉴解锁进度：与槽位存档分离的全局 JSON，新游戏删档不清理。
    /// </summary>
    public static class CardAlbumProgressStore
    {
        private const string FileName = "card_album_unlocks.json";

        [Serializable]
        private sealed class CardAlbumProgressJson
        {
            public string[] Ids;
        }

        private static string FilePath =>
            Path.Combine(Application.persistentDataPath, "Saves", FileName);

        public static void EnsureDirectory()
        {
            var dir = Path.Combine(Application.persistentDataPath, "Saves");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        /// <summary>从磁盘加载已解锁卡 Id（无文件时返回空集）。</summary>
        public static HashSet<string> LoadDiscoveredIds()
        {
            EnsureDirectory();
            var path = FilePath;
            if (!File.Exists(path))
                return new HashSet<string>(StringComparer.Ordinal);

            try
            {
                var json = File.ReadAllText(path);
                var dto = JsonUtility.FromJson<CardAlbumProgressJson>(json);
                if (dto?.Ids == null || dto.Ids.Length == 0)
                    return new HashSet<string>(StringComparer.Ordinal);

                var set = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < dto.Ids.Length; i++)
                {
                    var id = dto.Ids[i];
                    if (!string.IsNullOrEmpty(id))
                        set.Add(id);
                }

                return set;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CardAlbumProgressStore] 读取失败，将视为空进度: {e.Message}");
                return new HashSet<string>(StringComparer.Ordinal);
            }
        }

        private static void Save(HashSet<string> ids)
        {
            EnsureDirectory();
            var list = new List<string>(ids);
            list.Sort(StringComparer.Ordinal);
            var dto = new CardAlbumProgressJson { Ids = list.ToArray() };
            File.WriteAllText(FilePath, JsonUtility.ToJson(dto, true));
        }

        /// <summary>登记一张已发现的卡；若为新 Id 则写盘并返回 true。</summary>
        public static bool AddDiscoveredCardId(string cardId)
        {
            if (string.IsNullOrEmpty(cardId))
                return false;

            var set = LoadDiscoveredIds();
            if (!set.Add(cardId))
                return false;

            Save(set);
            return true;
        }

        /// <summary>将多组持有卡中的 Id 合并进全局进度（去重后若有变更则写盘）。</summary>
        public static void RegisterDiscoveredFromPlayerCollections(PlayerData player)
        {
            if (player == null)
                return;

            var set = LoadDiscoveredIds();
            var before = set.Count;
            CollectFromArray(player.HandCards, set);
            CollectFromArray(player.StoredCards, set);
            CollectFromArray(player.DiscardPile, set);
            CollectFromArray(player.OwnedCards, set);
            if (set.Count > before)
                Save(set);
        }

        private static void CollectFromArray(Card[] cards, HashSet<string> set)
        {
            if (cards == null)
                return;
            for (var i = 0; i < cards.Length; i++)
            {
                var c = cards[i];
                if (c != null && !string.IsNullOrEmpty(c.Id))
                    set.Add(c.Id);
            }
        }

        /// <summary>若全局进度为空且默认槽位有存档，将存档内持有卡合并进图鉴（一次性补老档）。</summary>
        public static void TryMigrateIfEmpty(PersistenceManager persistence)
        {
            if (persistence == null)
                return;
            var set = LoadDiscoveredIds();
            if (set.Count > 0)
                return;
            if (!persistence.HasSave())
                return;
            var save = persistence.Load();
            if (save?.Player == null)
                return;
            RegisterDiscoveredFromPlayerCollections(save.Player);
        }
    }
}
