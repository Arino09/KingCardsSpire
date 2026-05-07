using UnityEngine;

namespace KingCardsSpire.Configs
{
    /// <summary>
    /// 商店槽位与价格表（文档 §4.1），可由策划配置多条或一条总表。
    /// </summary>
    [CreateAssetMenu(fileName = "ShopConfig", menuName = "KingCardsSpire/Configs/Shop", order = 3)]
    public class ShopConfig : ScriptableObject
    {
        [SerializeField] private string id = "default";
        [SerializeField] private int abilityCardSlots = 1;
        [SerializeField] private int abilityCardPrice = 50;
        [SerializeField] private int functionCardSlots = 2;
        [SerializeField] private int functionCardPrice = 30;
        [SerializeField] private int basicCardSlots = 3;
        [SerializeField] private int basicCardPrice = 10;
        [SerializeField] private int consumableCardSlots = 1;
        [SerializeField] private int consumableCardPrice = 100;

        public string Id => id;
        public int AbilityCardSlots => abilityCardSlots;
        public int AbilityCardPrice => abilityCardPrice;
        public int FunctionCardSlots => functionCardSlots;
        public int FunctionCardPrice => functionCardPrice;
        public int BasicCardSlots => basicCardSlots;
        public int BasicCardPrice => basicCardPrice;
        public int ConsumableCardSlots => consumableCardSlots;
        public int ConsumableCardPrice => consumableCardPrice;
    }
}
