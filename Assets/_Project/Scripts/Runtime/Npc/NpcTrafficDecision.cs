using System;
using System.Collections.Generic;

namespace SaksiTerakhir.Npc
{
    /// <summary>Keeps one right-of-way decision until both actors have passed.</summary>
    public sealed class NpcTrafficDecision
    {
        private struct Encounter
        {
            public int YieldingId, FirstPriority, SecondPriority;
            public float LastSeen;
        }

        private readonly Dictionary<long, Encounter> encounters = new Dictionary<long, Encounter>();
        private readonly List<long> expired = new List<long>();

        public int ChooseYielding(int actorId, int actorPriority, int otherId, int otherPriority,
            float now, Func<bool> chooseFirst)
        {
            if (actorId == otherId) throw new ArgumentException("A traffic encounter needs two different actors.");
            int first = Math.Min(actorId, otherId);
            int second = Math.Max(actorId, otherId);
            int firstPriority = Clamp(actorId == first ? actorPriority : otherPriority);
            int secondPriority = Clamp(actorId == first ? otherPriority : actorPriority);
            long key = Pair(first, second);
            if (!encounters.TryGetValue(key, out Encounter encounter)
                || encounter.FirstPriority != firstPriority || encounter.SecondPriority != secondPriority)
            {
                encounter = new Encounter { FirstPriority = firstPriority, SecondPriority = secondPriority };
                encounter.YieldingId = firstPriority < secondPriority ? first
                    : secondPriority < firstPriority ? second
                    : (chooseFirst ?? throw new ArgumentNullException(nameof(chooseFirst)))() ? first : second;
            }
            encounter.LastSeen = now;
            encounters[key] = encounter;
            return encounter.YieldingId;
        }

        public void Retain(int actorId, int otherId, float now)
        {
            long key = Pair(Math.Min(actorId, otherId), Math.Max(actorId, otherId));
            if (!encounters.TryGetValue(key, out Encounter encounter)) return;
            encounter.LastSeen = now;
            encounters[key] = encounter;
        }

        public void Release(int actorId, int otherId)
            => encounters.Remove(Pair(Math.Min(actorId, otherId), Math.Max(actorId, otherId)));

        public void Expire(float now, float unseenSeconds = 3f)
        {
            expired.Clear();
            foreach (var pair in encounters)
                if (now - pair.Value.LastSeen > unseenSeconds) expired.Add(pair.Key);
            foreach (long key in expired) encounters.Remove(key);
        }

        public void Clear() => encounters.Clear();
        private static int Clamp(int value) => Math.Max(0, Math.Min(10, value));
        private static long Pair(int first, int second) => ((long)(uint)first << 32) | (uint)second;
    }
}
