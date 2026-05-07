using System.IO;
using KingCardsSpire.Core;
using KingCardsSpire.Core.Events;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Managers
{
    public sealed class PersistenceManager : PersistentMonoSingleton<PersistenceManager>
    {
        const int DefaultSlot = 0;
        const string SaveFileName = "save.json";

        string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");

        string SavePath(int slot = DefaultSlot) => Path.Combine(SaveDirectory, $"slot_{slot}_{SaveFileName}");

        protected override void Awake()
        {
            base.Awake();
            ServiceLocator.Register(this);
        }

        protected override void OnDestroy()
        {
            ServiceLocator.Unregister<PersistenceManager>();
            base.OnDestroy();
        }

        public void InitializeSync()
        {
            if (!Directory.Exists(SaveDirectory))
                Directory.CreateDirectory(SaveDirectory);
        }

        public void Save(SaveData data, int slot = DefaultSlot)
        {
            if (data == null)
                return;
            InitializeSync();
            var json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath(slot), json);
            ServiceLocator.Get<EventManager>()?.Publish(new SaveWrittenEvent());
        }

        public SaveData Load(int slot = DefaultSlot)
        {
            var path = SavePath(slot);
            if (!File.Exists(path))
                return null;
            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<SaveData>(json);
        }

        public bool HasSave(int slot = DefaultSlot) => File.Exists(SavePath(slot));

        public void Delete(int slot = DefaultSlot)
        {
            var path = SavePath(slot);
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
