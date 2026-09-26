using System;
using System.IO;
using UnityEngine;

namespace SaksiTerakhir.Story
{
    public static class ChapterOneSaveStore
    {
        private const int CurrentVersion = 1;

        [Serializable]
        private sealed class SaveFile
        {
            public int version;
            public ChapterOneProgress progress;
        }

        public static void Save(ChapterOneProgress progress, string path)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Save path is empty.", nameof(path));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temporaryPath = path + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(new SaveFile
                {
                    version = CurrentVersion,
                    progress = progress
                }, true));
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        public static ChapterOneProgress Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new ChapterOneProgress();
            try
            {
                SaveFile file = JsonUtility.FromJson<SaveFile>(File.ReadAllText(path));
                if (file == null || file.version != CurrentVersion || file.progress == null
                    || file.progress.Version != CurrentVersion
                    || !Enum.IsDefined(typeof(ChapterOneStage), file.progress.Stage))
                    throw new InvalidDataException("Unsupported version or invalid chapter state.");
                return file.progress;
            }
            catch (Exception error)
            {
                Debug.LogError($"Chapter one save could not be loaded; starting a new chapter. {error.Message}");
                return new ChapterOneProgress();
            }
        }
    }
}
