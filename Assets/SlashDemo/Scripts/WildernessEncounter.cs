using UnityEngine;

namespace SlashDemo
{
    public enum WildernessCreature { Bear, Snake, Ape }

    public sealed class WildernessEncounter
    {
        readonly WildernessCreature[] order = { WildernessCreature.Bear, WildernessCreature.Snake, WildernessCreature.Ape };
        int next;

        public WildernessCreature NextCreature => order[next];

        public WildernessEncounter()
        {
            // Shuffle once so windows that cross the end of the rotation also contain all three kinds.
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
        }

        public void SpawnPack(SlashArena arena, TravelMapEnvironment map)
        {
            switch (NextCreature)
            {
                case WildernessCreature.Bear: BearDemon.SpawnPack(arena, map); break;
                case WildernessCreature.Snake: SnakeDemon.SpawnPack(arena, map); break;
                case WildernessCreature.Ape: ApeDemon.SpawnPack(arena, map); break;
            }
            next = (next + 1) % order.Length;
        }
    }
}
