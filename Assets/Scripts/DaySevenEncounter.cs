using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Owns Day 7 as one uninterrupted final-boss encounter. It recovers AION
    /// after Continue/developer transitions and prevents duplicate manifestations.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DaySevenEncounter : MonoBehaviour
    {
        private SurfDayProgressionDirector director;
        private AionFinalBoss boss;
        private bool completed;
        private float nextRecoveryAt;

        public AionFinalBoss Boss => boss;
        public bool IsComplete => completed;

        public static DaySevenEncounter Begin(SurfDayProgressionDirector progression)
        {
            if (progression == null)
                return null;

            DaySevenEncounter existing = FindFirstObjectByType<DaySevenEncounter>();
            if (existing != null)
            {
                existing.director = progression;
                existing.completed = progression.CurrentChapter ==
                                     SurfDayProgressionDirector.Chapter.Complete;
                existing.EnsureBoss();
                return existing;
            }

            GameObject host = new("Day 7 - The Other Shore");
            DaySevenEncounter encounter = host.AddComponent<DaySevenEncounter>();
            encounter.director = progression;
            encounter.completed = progression.CurrentChapter ==
                                  SurfDayProgressionDirector.Chapter.Complete;
            encounter.EnsureBoss();
            return encounter;
        }

        private void Update()
        {
            if (completed || director == null || director.CurrentDay != 7 ||
                director.CurrentChapter == SurfDayProgressionDirector.Chapter.Complete)
                return;

            if (Time.unscaledTime < nextRecoveryAt)
                return;

            nextRecoveryAt = Time.unscaledTime + 1.25f;
            EnsureBoss();
        }

        private void EnsureBoss()
        {
            if (completed || director == null || director.CurrentDay != 7)
                return;

            if (boss == null)
                boss = FindFirstObjectByType<AionFinalBoss>();

            if (boss == null && !BossSpawnAuthority.HasBoss)
                boss = AionFinalBoss.Spawn(director, this);
        }

        public void NotifyAionDefeated(AionFinalBoss defeatedBoss)
        {
            if (completed || defeatedBoss == null || defeatedBoss != boss)
                return;

            completed = true;
            director?.CompleteDaySeven();
        }

        public void EndEncounter()
        {
            completed = true;

            foreach (AionLaneLaser laser in FindObjectsByType<AionLaneLaser>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (laser != null)
                    Destroy(laser.gameObject);
            }

            if (boss != null)
                Destroy(boss.gameObject);

            Destroy(gameObject);
        }
    }
}
