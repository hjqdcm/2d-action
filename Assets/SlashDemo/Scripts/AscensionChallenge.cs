using System.Collections.Generic;
using UnityEngine;

namespace SlashDemo
{
    sealed class AscensionChallenge
    {
        readonly Texture2D[] art = new Texture2D[AscensionCultivator.PathCount];
        readonly List<AscensionCultivator.Path> remaining = new List<AscensionCultivator.Path>();
        public SlashEnemy Opponent { get; private set; }
        public int Round { get; private set; }
        public int CompletedRounds { get; private set; }
        public bool HasNext => remaining.Count > 0;

        public AscensionChallenge()
        {
            for (int i = 0; i < art.Length; i++)
            {
                var path = (AscensionCultivator.Path)i;
                art[i] = AscensionCultivator.LoadArt(path);
                remaining.Add(path);
            }
        }

        public void SpawnNext(SlashArena arena, TravelMapEnvironment map)
        {
            int index = Random.Range(0, remaining.Count);
            var path = remaining[index];
            var next = AscensionCultivator.Spawn(arena, map, path, art[(int)path], Mathf.Pow(1.1f, Round));
            RemoveOpponent(arena);
            Opponent = next;
            remaining.RemoveAt(index);
            Round++;
        }

        public bool RecordVictory(SlashEnemy defeated)
        {
            if (defeated != Opponent || defeated.Alive || CompletedRounds == Round) return false;
            CompletedRounds = Round;
            return true;
        }

        public void RemoveOpponent(SlashArena arena)
        {
            if (Opponent == null) return;
            arena.UnregisterEnemy(Opponent);
            Opponent.gameObject.SetActive(false);
            Object.Destroy(Opponent.gameObject);
            Opponent = null;
        }
    }
}
