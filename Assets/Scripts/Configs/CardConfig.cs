using System;
using System.Collections.Generic;
using KingCardsSpire.Models;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace KingCardsSpire.Configs
{
    /// <summary>
    /// 单张卡牌的静态配置（来自表 / 数据库中的一行）。
    /// </summary>
    [Serializable]
    public sealed class CardConfigEntry
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

    /// <summary>
    /// 卡牌配置表：一个资源内包含全部 <see cref="CardConfigEntry"/>。
    /// </summary>
    [CreateAssetMenu(fileName = "CardConfig", menuName = "KingCardsSpire/Configs/Card Database", order = 0)]
    public sealed class CardConfig : ScriptableObject
    {
        [SerializeField] private List<CardConfigEntry> cards = new();

        public IReadOnlyList<CardConfigEntry> Cards => cards;
    }
}
