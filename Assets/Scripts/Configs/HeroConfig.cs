using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingCardsSpire.Configs
{
    [Serializable]
    public sealed class HeroEntry
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string portraitId;
        [SerializeField] private string avatarId;

        public string Id => id;

        public string DisplayName => displayName;

        public string PortraitId => portraitId;

        public string AvatarId => avatarId;
    }

    [CreateAssetMenu(fileName = "HeroConfig", menuName = "KingCardsSpire/Configs/Hero Database", order = 51)]
    public sealed class HeroConfig : ScriptableObject
    {
        [SerializeField] private List<HeroEntry> heroes = new();

        public IReadOnlyList<HeroEntry> Heroes => heroes;
    }
}
