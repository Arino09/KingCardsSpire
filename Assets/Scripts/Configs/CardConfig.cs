using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace KingCardsSpire.Configs
{
    [CreateAssetMenu(fileName = "CardConfig", menuName = "KingCardsSpire/Configs/Card", order = 0)]
    public class CardConfig : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] float level;
        [SerializeField] CardType type;
        [SerializeField] AssetReferenceSprite icon;
        [TextArea] [SerializeField] string description;
        [SerializeField] bool isUnique;

        public string Id => id;
        public string DisplayName => displayName;
        public float Level => level;
        public CardType Type => type;
        public AssetReferenceSprite Icon => icon;
        public string Description => description;
        public bool IsUnique => isUnique;
    }
}
