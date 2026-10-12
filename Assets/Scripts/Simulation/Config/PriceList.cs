using System;
using System.Collections.Generic;

namespace BabyChampion.Simulation
{
    public enum SupplyType { Diapers, Wipes, Formula, BabyFood, AdultFood }

    /// <summary>One orderable pack of a supply.</summary>
    [Serializable]
    public class SupplyPrice
    {
        public SupplyType item;
        public int packSize;
        public int price;
    }

    /// <summary>Supply prices and delivery rules (design Sections 9 and 10).</summary>
    [Serializable]
    public class PriceList
    {
        public List<SupplyPrice> items = new List<SupplyPrice>();
        public double deliveryMinutes = 120;
        public double expressDeliveryMinutes = 30;
        public int expressFee = 8;

        public SupplyPrice For(SupplyType item)
        {
            foreach (var price in items)
                if (price.item == item) return price;
            throw new ArgumentException($"No price for {item}");
        }

        public int Cost(SupplyType item, int packs, bool express) => For(item).price * packs + (express ? expressFee : 0);

        public static PriceList CreateDefault() => new PriceList
        {
            items = new List<SupplyPrice>
            {
                new SupplyPrice { item = SupplyType.Diapers, packSize = 24, price = 12 },
                new SupplyPrice { item = SupplyType.Wipes, packSize = 40, price = 4 },
                new SupplyPrice { item = SupplyType.Formula, packSize = 12, price = 15 },
                new SupplyPrice { item = SupplyType.BabyFood, packSize = 12, price = 10 },
                new SupplyPrice { item = SupplyType.AdultFood, packSize = 6, price = 18 },
            },
        };
    }
}
