using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Chapter-aware Day 5 population. Security units arrive from alternating
    /// sides and the supplied Day 5 art is used without scene or prefab setup.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayFiveEncounter : MonoBehaviour
    {
        private SurfDayProgressionDirector director;
        private int lastChapter = -1;
        private int nextSide = -1;
        private PixelWaterGPU sharedSortingSource;
        private int sharedLane;
        private float nextSharedSortingRefresh;
        private readonly HashSet<DayFiveCombatant> finalWaveTargets = new();
        private DayFiveCombatant activeSignalRelay;
        private bool signalRelaySpawned;
        private bool signalRelayDefeated;
        private float signalRelayFallbackAt;
        private float nextSignalRelayRecoveryAt;
        private bool initialised;

        public bool CanAdvanceFromSignalRelay =>
            signalRelayDefeated ||
            (signalRelaySpawned && Time.time >= signalRelayFallbackAt);

        public void DebugAllowFinalWave()
        {
            signalRelaySpawned = true;
            signalRelayDefeated = true;
        }

        public static DayFiveEncounter Begin(SurfDayProgressionDirector progression)
        {
            if (progression == null)
                return null;

            DayFiveEncounter existing = FindFirstObjectByType<DayFiveEncounter>();
            if (existing != null)
            {
                if (!existing.initialised || existing.director != progression)
                    existing.lastChapter = -1;
                existing.director = progression;
                existing.initialised = true;
                return existing;
            }

            GameObject host = new("Day Five Security Network");
            DayFiveEncounter encounter = host.AddComponent<DayFiveEncounter>();
            encounter.director = progression;
            encounter.initialised = true;
            return encounter;
        }

        private void Update()
        {
            if (!initialised || director == null || director.CurrentDay != 5)
                return;

            RefreshSharedWaveSorting(false);

            int chapter = (int)director.CurrentChapter;
            if (chapter != lastChapter)
            {
                lastChapter = chapter;
                SpawnForChapter(director.CurrentChapter);
            }

            if (director.CurrentChapter == SurfDayProgressionDirector.Chapter.Storm &&
                !signalRelayDefeated &&
                activeSignalRelay == null &&
                Time.time >= nextSignalRelayRecoveryAt)
            {
                nextSignalRelayRecoveryAt = Time.time + 1f;
                SpawnSignalRelayMiniBoss();
            }
        }

        private void SpawnForChapter(SurfDayProgressionDirector.Chapter chapter)
        {
            switch (chapter)
            {
                case SurfDayProgressionDirector.Chapter.Storm:
                    SpawnSignalRelayMiniBoss();
                    break;
                case SurfDayProgressionDirector.Chapter.FinalWave:
                    SpawnFinalPair();
                    break;
                default:
                    SpawnPatrolPair();
                    break;
            }
        }

        public void SpawnFinalPair()
        {
            foreach (DayFiveCombatant combatant in finalWaveTargets)
                if (combatant != null && !combatant.IsDefeated)
                    return;

            foreach (DayFiveCombatant combatant in FindObjectsByType<DayFiveCombatant>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (combatant != null && !combatant.IsDefeated)
                    combatant.BeginRetreat(true);
            }

            finalWaveTargets.Clear();
            activeSignalRelay = null;
            finalWaveTargets.Add(Spawn(DayFiveEnemyKind.Warden));
        }

        private void SpawnSignalRelayMiniBoss()
        {
            if (activeSignalRelay != null && !activeSignalRelay.IsDefeated)
                return;

            RetreatActiveCombatants();
            RefreshSharedWaveSorting(true);
            activeSignalRelay = Spawn(DayFiveEnemyKind.SignalRelay);
            signalRelaySpawned = true;
            signalRelayDefeated = false;
            signalRelayFallbackAt = Time.time + 25f;
        }

        private void SpawnPatrolPair()
        {
            RetreatActiveCombatants();
            RefreshSharedWaveSorting(true);
            Spawn(DayFiveEnemyKind.Drone);
            Spawn(DayFiveEnemyKind.SurveillanceBuoy);
        }

        public void NotifyCombatantDefeated(DayFiveCombatant combatant)
        {
            if (combatant != null &&
                combatant.Kind == DayFiveEnemyKind.SignalRelay)
            {
                signalRelayDefeated = true;
                activeSignalRelay = null;
                return;
            }

            if (!finalWaveTargets.Remove(combatant) || finalWaveTargets.Count > 0)
                return;

            director?.CompleteDayFive();
        }

        public void EndEncounter()
        {
            finalWaveTargets.Clear();
            activeSignalRelay = null;
            signalRelaySpawned = false;
            signalRelayDefeated = false;
            RetreatActiveCombatants();
            initialised = false;
        }

        private static void RetreatActiveCombatants()
        {
            foreach (DayFiveCombatant combatant in FindObjectsByType<DayFiveCombatant>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (combatant != null && !combatant.IsDefeated)
                    combatant.BeginRetreat(true);
            }
        }

        private DayFiveCombatant Spawn(DayFiveEnemyKind kind)
        {
            int side = nextSide;
            nextSide *= -1;

            GameObject enemy = new($"Day 5 {ReadableName(kind)} - {(side < 0 ? "Left" : "Right")} Entry");
            enemy.transform.SetParent(transform, false);
            enemy.AddComponent<SpriteRenderer>();
            enemy.AddComponent<BoxCollider2D>();
            enemy.AddComponent<Rigidbody2D>();
            DayFiveCombatant combatant = enemy.AddComponent<DayFiveCombatant>();
            combatant.Initialise(
                kind,
                side,
                director,
                sharedSortingSource,
                sharedLane);
            return combatant;
        }

        private void RefreshSharedWaveSorting(bool force)
        {
            if (!force && Time.time < nextSharedSortingRefresh)
                return;

            nextSharedSortingRefresh = Time.time + 0.12f;
            TinyWaveSurfer player = null;
            foreach (TinyWaveSurfer surfer in GameplayTargetCache.Surfers)
            {
                if (surfer != null && surfer.IsPlayerControlled && !surfer.IsDead)
                {
                    player = surfer;
                    break;
                }
            }

            float sampleX = player != null
                ? player.transform.position.x
                : Camera.main != null ? Camera.main.transform.position.x : transform.position.x;
            List<PixelWaterGPU> layers = EndlessWaveSections.LayersNearest(sampleX);
            layers.RemoveAll(layer => layer == null || !layer.isActiveAndEnabled);
            layers.Sort((a, b) =>
                a.IndependentLayerIndex.CompareTo(b.IndependentLayerIndex));
            if (layers.Count == 0)
                return;

            if (layers.Count >= 2 && player != null)
            {
                int closestLane = 0;
                float closestDistance = float.PositiveInfinity;
                for (int lane = 0; lane < layers.Count - 1; lane++)
                {
                    float centreY = Mathf.Lerp(
                        layers[lane].GetGameplaySurfaceHeight(sampleX),
                        layers[lane + 1].GetGameplaySurfaceHeight(sampleX),
                        0.5f);
                    float distance = Mathf.Abs(player.transform.position.y - centreY);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestLane = lane;
                    }
                }
                sharedLane = closestLane;
            }
            else
            {
                sharedLane = Mathf.Clamp(sharedLane, 0, Mathf.Max(0, layers.Count - 2));
            }

            sharedSortingSource = layers[Mathf.Clamp(sharedLane, 0, layers.Count - 1)];
            foreach (DayFiveCombatant combatant in FindObjectsByType<DayFiveCombatant>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (combatant != null &&
                    (combatant.Kind == DayFiveEnemyKind.Drone ||
                     combatant.Kind == DayFiveEnemyKind.SurveillanceBuoy ||
                     combatant.Kind == DayFiveEnemyKind.SignalRelay ||
                     combatant.Kind == DayFiveEnemyKind.Warden))
                    combatant.SetSharedWaveSorting(sharedSortingSource, sharedLane);
            }
        }

        private static string ReadableName(DayFiveEnemyKind kind) => kind switch
        {
            DayFiveEnemyKind.SurveillanceBuoy => "Surveillance Buoy",
            _ => kind.ToString()
        };
    }
}
