using UnityEngine;

namespace KingCardsSpire.Configs
{
    [CreateAssetMenu(fileName = "TowerConfig", menuName = "KingCardsSpire/Configs/Tower", order = 5)]
    public class TowerConfig : ScriptableObject
    {
        [SerializeField] private string id = "default";
        [SerializeField] private TowerFloorEntry[] floors = new TowerFloorEntry[7];

        public string Id => id;

        /// <summary>下标 0 对应第 1 层。</summary>
        public TowerFloorEntry[] FloorsRaw => floors;

        public bool TryGetFloor(int floorIndex1Based, out TowerFloorEntry entry)
        {
            entry = null;
            if (floorIndex1Based < 1 || floors == null || floorIndex1Based > floors.Length)
                return false;
            entry = floors[floorIndex1Based - 1];
            return entry != null;
        }
    }
}
