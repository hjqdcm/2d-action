using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace SlashDemo
{
    sealed class MarketStall
    {
        const int OfferCount = 4;
        (AlchemyEntry Item, int Price, bool Sold)[] offers = System.Array.Empty<(AlchemyEntry, int, bool)>();

        internal bool SellsTreasures { get; }
        internal long RefreshPrice { get; private set; }
        internal IReadOnlyList<(AlchemyEntry Item, int Price, bool Sold)> Offers => offers;

        internal MarketStall(bool sellsTreasures) => SellsTreasures = sellsTreasures;

        internal void BeginVisit()
        {
            offers = RollOffers();
            RefreshPrice = SellsTreasures ? 50 : 30;
        }

        internal bool TryBuy(int index, GameResources resources, out AlchemyEntry item)
        {
            item = default;
            if (index < 0 || index >= offers.Length || offers[index].Sold
                || !resources.TryChangeSpiritStones(-offers[index].Price)) return false;

            item = offers[index].Item;
            offers[index].Sold = true;
            return true;
        }

        internal bool TryRefresh(GameResources resources)
        {
            if (RefreshPrice > resources.SpiritStones) return false;
            var newOffers = RollOffers();
            if (!resources.TryChangeSpiritStones(-(int)RefreshPrice)) return false;

            offers = newOffers;
            RefreshPrice *= 2;
            return true;
        }

        (AlchemyEntry Item, int Price, bool Sold)[] RollOffers()
        {
            var entries = AlchemyLoot.RollOptions(SellsTreasures ? TreasureCatalog.All : PillCatalog.All, OfferCount);
            var stock = new (AlchemyEntry, int, bool)[entries.Length];
            int priceFactor = SellsTreasures ? 2 : 1;
            for (int i = 0; i < entries.Length; i++)
            {
                var item = entries[i];
                int basePrice = 50 * ((int)item.Quality + 1);
                int price = Random.Range((basePrice - 10) * priceFactor, (basePrice + 10) * priceFactor + 1);
                stock[i] = (item, price, false);
            }
            return stock;
        }
    }
}
