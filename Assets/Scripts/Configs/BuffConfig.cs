using System;
using System.Collections.Generic;
using KingCardsSpire.Models;
using UnityEngine;

namespace KingCardsSpire.Configs
{
    /// <summary>
    /// 单条 Buff 的静态配置（表中的一行）。
    /// </summary>
    [Serializable]
    public sealed class BuffConfigEntry
    {
        [SerializeField] private string id;
        [SerializeField] private BuffId buffId;
        [SerializeField] private string displayName;
        [TextArea] [SerializeField] private string description;

        public string Id => id;

        public BuffId BuffId => buffId;

        public string DisplayName => displayName;

        public string Description => description;

    }

    /// <summary>
    /// Buff 配置表：一个资源内包含全部 <see cref="BuffConfigEntry"/>（与 <see cref="CardConfig"/> 一致）。
    /// </summary>
    [CreateAssetMenu(fileName = "BuffConfig", menuName = "KingCardsSpire/Configs/Buff Database", order = 1)]
    public sealed class BuffConfig : ScriptableObject
    {
        [SerializeField] private List<BuffConfigEntry> buffs = new();

        public IReadOnlyList<BuffConfigEntry> Buffs => buffs;
    }
}
