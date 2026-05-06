using UnityEngine;

namespace KingCardsSpire.Configs
{
    /// <summary>
    /// 商店槽位与价格表（文档 §4.1），可由策划配置多条或一条总表。
    /// </summary>
    [CreateAssetMenu(fileName = "ShopConfig", menuName = "KingCardsSpire/Configs/Shop", order = 3)]
    public class ShopConfig : ScriptableObject
    {
        [SerializeField] string id = "default";
        [SerializeField] int abilityCardSlots = 1;
        [SerializeField] int abilityCardPrice = 50;
        [SerializeField] int functionCardSlots = 2;
        [SerializeField] int functionCardPrice = 30;
        [SerializeField] int basicCardSlots = 3;
        [SerializeField] int basicCardPrice = 10;
        [SerializeField] int consumableCardSlots = 1;
        [SerializeField] int consumableCardPrice = 100;

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
