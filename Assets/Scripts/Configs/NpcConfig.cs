using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingCardsSpire.Configs
{
    [Serializable]
    public sealed class NpcEntry
    {
        [SerializeField] private string npcId;
        [SerializeField] private string displayName;
        [SerializeField] private string portraitId;
        [SerializeField] private string avatarId;

        public string NpcId => npcId;

        public string DisplayName => displayName;

        public string PortraitId => portraitId;

        public string AvatarId => avatarId;
    }

    [CreateAssetMenu(fileName = "NpcConfig", menuName = "KingCardsSpire/Configs/NPC Database", order = 52)]
    public sealed class NpcConfig : ScriptableObject
    {
        [SerializeField] private List<NpcEntry> npcs = new();

        public IReadOnlyList<NpcEntry> Npcs => npcs;
    }
}
