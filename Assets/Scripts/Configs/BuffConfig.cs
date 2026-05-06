using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Configs
{
    [CreateAssetMenu(fileName = "BuffConfig", menuName = "KingCardsSpire/Configs/Buff", order = 1)]
    public class BuffConfig : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] BuffId buffId;
        [SerializeField] string displayName;
        [TextArea] [SerializeField] string description;
        [SerializeField] float effectParamA;
        [SerializeField] float effectParamB;

        public string Id => id;
        public BuffId BuffId => buffId;
        public string DisplayName => displayName;
        public string Description => description;
        public float EffectParamA => effectParamA;
        public float EffectParamB => effectParamB;
    }
}
