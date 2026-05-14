using System;
using System.Collections.Generic;

namespace KingCardsSpire.Models
{
    /// <summary>
    /// 卡牌列表弹层 / 面板的展示数据：标题 + 卡牌序列（如卡组、双方弃牌堆等）。
    /// </summary>
    public sealed class CardListViewModel
    {
        public CardListViewModel(string title, IReadOnlyList<Card> cards, bool faceDown = false,
            int maxSelectable = 0, Action<IReadOnlyList<Card>> onConfirmSelection = null,
            Action onCancelSelection = null, string selectionHint = null)
        {
            Title = title ?? string.Empty;
            Cards = cards ?? Array.Empty<Card>();
            FaceDown = faceDown;
            MaxSelectable = Math.Max(0, Math.Min(maxSelectable, 64));
            OnConfirmSelection = onConfirmSelection;
            OnCancelSelection = onCancelSelection;
            SelectionHint = selectionHint ?? string.Empty;
        }

        public string Title { get; }

        public IReadOnlyList<Card> Cards { get; }

        /// <summary>true 时列表内卡牌均以卡背展示（列表仍为占位格子）。</summary>
        public bool FaceDown { get; }

        /// <summary>大于 0 时进入多选模式，需点选恰好 <see cref="MaxSelectable"/> 张后确认。</summary>
        public int MaxSelectable { get; }

        public Action<IReadOnlyList<Card>> OnConfirmSelection { get; }

        public Action OnCancelSelection { get; }

        public string SelectionHint { get; }
    }
}
