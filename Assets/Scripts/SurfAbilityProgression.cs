using System;
using UnityEngine;

namespace PixelOcean
{
    [Flags]
    public enum SurfAbility
    {
        None = 0,
        WaveSwitch = 1 << 0,
        ChargedJump = 1 << 1,
        Handstand = 1 << 2,
        Rotation = 1 << 3,
        Flip = 1 << 4,
        DoubleChain = 1 << 5,
        TripleChain = 1 << 6,
        WaterSkid = 1 << 7,
        WaterSlash = 1 << 8,
        Flow = 1 << 9,
        FlowFinisher = 1 << 10,
        ThrowItems = 1 << 11
    }

    [DefaultExecutionOrder(-12100)]
    public sealed class SurfAbilityProgression : MonoBehaviour
    {
        public static SurfAbilityProgression Instance { get; private set; }
        [SerializeField] private SurfAbility unlocked;
        [SerializeField, Min(0)] private int jumpUpgradeLevel;
        [SerializeField, Min(0)] private int waterSlashUpgradeLevel;
        [SerializeField, Min(0)] private int skidUpgradeLevel;

        public SurfAbility Unlocked => unlocked;
        public int JumpUpgradeLevel => jumpUpgradeLevel;
        public int WaterSlashUpgradeLevel => waterSlashUpgradeLevel;
        public int SkidUpgradeLevel => skidUpgradeLevel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<SurfAbilityProgression>() != null) return;
            GameObject host = new("Surf Ability Progression");
            DontDestroyOnLoad(host);
            host.AddComponent<SurfAbilityProgression>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public bool Has(SurfAbility ability) => (unlocked & ability) == ability;

        public void DebugUnlockAll()
        {
            unlocked = SurfAbility.WaveSwitch | SurfAbility.ChargedJump |
                SurfAbility.Handstand | SurfAbility.Rotation | SurfAbility.Flip |
                SurfAbility.DoubleChain | SurfAbility.TripleChain |
                SurfAbility.WaterSkid | SurfAbility.WaterSlash |
                SurfAbility.Flow | SurfAbility.FlowFinisher | SurfAbility.ThrowItems;
            ApplyUpgradesToAllPlayers();
        }
        public void ResetForNewRun()
        {
            unlocked = SurfAbility.WaveSwitch;
            jumpUpgradeLevel = 0;
            waterSlashUpgradeLevel = 0;
            skidUpgradeLevel = 0;
        }
        public bool Unlock(SurfAbility ability)
        {
            bool changed = !Has(ability);
            unlocked |= ability;
            return changed;
        }
        public void RestoreFor(int day, SurfDayProgressionDirector.Chapter chapter)
        {
            ResetForNewRun();

            // Legacy-save reconstruction. New saves restore the exact flags.
            if (day >= 2)
            {
                DebugUnlockAll();
                return;
            }

            if (chapter >= SurfDayProgressionDirector.Chapter.DangerousWater)
                Unlock(SurfAbility.ChargedJump | SurfAbility.Handstand | SurfAbility.ThrowItems);
            if (chapter >= SurfDayProgressionDirector.Chapter.StrangeTide)
                Unlock(SurfAbility.Rotation | SurfAbility.Flip |
                    SurfAbility.DoubleChain | SurfAbility.TripleChain | SurfAbility.Flow);
            if (chapter >= SurfDayProgressionDirector.Chapter.Storm)
                Unlock(SurfAbility.WaterSkid | SurfAbility.WaterSlash);
            if (chapter >= SurfDayProgressionDirector.Chapter.FinalWave)
                Unlock(SurfAbility.FlowFinisher);
        }

        public void RestoreExact(SurfAbility savedAbilities, int jumpLevel, int slashLevel, int skidLevel)
        {
            unlocked = savedAbilities | SurfAbility.WaveSwitch;
            jumpUpgradeLevel = Mathf.Max(0, jumpLevel);
            waterSlashUpgradeLevel = Mathf.Max(0, slashLevel);
            skidUpgradeLevel = Mathf.Max(0, skidLevel);
            ApplyUpgradesToAllPlayers();
        }

        public void AddUpgrade(int upgradeIndex)
        {
            switch (upgradeIndex)
            {
                case 0: jumpUpgradeLevel++; break;
                case 1: waterSlashUpgradeLevel++; break;
                case 2: skidUpgradeLevel++; break;
                default: return;
            }

            ApplyUpgradesToAllPlayers();
            SurfDayProgressionDirector director = FindFirstObjectByType<SurfDayProgressionDirector>();
            if (director != null)
                SurfStageSaveSystem.Save(director);
        }

        public void ApplyUpgradesToAllPlayers()
        {
            foreach (TinyWaveSurfer surfer in FindObjectsByType<TinyWaveSurfer>(FindObjectsSortMode.None))
                if (surfer != null)
                    surfer.ApplyProgressionUpgrades();
        }
    }
}
