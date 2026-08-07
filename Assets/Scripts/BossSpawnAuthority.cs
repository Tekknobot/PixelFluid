using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Single source of truth for story boss creation. Prevents natural progression,
    /// Continue restoration, arena recovery, and developer tools from creating two
    /// bosses during overlapping frames or delayed spawn coroutines.
    /// </summary>
    public static class BossSpawnAuthority
    {
        private static MonoBehaviour registeredBoss;
        private static bool spawnReserved;

        public static MonoBehaviour ActiveBoss
        {
            get
            {
                RefreshRegisteredBoss();
                return registeredBoss;
            }
        }

        public static bool HasBoss
        {
            get
            {
                RefreshRegisteredBoss();
                return registeredBoss != null;
            }
        }

        public static bool TryReserveSpawn()
        {
            RefreshRegisteredBoss();

            if (registeredBoss != null || spawnReserved)
                return false;

            spawnReserved = true;
            return true;
        }

        public static void ReleaseReservation()
        {
            spawnReserved = false;
        }

        public static bool RegisterBoss(MonoBehaviour candidate)
        {
            if (candidate == null)
            {
                spawnReserved = false;
                return false;
            }

            RefreshRegisteredBoss(candidate);

            if (registeredBoss != null && registeredBoss != candidate)
            {
                spawnReserved = false;
                Object.Destroy(candidate.gameObject);
                return false;
            }

            registeredBoss = candidate;
            spawnReserved = false;
            DestroyOtherBosses(candidate);
            return true;
        }

        public static void UnregisterBoss(MonoBehaviour candidate)
        {
            if (registeredBoss == candidate)
                registeredBoss = null;

            spawnReserved = false;
        }

        public static T FindExistingBoss<T>() where T : MonoBehaviour
        {
            RefreshRegisteredBoss();
            return registeredBoss as T;
        }

        private static void RefreshRegisteredBoss(MonoBehaviour ignoredCandidate = null)
        {
            if (registeredBoss != null)
                return;

            GodzillaLaneSwimmer[] reapers = Object.FindObjectsByType<GodzillaLaneSwimmer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (GodzillaLaneSwimmer boss in reapers)
            {
                if (boss == null || boss == ignoredCandidate)
                    continue;

                registeredBoss = boss;
                return;
            }

            RubberDuckBossSwimmer[] ducks = Object.FindObjectsByType<RubberDuckBossSwimmer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (RubberDuckBossSwimmer boss in ducks)
            {
                if (boss == null || boss == ignoredCandidate)
                    continue;

                registeredBoss = boss;
                return;
            }
        }

        private static void DestroyOtherBosses(MonoBehaviour keeper)
        {
            foreach (GodzillaLaneSwimmer boss in Object.FindObjectsByType<GodzillaLaneSwimmer>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (boss != null && boss != keeper)
                    Object.Destroy(boss.gameObject);
            }

            foreach (RubberDuckBossSwimmer boss in Object.FindObjectsByType<RubberDuckBossSwimmer>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (boss != null && boss != keeper)
                    Object.Destroy(boss.gameObject);
            }
        }
    }
}
