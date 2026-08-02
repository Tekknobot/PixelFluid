using System;
using UnityEngine;

namespace PixelOcean
{
    public static class SurfStageSaveSystem
    {
        private const string SaveKey = "SurferSlug.StageSave.v1";

        [Serializable]
        public sealed class SaveData
        {
            public int day = 1;
            public int chapter = 0;
            public float runTime;
            public int rescues;
            public bool finalWaveStarted;
            public bool bossDefeatedSunset;
            public int lives = 3;
            public int unlockedAbilities;
            public int jumpUpgradeLevel;
            public int waterSlashUpgradeLevel;
            public int skidUpgradeLevel;
        }

        public static bool HasSave => PlayerPrefs.HasKey(SaveKey) && TryLoad(out _);

        public static void Save(SurfDayProgressionDirector director)
        {
            if (director == null) return;
            SaveData data = director.CaptureSaveData();
            if (SurfRunLifeManager.Instance != null)
                data.lives = Mathf.Max(1, SurfRunLifeManager.Instance.LivesRemaining);
            if (SurfAbilityProgression.Instance != null)
            {
                data.unlockedAbilities = (int)SurfAbilityProgression.Instance.Unlocked;
                data.jumpUpgradeLevel = SurfAbilityProgression.Instance.JumpUpgradeLevel;
                data.waterSlashUpgradeLevel = SurfAbilityProgression.Instance.WaterSlashUpgradeLevel;
                data.skidUpgradeLevel = SurfAbilityProgression.Instance.SkidUpgradeLevel;
            }
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static bool TryLoad(out SaveData data)
        {
            data = null;
            if (!PlayerPrefs.HasKey(SaveKey)) return false;
            try
            {
                string json = PlayerPrefs.GetString(SaveKey, string.Empty);
                if (string.IsNullOrWhiteSpace(json)) return false;
                data = JsonUtility.FromJson<SaveData>(json);
                return data != null && data.day >= 1;
            }
            catch
            {
                data = null;
                return false;
            }
        }

        public static void Delete()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }
    }
}
