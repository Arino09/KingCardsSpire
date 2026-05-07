using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace KingCardsSpire.Configs
{
    [CreateAssetMenu(fileName = "CardConfig", menuName = "KingCardsSpire/Configs/Card", order = 0)]
    public class CardConfig : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private float level;
        [SerializeField] private CardType type;
        [SerializeField] private AssetReferenceSprite icon;
        [TextArea] [SerializeField] private string description;
        [SerializeField] private bool isUnique;

        public string Id => id;
        public string DisplayName => displayName;
        public float Level => level;
        public CardType Type => type;
        public AssetReferenceSprite Icon => icon;
        public string Description => description;
        public bool IsUnique => isUnique;
    }
}
