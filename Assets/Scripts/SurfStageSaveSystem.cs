using System;
using System.IO;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Versioned, disk-backed run save. The JSON file survives application closes,
    /// while the old PlayerPrefs save is migrated automatically when encountered.
    /// </summary>
    public static class SurfStageSaveSystem
    {
        private const int CurrentVersion = 2;
        private const string LegacySaveKey = "SurferSlug.StageSave.v1";
        private const string SaveFileName = "surfer_slug_save.json";
        private const string BackupFileName = "surfer_slug_save.backup.json";

        [Serializable]
        public sealed class SaveData
        {
            public int version = CurrentVersion;
            public string savedAtUtc = string.Empty;

            public int day = 1;
            public int chapter;
            public float runTime;
            public float distanceTravelled;
            public int rescues;
            public bool finalWaveStarted;
            public bool bossDefeatedSunset;

            public int lives = 3;
            public int unlockedAbilities;
            public int jumpUpgradeLevel;
            public int waterSlashUpgradeLevel;
            public int skidUpgradeLevel;

            public int totalStoke;
            public int dayStoke;

            public bool hasPlayerState;
            public float playerX;
            public int playerWaveIndex;
            public int playerHealth = 3;
            public float playerDirection = 1f;
            public string[] throwableSpriteNames = Array.Empty<string>();
        }

        public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
        private static string BackupPath => Path.Combine(Application.persistentDataPath, BackupFileName);
        public static bool HasSave => TryLoad(out _);

        public static void Save(SurfDayProgressionDirector director)
        {
            if (director == null)
                return;

            SaveData data = director.CaptureSaveData();
            data.version = CurrentVersion;
            data.savedAtUtc = DateTime.UtcNow.ToString("O");

            if (SurfRunLifeManager.Instance != null)
                data.lives = Mathf.Max(1, SurfRunLifeManager.Instance.LivesRemaining);

            if (SurfAbilityProgression.Instance != null)
            {
                data.unlockedAbilities = (int)SurfAbilityProgression.Instance.Unlocked;
                data.jumpUpgradeLevel = SurfAbilityProgression.Instance.JumpUpgradeLevel;
                data.waterSlashUpgradeLevel = SurfAbilityProgression.Instance.WaterSlashUpgradeLevel;
                data.skidUpgradeLevel = SurfAbilityProgression.Instance.SkidUpgradeLevel;
            }

            if (AirTrickScoreSystem.Instance != null)
            {
                data.totalStoke = AirTrickScoreSystem.Instance.TotalStoke;
                data.dayStoke = AirTrickScoreSystem.Instance.DayStoke;
            }

            TinyWaveSurfer surfer = FindPlayerSurfer();
            surfer?.CapturePersistentState(data);

            WriteAtomic(JsonUtility.ToJson(data, true));
        }

        public static bool TryLoad(out SaveData data)
        {
            data = null;

            if (TryReadFile(SavePath, out data))
                return Validate(data);

            if (TryReadFile(BackupPath, out data))
                return Validate(data);

            // One-time migration from the original PlayerPrefs implementation.
            if (PlayerPrefs.HasKey(LegacySaveKey))
            {
                try
                {
                    string legacyJson = PlayerPrefs.GetString(LegacySaveKey, string.Empty);
                    if (!string.IsNullOrWhiteSpace(legacyJson))
                    {
                        data = JsonUtility.FromJson<SaveData>(legacyJson);
                        if (Validate(data))
                        {
                            data.version = CurrentVersion;
                            data.savedAtUtc = DateTime.UtcNow.ToString("O");
                            WriteAtomic(JsonUtility.ToJson(data, true));
                            PlayerPrefs.DeleteKey(LegacySaveKey);
                            PlayerPrefs.Save();
                            return true;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Could not migrate the old Surfer Slug save: " + exception.Message);
                }
            }

            data = null;
            return false;
        }

        public static void Delete()
        {
            SafeDelete(SavePath);
            SafeDelete(BackupPath);
            PlayerPrefs.DeleteKey(LegacySaveKey);
            PlayerPrefs.Save();
        }

        private static TinyWaveSurfer FindPlayerSurfer()
        {
            TinyWaveSurfer[] surfers = UnityEngine.Object.FindObjectsByType<TinyWaveSurfer>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            foreach (TinyWaveSurfer surfer in surfers)
            {
                if (surfer != null && surfer.IsPlayerControlled)
                    return surfer;
            }
            return null;
        }

        private static bool Validate(SaveData data)
        {
            return data != null && data.day >= 1 && data.chapter >= 0;
        }

        private static bool TryReadFile(string path, out SaveData data)
        {
            data = null;
            if (!File.Exists(path))
                return false;

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return false;
                data = JsonUtility.FromJson<SaveData>(json);
                return data != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not read Surfer Slug save at " + path + ": " + exception.Message);
                return false;
            }
        }

        private static void WriteAtomic(string json)
        {
            try
            {
                string directory = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                string temporaryPath = SavePath + ".tmp";
                using (FileStream stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(SavePath))
                {
                    File.Copy(SavePath, BackupPath, true);
                    File.Delete(SavePath);
                }

                File.Move(temporaryPath, SavePath);
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not write Surfer Slug save: " + exception.Message);
            }
        }

        private static void SafeDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not delete save file " + path + ": " + exception.Message);
            }
        }
    }
}
