using System.Collections.Generic;
using UnityEngine;

namespace PixelOcean
{
    /// <summary>
    /// Owns the Day 6 population. Each chapter introduces a deliberate pair of
    /// new oddities; the final wave rotates through the complete seven-creature
    /// roster without handing spawning back to the generic ambient system.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DaySixEncounter : MonoBehaviour
    {
        private static readonly DaySixCreatureKind[] FinalRoster =
        {
            DaySixCreatureKind.Fishbowl,
            DaySixCreatureKind.MushroomSquid,
            DaySixCreatureKind.MustacheShark,
            DaySixCreatureKind.Resort,
            DaySixCreatureKind.Starfish,
            DaySixCreatureKind.Toaster,
            DaySixCreatureKind.Toilet
        };

        private readonly List<DaySixCreature> activeCreatures = new();
        private readonly List<DaySixSkyHostile> activeSkyHostiles = new();
        private SurfDayProgressionDirector director;
        private SurfDayProgressionDirector.Chapter observedChapter =
            (SurfDayProgressionDirector.Chapter)(-1);
        private float nextSpawnAt;
        private float nextSkySpawnAt;
        private int nextEntrySide = -1;
        private int nextSkyEntrySide = -1;
        private int finalRosterIndex;
        private int finalSkyRosterIndex;
        private bool initialised;

        public static DaySixEncounter Begin(SurfDayProgressionDirector progression)
        {
            if (progression == null)
                return null;

            DaySixEncounter existing = FindFirstObjectByType<DaySixEncounter>();
            if (existing != null)
            {
                existing.director = progression;
                existing.initialised = true;
                existing.observedChapter = (SurfDayProgressionDirector.Chapter)(-1);
                return existing;
            }

            GameObject host = new("Day 6 - Beyond the Horizon");
            DaySixEncounter encounter = host.AddComponent<DaySixEncounter>();
            encounter.director = progression;
            encounter.initialised = true;
            return encounter;
        }

        private void Update()
        {
            if (!initialised || director == null || director.CurrentDay != 6 ||
                director.CurrentChapter == SurfDayProgressionDirector.Chapter.Complete)
                return;

            activeCreatures.RemoveAll(creature => creature == null);
            activeSkyHostiles.RemoveAll(hostile => hostile == null);
            if (director.CurrentChapter != observedChapter)
            {
                observedChapter = director.CurrentChapter;
                BeginChapterPopulation(observedChapter);
            }

            if (Time.time >= nextSpawnAt && activeCreatures.Count < PopulationCap(observedChapter))
            {
                Spawn(ChooseReinforcement(observedChapter));
                nextSpawnAt = Time.time + SpawnInterval(observedChapter);
            }

            if (Time.time >= nextSkySpawnAt &&
                activeSkyHostiles.Count < SkyPopulationCap(observedChapter))
            {
                SpawnSkyHostile(ChooseSkyReinforcement(observedChapter));
                nextSkySpawnAt = Time.time + SkySpawnInterval(observedChapter);
            }
        }

        private void BeginChapterPopulation(SurfDayProgressionDirector.Chapter chapter)
        {
            RetreatActiveCreatures();
            activeCreatures.Clear();
            RetreatActiveSkyHostiles();
            activeSkyHostiles.Clear();

            switch (chapter)
            {
                case SurfDayProgressionDirector.Chapter.Dawn:
                    Spawn(DaySixCreatureKind.Starfish);
                    Spawn(DaySixCreatureKind.Fishbowl);
                    break;

                case SurfDayProgressionDirector.Chapter.FirstRescue:
                    Spawn(DaySixCreatureKind.Fishbowl);
                    Spawn(DaySixCreatureKind.Starfish);
                    break;

                case SurfDayProgressionDirector.Chapter.DangerousWater:
                    Spawn(DaySixCreatureKind.MustacheShark);
                    Spawn(DaySixCreatureKind.Toaster);
                    SpawnSkyHostile(DaySixSkyHostileKind.ClawUfo);
                    break;

                case SurfDayProgressionDirector.Chapter.StrangeTide:
                    Spawn(DaySixCreatureKind.MushroomSquid);
                    Spawn(DaySixCreatureKind.Resort);
                    Spawn(DaySixCreatureKind.Starfish);
                    SpawnSkyHostile(DaySixSkyHostileKind.RacerUfo);
                    break;

                case SurfDayProgressionDirector.Chapter.Storm:
                    Spawn(DaySixCreatureKind.Toilet);
                    Spawn(DaySixCreatureKind.Resort);
                    Spawn(DaySixCreatureKind.MushroomSquid);
                    SpawnSkyHostile(DaySixSkyHostileKind.RetroUfo);
                    SpawnSkyHostile(DaySixSkyHostileKind.RacerUfo);
                    break;

                case SurfDayProgressionDirector.Chapter.FinalWave:
                    finalRosterIndex = 0;
                    Spawn(DaySixCreatureKind.MustacheShark);
                    Spawn(DaySixCreatureKind.Toaster);
                    Spawn(DaySixCreatureKind.Toilet);
                    Spawn(DaySixCreatureKind.Fishbowl);
                    finalSkyRosterIndex = 0;
                    SpawnSkyHostile(DaySixSkyHostileKind.ClawUfo);
                    SpawnSkyHostile(DaySixSkyHostileKind.RacerUfo);
                    SpawnSkyHostile(DaySixSkyHostileKind.RetroUfo);
                    break;
            }

            nextSpawnAt = Time.time + SpawnInterval(chapter);
            nextSkySpawnAt = Time.time + SkySpawnInterval(chapter);
        }

        private DaySixSkyHostileKind ChooseSkyReinforcement(
            SurfDayProgressionDirector.Chapter chapter)
        {
            if (chapter == SurfDayProgressionDirector.Chapter.FinalWave)
            {
                DaySixSkyHostileKind[] finalSkyRoster =
                {
                    DaySixSkyHostileKind.ClawUfo,
                    DaySixSkyHostileKind.RacerUfo,
                    DaySixSkyHostileKind.RetroUfo
                };
                DaySixSkyHostileKind result =
                    finalSkyRoster[finalSkyRosterIndex % finalSkyRoster.Length];
                finalSkyRosterIndex++;
                return result;
            }

            return chapter switch
            {
                SurfDayProgressionDirector.Chapter.DangerousWater =>
                    DaySixSkyHostileKind.ClawUfo,
                SurfDayProgressionDirector.Chapter.StrangeTide =>
                    DaySixSkyHostileKind.RacerUfo,
                SurfDayProgressionDirector.Chapter.Storm =>
                    Random.value < 0.55f
                        ? DaySixSkyHostileKind.RetroUfo
                        : DaySixSkyHostileKind.RacerUfo,
                _ => DaySixSkyHostileKind.ClawUfo
            };
        }

        private DaySixCreatureKind ChooseReinforcement(SurfDayProgressionDirector.Chapter chapter)
        {
            if (chapter == SurfDayProgressionDirector.Chapter.FinalWave)
            {
                DaySixCreatureKind result = FinalRoster[finalRosterIndex % FinalRoster.Length];
                finalRosterIndex++;
                return result;
            }

            return chapter switch
            {
                SurfDayProgressionDirector.Chapter.Dawn =>
                    Random.value < 0.58f ? DaySixCreatureKind.Starfish : DaySixCreatureKind.Fishbowl,
                SurfDayProgressionDirector.Chapter.FirstRescue =>
                    Random.value < 0.5f ? DaySixCreatureKind.Fishbowl : DaySixCreatureKind.Starfish,
                SurfDayProgressionDirector.Chapter.DangerousWater =>
                    Random.value < 0.5f ? DaySixCreatureKind.MustacheShark : DaySixCreatureKind.Toaster,
                SurfDayProgressionDirector.Chapter.StrangeTide => Random.Range(0, 3) switch
                {
                    0 => DaySixCreatureKind.MushroomSquid,
                    1 => DaySixCreatureKind.Resort,
                    _ => DaySixCreatureKind.Starfish
                },
                SurfDayProgressionDirector.Chapter.Storm => Random.Range(0, 4) switch
                {
                    0 => DaySixCreatureKind.Toilet,
                    1 => DaySixCreatureKind.Resort,
                    2 => DaySixCreatureKind.MushroomSquid,
                    _ => DaySixCreatureKind.MustacheShark
                },
                _ => FinalRoster[Random.Range(0, FinalRoster.Length)]
            };
        }

        private void Spawn(DaySixCreatureKind kind)
        {
            TinyWaveSurfer player = FindPlayer();
            float sampleX = player != null ? player.transform.position.x : transform.position.x;
            List<PixelWaterGPU> waterLayers = EndlessWaveSections.LayersNearest(sampleX);
            waterLayers.RemoveAll(layer => layer == null || !layer.isActiveAndEnabled);
            int laneCount = Mathf.Max(1, waterLayers.Count - 1);
            int lane = Random.Range(0, laneCount);

            GameObject creatureObject = new($"Day 6 {ReadableName(kind)}");
            creatureObject.transform.SetParent(transform, false);
            creatureObject.transform.position = new Vector3(sampleX, player != null ? player.transform.position.y : 0f, 0f);
            creatureObject.AddComponent<SpriteRenderer>();
            creatureObject.AddComponent<BoxCollider2D>();
            creatureObject.AddComponent<Rigidbody2D>();
            creatureObject.AddComponent<InterWaveRenderItem>();
            DaySixCreature creature = creatureObject.AddComponent<DaySixCreature>();
            creature.Initialise(kind, lane, nextEntrySide, this);
            nextEntrySide *= -1;
            activeCreatures.Add(creature);
        }

        private void SpawnSkyHostile(DaySixSkyHostileKind kind)
        {
            TinyWaveSurfer player = FindPlayer();
            float sampleX = player != null ? player.transform.position.x : transform.position.x;

            GameObject hostileObject = new($"Day 6 {ReadableSkyName(kind)}");
            hostileObject.transform.SetParent(transform, false);
            hostileObject.transform.position = new Vector3(
                sampleX,
                player != null ? player.transform.position.y + 2f : 2f,
                0f);
            hostileObject.AddComponent<SpriteRenderer>();
            hostileObject.AddComponent<BoxCollider2D>();
            hostileObject.AddComponent<Rigidbody2D>();
            hostileObject.AddComponent<InterWaveRenderItem>();
            DaySixSkyHostile hostile = hostileObject.AddComponent<DaySixSkyHostile>();
            hostile.Initialise(kind, nextSkyEntrySide, this);
            nextSkyEntrySide *= -1;
            activeSkyHostiles.Add(hostile);
        }

        public void NotifyCreatureRemoved(DaySixCreature creature, bool defeated)
        {
            activeCreatures.Remove(creature);
            if (defeated && director != null && director.CurrentChapter == SurfDayProgressionDirector.Chapter.FinalWave)
                nextSpawnAt = Mathf.Min(nextSpawnAt, Time.time + 0.75f);
        }

        public void NotifySkyHostileRemoved(DaySixSkyHostile hostile, bool defeated)
        {
            activeSkyHostiles.Remove(hostile);
            if (defeated && director != null &&
                director.CurrentChapter == SurfDayProgressionDirector.Chapter.FinalWave)
            {
                nextSkySpawnAt = Mathf.Min(nextSkySpawnAt, Time.time + 1.25f);
            }
        }

        public void EndEncounter()
        {
            initialised = false;
            RetreatActiveCreatures();
            activeCreatures.Clear();
            RetreatActiveSkyHostiles();
            activeSkyHostiles.Clear();
            Destroy(gameObject);
        }

        private void RetreatActiveCreatures()
        {
            DaySixCreature[] creatures = activeCreatures.ToArray();
            foreach (DaySixCreature creature in creatures)
                if (creature != null)
                    creature.BeginRetreat();

            foreach (DaySixHazardProjectile hazard in FindObjectsByType<DaySixHazardProjectile>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (hazard != null)
                    Destroy(hazard.gameObject);
            }
        }

        private void RetreatActiveSkyHostiles()
        {
            DaySixSkyHostile[] hostiles = activeSkyHostiles.ToArray();
            foreach (DaySixSkyHostile hostile in hostiles)
                if (hostile != null)
                    hostile.BeginRetreat();
        }

        private static int PopulationCap(SurfDayProgressionDirector.Chapter chapter) => chapter switch
        {
            SurfDayProgressionDirector.Chapter.Dawn => 3,
            SurfDayProgressionDirector.Chapter.FirstRescue => 3,
            SurfDayProgressionDirector.Chapter.DangerousWater => 4,
            SurfDayProgressionDirector.Chapter.StrangeTide => 4,
            SurfDayProgressionDirector.Chapter.Storm => 5,
            SurfDayProgressionDirector.Chapter.FinalWave => 5,
            _ => 3
        };

        private static float SpawnInterval(SurfDayProgressionDirector.Chapter chapter) => chapter switch
        {
            SurfDayProgressionDirector.Chapter.Dawn => 8.0f,
            SurfDayProgressionDirector.Chapter.FirstRescue => 7.2f,
            SurfDayProgressionDirector.Chapter.DangerousWater => 6.2f,
            SurfDayProgressionDirector.Chapter.StrangeTide => 5.6f,
            SurfDayProgressionDirector.Chapter.Storm => 4.9f,
            SurfDayProgressionDirector.Chapter.FinalWave => 4.25f,
            _ => 7f
        };

        private static int SkyPopulationCap(SurfDayProgressionDirector.Chapter chapter) => chapter switch
        {
            SurfDayProgressionDirector.Chapter.DangerousWater => 1,
            SurfDayProgressionDirector.Chapter.StrangeTide => 1,
            SurfDayProgressionDirector.Chapter.Storm => 2,
            SurfDayProgressionDirector.Chapter.FinalWave => 3,
            _ => 0
        };

        private static float SkySpawnInterval(SurfDayProgressionDirector.Chapter chapter) => chapter switch
        {
            SurfDayProgressionDirector.Chapter.DangerousWater => 12f,
            SurfDayProgressionDirector.Chapter.StrangeTide => 10.5f,
            SurfDayProgressionDirector.Chapter.Storm => 9f,
            SurfDayProgressionDirector.Chapter.FinalWave => 7.5f,
            _ => 99f
        };

        private static TinyWaveSurfer FindPlayer()
        {
            foreach (TinyWaveSurfer surfer in GameplayTargetCache.Surfers)
                if (surfer != null && surfer.IsPlayerControlled && !surfer.IsDead)
                    return surfer;
            return null;
        }

        private static string ReadableName(DaySixCreatureKind kind) => kind switch
        {
            DaySixCreatureKind.Fishbowl => "Fish in a Fishbowl",
            DaySixCreatureKind.MushroomSquid => "Mushroom Squid",
            DaySixCreatureKind.MustacheShark => "Mustache Shark",
            DaySixCreatureKind.Resort => "Tiny Iceberg Resort",
            DaySixCreatureKind.Starfish => "Pinball Starfish",
            DaySixCreatureKind.Toaster => "Waterproof Toaster",
            DaySixCreatureKind.Toilet => "Royal Flush Toilet",
            _ => kind.ToString()
        };

        private static string ReadableSkyName(DaySixSkyHostileKind kind) => kind switch
        {
            DaySixSkyHostileKind.ClawUfo => "Claw UFO",
            DaySixSkyHostileKind.RacerUfo => "Racer UFO",
            DaySixSkyHostileKind.RetroUfo => "Retro UFO",
            _ => kind.ToString()
        };
    }
}
