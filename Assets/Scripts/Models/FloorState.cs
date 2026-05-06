using System;

namespace KingCardsSpire.Models
{
    [Serializable]
    public class FloorState
    {
        public int FloorIndex = 1;
        public string BossId;
        public string[] NpcIds = Array.Empty<string>();
        public bool BossDefeated;
    }
}
