using System;
using System.Collections.Generic;

namespace KingCardsSpire.Models
{
    /// <summary>
    /// 卡牌列表弹层 / 面板的展示数据：标题 + 卡牌序列（如卡组、双方弃牌堆等）。
    /// </summary>
    public sealed class CardListViewModel
    {
        public CardListViewModel(string title, IReadOnlyList<Card> cards, bool faceDown = false)
        {
            Title = title ?? string.Empty;
            Cards = cards ?? Array.Empty<Card>();
            FaceDown = faceDown;
        }

        public string Title { get; }

        public IReadOnlyList<Card> Cards { get; }

        /// <summary>true 时列表内卡牌均以卡背展示（列表仍为占位格子）。</summary>
        public bool FaceDown { get; }
    }
}
