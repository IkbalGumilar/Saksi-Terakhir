using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaksiTerakhir.Npc
{
    /// <summary>Run-scoped stable IDs survive pooling and can be included in the existing save snapshot.</summary>
    public static class NpcGreetingMemory
    {
        private static readonly HashSet<string> greeted = new HashSet<string>(StringComparer.Ordinal);

        public static bool Contains(string id) => !string.IsNullOrWhiteSpace(id) && greeted.Contains(id);
        public static void Mark(string id)
        {
            if (!string.IsNullOrWhiteSpace(id)) greeted.Add(id);
        }

        public static string[] Capture()
        {
            var result = new string[greeted.Count];
            greeted.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        public static void Restore(IEnumerable<string> ids)
        {
            Clear();
            if (ids == null) return;
            foreach (string id in ids) Mark(id);
        }

        public static void Clear() => greeted.Clear();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => Clear();
    }
}
