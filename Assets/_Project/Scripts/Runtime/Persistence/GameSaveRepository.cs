using System;
using UnityEngine;

namespace SaksiTerakhir.Persistence
{
    public static class GameSaveRepository
    {
        public static bool TryLoadLatest(string path, out GameSaveSnapshot snapshot, out string error)
        {
            if (string.IsNullOrWhiteSpace(path))
            { snapshot = null; error = "Save path is empty."; return false; }
            if (TryLoad(path, out snapshot, out error)) return true;
            string primaryError = error;
            if (TryLoad(path + ".bak", out snapshot, out string backupError))
            {
                error = "Recovered the previous valid checkpoint from backup.";
                return true;
            }
            error = string.IsNullOrEmpty(primaryError) ? backupError : primaryError;
            return false;
        }

        private static bool TryLoad(string path, out GameSaveSnapshot snapshot, out string error)
        {
            snapshot = null;
            if (!ProtectedJsonSaveStore.TryLoad(path, out string payload, out error)) return false;
            try
            {
                GameSaveSnapshot candidate = JsonUtility.FromJson<GameSaveSnapshot>(payload);
                if (candidate == null) { error = "Save payload is empty."; return false; }
                if (!candidate.Validate(out error)) return false;
                snapshot = candidate;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { error = "Invalid save payload: " + exception.Message; return false; }
        }
    }
}
