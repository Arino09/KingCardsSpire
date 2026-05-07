using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Configs
{
    [CreateAssetMenu(fileName = "BuffConfig", menuName = "KingCardsSpire/Configs/Buff", order = 1)]
    public class BuffConfig : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private BuffId buffId;
        [SerializeField] private string displayName;
        [TextArea] [SerializeField] private string description;
        [SerializeField] private float effectParamA;
        [SerializeField] private float effectParamB;

        public string Id => id;
        public BuffId BuffId => buffId;
        public string DisplayName => displayName;
        public string Description => description;
        public float EffectParamA => effectParamA;
        public float EffectParamB => effectParamB;
    }
}
