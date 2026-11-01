using UnityEngine;

namespace SlashDemo
{
    public enum WildernessCreature { Bear, Snake, Ape }

    public static class WildernessEncounter
    {
        public static WildernessCreature Choose(float roll)
            => roll < 1f / 3 ? WildernessCreature.Bear : roll < 2f / 3 ? WildernessCreature.Snake : WildernessCreature.Ape;

        public static void SpawnPack(SlashArena arena, TravelMapEnvironment map)
        {
            switch (Choose(Random.value))
            {
                case WildernessCreature.Bear: BearDemon.SpawnPack(arena, map); break;
                case WildernessCreature.Snake: SnakeDemon.SpawnPack(arena, map); break;
                case WildernessCreature.Ape: ApeDemon.SpawnPack(arena, map); break;
            }
        }
    }
}
