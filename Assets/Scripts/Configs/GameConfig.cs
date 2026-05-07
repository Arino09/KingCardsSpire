using UnityEngine;

namespace KingCardsSpire.Configs
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "KingCardsSpire/Configs/Game", order = 4)]
    public class GameConfig : ScriptableObject
    {
        [SerializeField] private string id = "default";
        [SerializeField] private int initialGold = 50;
        [SerializeField] private int towerFloors = 7;
        [SerializeField] private int maxDaysPerFloor = 3;
        [SerializeField] private int initialXRayCount = 1;

        public string Id => id;
        public int InitialGold => initialGold;
        public int TowerFloors => towerFloors;
        public int MaxDaysPerFloor => maxDaysPerFloor;
        public int InitialXRayCount => initialXRayCount;
    }
}
