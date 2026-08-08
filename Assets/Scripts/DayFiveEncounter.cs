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
        private readonly HashSet<DayFiveCombatant> finalPair = new();
        private bool initialised;

        public static DayFiveEncounter Begin(SurfDayProgressionDirector progression)
        {
            if (progression == null)
                return null;

            DayFiveEncounter existing = FindFirstObjectByType<DayFiveEncounter>();
            if (existing != null)
            {
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
            if (chapter == lastChapter)
                return;

            lastChapter = chapter;
            SpawnForChapter(director.CurrentChapter);
        }

        private void SpawnForChapter(SurfDayProgressionDirector.Chapter chapter)
        {
            if (chapter == SurfDayProgressionDirector.Chapter.FinalWave)
            {
                SpawnFinalPair();
                return;
            }

            SpawnPair(false);
        }

        public void SpawnFinalPair()
        {
            foreach (DayFiveCombatant combatant in finalPair)
                if (combatant != null && !combatant.IsDefeated)
                    return;

            foreach (DayFiveCombatant combatant in FindObjectsByType<DayFiveCombatant>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (combatant != null)
                    combatant.BeginRetreat(true);
            }

            finalPair.Clear();
            SpawnPair(true);
        }

        private void SpawnPair(bool countsForFinalWave)
        {
            RefreshSharedWaveSorting(true);
            DayFiveCombatant drone = Spawn(DayFiveEnemyKind.Drone);
            DayFiveCombatant buoy = Spawn(DayFiveEnemyKind.SurveillanceBuoy);
            if (countsForFinalWave)
            {
                finalPair.Add(drone);
                finalPair.Add(buoy);
            }
        }

        public void NotifyCombatantDefeated(DayFiveCombatant combatant)
        {
            if (!finalPair.Remove(combatant) || finalPair.Count > 0)
                return;

            director?.CompleteDayFive();
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
                     combatant.Kind == DayFiveEnemyKind.SurveillanceBuoy))
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
