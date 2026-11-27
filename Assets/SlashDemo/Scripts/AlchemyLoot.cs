using System;
using System.Collections.Generic;
using Random = UnityEngine.Random;

namespace SlashDemo
{
    static class AlchemyLoot
    {
        const int OptionCount = 3;

        public static AlchemyEntry[] RollOptions(IReadOnlyList<AlchemyEntry> catalog)
        {
            var options = new AlchemyEntry[OptionCount];
            var pool = new List<AlchemyEntry>();
            for (int i = 0; i < options.Length; i++)
            {
                var quality = RollQuality();
                pool.Clear();
                foreach (var item in catalog)
                    if (item.Quality == quality && Array.IndexOf(options, item, 0, i) < 0) pool.Add(item);
                options[i] = pool[Random.Range(0, pool.Count)];
            }
            return options;
        }

        static AlchemyQuality RollQuality()
        {
            int roll = Random.Range(0, 100);
            return roll < 60 ? AlchemyQuality.White
                : roll < 90 ? AlchemyQuality.Blue
                : roll < 99 ? AlchemyQuality.Purple
                : AlchemyQuality.Orange;
        }
    }
}
