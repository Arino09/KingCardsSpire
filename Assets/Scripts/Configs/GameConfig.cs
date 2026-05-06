using UnityEngine;

namespace KingCardsSpire.Configs
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "KingCardsSpire/Configs/Game", order = 4)]
    public class GameConfig : ScriptableObject
    {
        [SerializeField] string id = "default";
        [SerializeField] int initialGold = 50;
        [SerializeField] int towerFloors = 7;
        [SerializeField] int maxDaysPerFloor = 3;
        [SerializeField] int initialXRayCount = 1;

        public string Id => id;
        public int InitialGold => initialGold;
        public int TowerFloors => towerFloors;
        public int MaxDaysPerFloor => maxDaysPerFloor;
        public int InitialXRayCount => initialXRayCount;
    }
}
