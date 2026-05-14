using System;
using System.Collections.Generic;

namespace KingCardsSpire.Models
{
    /// <summary>
    /// 牌组仓库交换界面：左右列表数据与移动操作（逻辑由注入的委托完成，例如 GameManager.TryMoveCardBetweenDeckAndStorage）。
    /// </summary>
    public sealed class DeckStorageViewModel
    {
        private readonly Func<IReadOnlyList<Card>> _getActiveCards;
        private readonly Func<IReadOnlyList<Card>> _getStorageCards;
        private readonly Func<bool, int, bool> _tryMoveBetweenDeckAndStorage;

        public DeckStorageViewModel(
            Func<IReadOnlyList<Card>> getActiveCards,
            Func<IReadOnlyList<Card>> getStorageCards,
            Func<bool, int, bool> tryMoveBetweenDeckAndStorage)
        {
            _getActiveCards = getActiveCards ?? throw new ArgumentNullException(nameof(getActiveCards));
            _getStorageCards = getStorageCards ?? throw new ArgumentNullException(nameof(getStorageCards));
            _tryMoveBetweenDeckAndStorage = tryMoveBetweenDeckAndStorage ??
                                           throw new ArgumentNullException(nameof(tryMoveBetweenDeckAndStorage));
        }

        public IReadOnlyList<Card> ActiveCards => _getActiveCards();

        public IReadOnlyList<Card> StorageCards => _getStorageCards();

        /// <summary>将出战区指定下标的牌移入仓库。</summary>
        public bool TryMoveActiveToStorage(int index) =>
            _tryMoveBetweenDeckAndStorage(true, index);

        /// <summary>将仓库指定下标的牌移入出战（出战满则失败）。</summary>
        public bool TryMoveStorageToActive(int index) =>
            _tryMoveBetweenDeckAndStorage(false, index);
    }
}
