using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PixelOcean
{
    [DefaultExecutionOrder(15000)]
    [DisallowMultipleComponent]
    public sealed class DayThreeOceanRemembers : MonoBehaviour
    {
        private sealed class WaterState
        {
            public float Horizontal;
            public float Vertical;
            public float Frequency;
            public float Variation;
            public float BigScale;
            public Color Deep;
            public Color Main;
            public Color Surface;
        }

        private static readonly BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo HorizontalForceField = typeof(PixelWaterGPU).GetField("waveHorizontalForce", FieldFlags);
        private static readonly FieldInfo VerticalForceField = typeof(PixelWaterGPU).GetField("waveVerticalForce", FieldFlags);
        private static readonly FieldInfo FrequencyField = typeof(PixelWaterGPU).GetField("waveFrequency", FieldFlags);
        private static readonly FieldInfo VariationField = typeof(PixelWaterGPU).GetField("waveVerticalVariation", FieldFlags);
        private static readonly FieldInfo BigScaleField = typeof(PixelWaterGPU).GetField("bigWaveScale", FieldFlags);
        private static readonly FieldInfo DeepColorField = typeof(PixelWaterGPU).GetField("deepWaterColor", FieldFlags);
        private static readonly FieldInfo MainColorField = typeof(PixelWaterGPU).GetField("mainWaterColor", FieldFlags);
        private static readonly FieldInfo SurfaceColorField = typeof(PixelWaterGPU).GetField("surfaceWaterColor", FieldFlags);

        // Day 3 changes existing simulations only. It never adds grids or increases resolution.
        private const float WaterUpdateInterval = 0.125f; // 8 updates per second is visually smooth and inexpensive.
        private const int MaximumReturningThreats = 5;

        private readonly Dictionary<PixelWaterGPU, WaterState> waterStates = new();
        private readonly List<GameObject> spawnedHolders = new();

        private SurfDayProgressionDirector director;
        private ProceduralStarryNight sky;
        private ProceduralRainSystem rain;
        private ShadowSurferEcho shadow;
        private float nextEnemyAt;
        private float nextAnomalyAt;
        private float anomalyStartedAt;
        private float anomalyUntil;
        private float anomalyOffset;
        private float displayedSkyTime;
        private float skyTimeVelocity;
        private bool skyTimeInitialised;
        private float nextWaveShiftAt;
        private float waveShiftUntil;
        private float waveIntensity = 1f;
        private float nextWaterUpdateAt;
        private bool active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<DayThreeOceanRemembers>() != null)
                return;
            new GameObject("Day 3 - The Ocean Remembers").AddComponent<DayThreeOceanRemembers>();
        }

        private void Update()
        {
            if (director == null) director = FindFirstObjectByType<SurfDayProgressionDirector>();
            bool shouldBeActive = director != null && director.CurrentDay == 3 &&
                                  director.CurrentChapter != SurfDayProgressionDirector.Chapter.Complete;

            if (shouldBeActive && !active) BeginDayThree();
            else if (!shouldBeActive && active) EndDayThree();
            if (!active) return;

            if (sky == null) sky = FindFirstObjectByType<ProceduralStarryNight>();
            if (rain == null) rain = FindFirstObjectByType<ProceduralRainSystem>();
            EnsureShadow();
            UpdateEnemyRemix();
            UpdateWaveCorruption();
            UpdateWeather();
        }

        private void LateUpdate()
        {
            if (!active || sky == null || director == null)
                return;

            float baseProgress = director.DayDuration > 0f
                ? Mathf.Clamp01(director.RunTime / director.DayDuration)
                : 0f;
            float normalTime = Mathf.Repeat(0.25f + baseProgress * 0.75f, 1f);

            if (!skyTimeInitialised)
            {
                displayedSkyTime = sky.TimeOfDay;
                skyTimeVelocity = 0f;
                skyTimeInitialised = true;
            }

            if (Time.unscaledTime >= nextAnomalyAt && Time.unscaledTime >= anomalyUntil)
            {
                anomalyStartedAt = Time.unscaledTime;
                anomalyUntil = anomalyStartedAt + UnityEngine.Random.Range(7f, 14f);
                nextAnomalyAt = anomalyUntil + UnityEngine.Random.Range(14f, 30f);

                float[] offsets = { 0.18f, -0.22f, 0.42f, -0.40f };
                anomalyOffset = offsets[UnityEngine.Random.Range(0, offsets.Length)];
            }

            float anomaly = 0f;
            if (Time.unscaledTime < anomalyUntil)
            {
                const float anomalyBlendDuration = 2.5f;
                float fadeIn = Mathf.Clamp01(
                    (Time.unscaledTime - anomalyStartedAt) / anomalyBlendDuration);
                float fadeOut = Mathf.Clamp01(
                    (anomalyUntil - Time.unscaledTime) / anomalyBlendDuration);
                float envelope = Mathf.SmoothStep(0f, 1f, Mathf.Min(fadeIn, fadeOut));
                anomaly = anomalyOffset * envelope;
            }

            float nervousPulse = Mathf.Sin(Time.unscaledTime * 0.19f) * 0.018f;
            float targetSkyTime = Mathf.Repeat(normalTime + anomaly + nervousPulse, 1f);

            // Smooth in degrees so crossing midnight follows the shortest circular route
            // instead of snapping from values near 1 back to values near 0.
            float smoothedDegrees = Mathf.SmoothDampAngle(
                displayedSkyTime * 360f,
                targetSkyTime * 360f,
                ref skyTimeVelocity,
                1.65f,
                90f,
                Time.unscaledDeltaTime);

            displayedSkyTime = Mathf.Repeat(smoothedDegrees / 360f, 1f);
            sky.SetTimeOfDay(displayedSkyTime);
        }


        private void BeginDayThree()
        {
            active = true;
            sky = FindFirstObjectByType<ProceduralStarryNight>();
            rain = FindFirstObjectByType<ProceduralRainSystem>();
            CaptureWater();
            nextEnemyAt = Time.unscaledTime + 7f;
            nextAnomalyAt = Time.unscaledTime + 12f;
            anomalyStartedAt = 0f;
            anomalyUntil = 0f;
            anomalyOffset = 0f;
            displayedSkyTime = sky != null ? sky.TimeOfDay : 0.25f;
            skyTimeVelocity = 0f;
            skyTimeInitialised = true;
            nextWaveShiftAt = Time.unscaledTime + 6f;
            nextWaterUpdateAt = Time.unscaledTime;
            EnsureShadow();
        }

        private void EndDayThree()
        {
            active = false;
            skyTimeInitialised = false;
            skyTimeVelocity = 0f;
            RestoreWater();
            if (shadow != null) Destroy(shadow.gameObject);
            shadow = null;
            foreach (GameObject holder in spawnedHolders)
                if (holder != null) Destroy(holder);
            spawnedHolders.Clear();
        }

        private void EnsureShadow()
        {
            if (shadow != null) return;
            TinyWaveSurfer player = FindFirstObjectByType<TinyWaveSurfer>();
            if (player == null || player.IsDead) return;

            shadow = CreateShadow(player, "Shadow Surfer - The Ocean Remembers", -1f, 1.10f, 0.12f, 0f);
        }

        private ShadowSurferEcho CreateShadow(
            TinyWaveSurfer player,
            string objectName,
            float sideSign,
            float followDistance,
            float verticalOffset,
            float phaseOffset)
        {
            GameObject go = new(objectName);
            ShadowSurferEcho echo = go.AddComponent<ShadowSurferEcho>();
            echo.Initialise(player, director);
            echo.ConfigureFollower(sideSign, followDistance, verticalOffset, phaseOffset);
            return echo;
        }

        private void UpdateEnemyRemix()
        {
            if (Time.unscaledTime < nextEnemyAt) return;

            int livingThreats = FindObjectsByType<SharkLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                                FindObjectsByType<GiantSquidLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                                FindObjectsByType<BloodSharkLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                                FindObjectsByType<TransparentSquidLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length +
                                FindObjectsByType<StingrayLaneSwimmer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;

            if (livingThreats < MaximumReturningThreats)
                SpawnRandomReturningEnemy();

            float chapterPressure = director.CurrentChapter >= SurfDayProgressionDirector.Chapter.Storm ? 0.78f : 1f;
            nextEnemyAt = Time.unscaledTime + UnityEngine.Random.Range(12f, 24f) * chapterPressure;
        }

        private void SpawnRandomReturningEnemy()
        {
            GameObject holder = new GameObject("Day 3 Memory Spawn");
            spawnedHolders.Add(holder);

            switch (UnityEngine.Random.Range(0, 8))
            {
                case 0: holder.AddComponent<SharkLaneSpawner>().SpawnShark(true); break;
                case 1: holder.AddComponent<GiantSquidLaneSpawner>().SpawnSquid(true); break;
                case 2: holder.AddComponent<WhaleLaneSpawner>().SpawnWhale(true); break;
                case 3: holder.AddComponent<BloodSharkLaneSpawner>().SpawnBloodShark(true); break;
                case 4: holder.AddComponent<TransparentSquidLaneSpawner>().SpawnTransparentSquid(true); break;
                case 5: holder.AddComponent<StingrayLaneSpawner>().SpawnStingray(true); break;
                case 6: holder.AddComponent<JellyfishSchoolSpawner>().SpawnSchool(); break;
                default: holder.AddComponent<BloodfishSchoolSpawner>().SpawnSchool(); break;
            }
        }

        private void CaptureWater()
        {
            waterStates.Clear();
            foreach (PixelWaterGPU water in FindObjectsByType<PixelWaterGPU>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (water == null) continue;
                waterStates[water] = new WaterState
                {
                    Horizontal = ReadFloat(water, HorizontalForceField),
                    Vertical = ReadFloat(water, VerticalForceField),
                    Frequency = ReadFloat(water, FrequencyField),
                    Variation = ReadFloat(water, VariationField),
                    BigScale = ReadFloat(water, BigScaleField),
                    Deep = ReadColor(water, DeepColorField),
                    Main = ReadColor(water, MainColorField),
                    Surface = ReadColor(water, SurfaceColorField)
                };
            }
        }

        private void UpdateWaveCorruption()
        {
            if (waterStates.Count == 0) CaptureWater();

            if (Time.unscaledTime < nextWaterUpdateAt)
                return;
            nextWaterUpdateAt = Time.unscaledTime + WaterUpdateInterval;

            if (Time.unscaledTime >= nextWaveShiftAt)
            {
                waveShiftUntil = Time.unscaledTime + UnityEngine.Random.Range(5f, 12f);
                nextWaveShiftAt = waveShiftUntil + UnityEngine.Random.Range(6f, 16f);
                waveIntensity = UnityEngine.Random.Range(0, 5) == 0
                    ? 0.20f // false calm
                    : UnityEngine.Random.Range(0.75f, director.CurrentChapter >= SurfDayProgressionDirector.Chapter.Storm ? 2.1f : 1.55f);
            }

            float target = Time.unscaledTime < waveShiftUntil ? waveIntensity : 1f;
            float chapterDarkness = Mathf.InverseLerp((int)SurfDayProgressionDirector.Chapter.Dawn,
                (int)SurfDayProgressionDirector.Chapter.FinalWave, (int)director.CurrentChapter);

            foreach (KeyValuePair<PixelWaterGPU, WaterState> pair in waterStates)
            {
                PixelWaterGPU water = pair.Key;
                WaterState original = pair.Value;
                if (water == null) continue;

                float pulse = target * (
                    1f +
                    Mathf.Sin(
                        Time.unscaledTime * 0.48f +
                        water.transform.position.x * 0.07f) * 0.10f);
                        
                WriteFloat(water, HorizontalForceField, Mathf.Clamp(original.Horizontal * pulse, 3f, 38f));
                WriteFloat(water, VerticalForceField, Mathf.Clamp(original.Vertical * Mathf.Lerp(0.8f, 1.7f, pulse / 2.1f), 1f, 28f));
                WriteFloat(water, FrequencyField, Mathf.Clamp(original.Frequency * Mathf.Lerp(0.72f, 1.48f, pulse / 2.1f), 0.06f, 2.5f));
                WriteFloat(water, VariationField, Mathf.Clamp(original.Variation + chapterDarkness * 0.55f, 0f, 2.5f));
                WriteFloat(water, BigScaleField, Mathf.Clamp(original.BigScale * Mathf.Lerp(1f, 1.65f, chapterDarkness), 0.5f, 4.5f));

                Color deepTarget = new Color(0.004f, 0.025f, 0.045f, original.Deep.a);
                Color mainTarget = new Color(0.008f, 0.12f, 0.16f, original.Main.a);
                Color surfaceTarget = new Color(0.035f, 0.28f, 0.30f, original.Surface.a);
                WriteColor(water, DeepColorField, Color.Lerp(original.Deep, deepTarget, 0.35f + chapterDarkness * 0.55f));
                WriteColor(water, MainColorField, Color.Lerp(original.Main, mainTarget, 0.30f + chapterDarkness * 0.58f));
                WriteColor(water, SurfaceColorField, Color.Lerp(original.Surface, surfaceTarget, 0.25f + chapterDarkness * 0.55f));
            }
        }

        private void UpdateWeather()
        {
            if (rain == null) return;
            if (director.CurrentChapter >= SurfDayProgressionDirector.Chapter.Storm)
                rain.SetSituation(ProceduralRainSystem.RainSituation.HeavyRain);
            else if (director.CurrentChapter >= SurfDayProgressionDirector.Chapter.StrangeTide)
                rain.SetSituation(ProceduralRainSystem.RainSituation.SteadyRain);
        }

        private void RestoreWater()
        {
            foreach (KeyValuePair<PixelWaterGPU, WaterState> pair in waterStates)
            {
                if (pair.Key == null) continue;
                WriteFloat(pair.Key, HorizontalForceField, pair.Value.Horizontal);
                WriteFloat(pair.Key, VerticalForceField, pair.Value.Vertical);
                WriteFloat(pair.Key, FrequencyField, pair.Value.Frequency);
                WriteFloat(pair.Key, VariationField, pair.Value.Variation);
                WriteFloat(pair.Key, BigScaleField, pair.Value.BigScale);
                WriteColor(pair.Key, DeepColorField, pair.Value.Deep);
                WriteColor(pair.Key, MainColorField, pair.Value.Main);
                WriteColor(pair.Key, SurfaceColorField, pair.Value.Surface);
            }
            waterStates.Clear();
        }

        private static float ReadFloat(object target, FieldInfo field) =>
            field != null ? (float)(field.GetValue(target) ?? 0f) : 0f;
        private static Color ReadColor(object target, FieldInfo field) =>
            field != null ? (Color)(field.GetValue(target) ?? Color.white) : Color.white;
        private static void WriteFloat(object target, FieldInfo field, float value)
        {
            if (target != null && field != null) field.SetValue(target, value);
        }
        private static void WriteColor(object target, FieldInfo field, Color value)
        {
            if (target != null && field != null) field.SetValue(target, value);
        }
    }

    [DisallowMultipleComponent]
    public sealed class ShadowSurferEcho : MonoBehaviour
    {
        private enum Motion
        {
            Idle,
            Move,
            Jump,
            SurfJump,
            Handstand,
            Rotation,
            Flip,
            Skid,
            Death
        }

        private static readonly int IdleStateHash = Animator.StringToHash("Idle");
        private static readonly int MoveStateHash = Animator.StringToHash("chuck_move");
        private static readonly int JumpStateHash = Animator.StringToHash("chuck_jump");
        private static readonly int WaveSwitchStateHash = Animator.StringToHash("chuck_wave_switch");
        private static readonly int SurfJumpStateHash = Animator.StringToHash("chuck_surf_jump");
        private static readonly int HandstandStateHash = Animator.StringToHash("chuck_handstand");
        private static readonly int RotationStateHash = Animator.StringToHash("chuck_rotation");
        private static readonly int FlipStateHash = Animator.StringToHash("chuck_flip");
        private static readonly int DeathStateHash = Animator.StringToHash("chuck_death");
        private static readonly int ProneStateHash = Animator.StringToHash("chuck_prone");

        private TinyWaveSurfer player;
        private SurfDayProgressionDirector director;
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer playerSpriteRenderer;
        private InterWaveRenderItem renderItem;
        private readonly Dictionary<Motion, Sprite[]> frames = new();
        private Vector3 velocity;
        private float phase;
        private float sideSign = -1f;
        private float normalFollowDistance = 1.1f;
        private float followerVerticalOffset = 0.12f;
        private float shadowWaterGlideAmount = 0.10f;
        private float shadowWaterGlideFrequency = 0.26f;
        private bool underwater;
        private Coroutine underwaterRoutine;
        private float nextUnderwaterAt;
        private float underwaterDepthOffset;
        private float shadowVerticalVelocity;
        private float smoothedShadowY;
        private bool smoothedShadowYInitialised;

        [SerializeField, Range(0.05f, 0.6f)] private float shadowVerticalSmoothTime = 0.18f;
        [SerializeField, Range(1f, 24f)] private float shadowMaximumVerticalSpeed = 7f;
        private int appliedLane = -1;
        private Motion currentMotion = Motion.Idle;

        public void Initialise(TinyWaveSurfer target, SurfDayProgressionDirector dayDirector)
        {
            player = target;
            director = dayDirector;
            phase = UnityEngine.Random.Range(0f, 10f);
            transform.position = target.transform.position + new Vector3(-3.2f, 0.15f, 0f);

            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.color = Color.white;
            playerSpriteRenderer = target.GetComponent<SpriteRenderer>();
            ApplyWithinLaneSorting();

            renderItem = gameObject.AddComponent<InterWaveRenderItem>();
            appliedLane = Mathf.Max(0, target.CurrentWaveIndex);
            renderItem.SetLane(appliedLane);

            Load(Motion.Idle, "Shadow/shadow_idle");
            Load(Motion.Move, "Shadow/shadow_move");
            Load(Motion.Jump, "Shadow/shadow_jump");
            Load(Motion.SurfJump, "Shadow/shadow_surf_jump");
            Load(Motion.Handstand, "Shadow/shadow_handstand");
            Load(Motion.Rotation, "Shadow/shadow_rotation");
            Load(Motion.Flip, "Shadow/shadow_flip");
            Load(Motion.Skid, "Shadow/shadow_skid");
            Load(Motion.Death, "Shadow/shadow_death");

            ApplyFrame(Motion.Idle, 0f);
            nextUnderwaterAt = Time.unscaledTime + UnityEngine.Random.Range(7f, 12f);
        }

        public void ConfigureFollower(
            float configuredSideSign,
            float followDistance,
            float verticalOffset,
            float phaseOffset)
        {
            sideSign = Mathf.Sign(Mathf.Approximately(configuredSideSign, 0f) ? -1f : configuredSideSign);
            normalFollowDistance = Mathf.Max(0.2f, followDistance);
            followerVerticalOffset = verticalOffset;
            phase += phaseOffset;
        }

        private void Load(Motion key, string path)
        {
            Sprite[] loaded = Resources.LoadAll<Sprite>(path);
            Array.Sort(loaded, (a, b) => ExtractFrameNumber(a.name).CompareTo(ExtractFrameNumber(b.name)));
            frames[key] = loaded;
        }

        private static int ExtractFrameNumber(string name)
        {
            int split = name.LastIndexOf('_');
            return split >= 0 && int.TryParse(name.Substring(split + 1), out int value)
                ? value
                : 0;
        }

        private void Update()
        {
            if (player == null)
            {
                player = FindFirstObjectByType<TinyWaveSurfer>();
                if (player == null)
                    return;

                playerSpriteRenderer = player.GetComponent<SpriteRenderer>();
                ApplyWithinLaneSorting();
            }

            SurfDayProgressionDirector.Chapter chapter = director != null
                ? director.CurrentChapter
                : SurfDayProgressionDirector.Chapter.Dawn;

            bool finalWave = chapter >= SurfDayProgressionDirector.Chapter.FinalWave;

            float side = sideSign;
            if (chapter >= SurfDayProgressionDirector.Chapter.StrangeTide && !underwater)
            {
                float crossWave = Mathf.Sin(Time.unscaledTime * 0.10f + phase);
                if (crossWave > 0.985f)
                    side = -sideSign;
            }

            float distance = normalFollowDistance;
            if (chapter >= SurfDayProgressionDirector.Chapter.Storm)
            {
                distance = Mathf.Max(0.75f, normalFollowDistance * 0.92f) +
                           Mathf.Abs(Mathf.Sin(Time.unscaledTime * 0.28f + phase)) * 0.28f;
            }

            UpdateUnderwaterBehaviour(finalWave);

            float bob = underwater
                ? Mathf.Sin(Time.unscaledTime * 0.85f + phase) * 0.035f
                : Mathf.Sin(Time.unscaledTime * 1.3f + phase) * 0.08f;

            float shadowGlide = Mathf.Sin(
                Time.time * shadowWaterGlideFrequency * Mathf.PI * 2f + phase) *
                shadowWaterGlideAmount;

            float waveCarry = player.CurrentWave != null
                ? player.CurrentWave.GetGameplayWaveVelocity(player.transform.position.x).x * 0.035f
                : 0f;

            float targetShadowY = player.transform.position.y +
                followerVerticalOffset + underwaterDepthOffset + bob;

            if (!smoothedShadowYInitialised)
            {
                smoothedShadowY = transform.position.y;
                shadowVerticalVelocity = 0f;
                smoothedShadowYInitialised = true;
            }

            smoothedShadowY = Mathf.SmoothDamp(
                smoothedShadowY,
                targetShadowY,
                ref shadowVerticalVelocity,
                shadowVerticalSmoothTime,
                shadowMaximumVerticalSpeed,
                Time.deltaTime);

            Vector3 followTarget = new Vector3(
                player.transform.position.x + side * distance + shadowGlide + waveCarry,
                smoothedShadowY,
                player.transform.position.z);

            Vector3 currentPosition = transform.position;
            float targetX = Mathf.SmoothDamp(
                currentPosition.x,
                followTarget.x,
                ref velocity.x,
                finalWave ? 0.30f : 0.27f,
                finalWave ? 7f : 7.5f,
                Time.deltaTime);

            transform.position = new Vector3(targetX, smoothedShadowY, followTarget.z);

            int targetLane = GetShadowLane(chapter);
            if (appliedLane != targetLane)
            {
                appliedLane = targetLane;
                renderItem.SetLane(appliedLane);
            }

            SyncVisualsToPlayer(chapter);
        }

        private int GetShadowLane(SurfDayProgressionDirector.Chapter chapter)
        {
            int playerLane = Mathf.Max(0, player.CurrentWaveIndex);
            int waveCount = Mathf.Max(1, player.WaveCount);

            if (chapter >= SurfDayProgressionDirector.Chapter.StrangeTide &&
                chapter < SurfDayProgressionDirector.Chapter.FinalWave)
            {
                // Use the next wave when possible.
                // At the last wave, use the previous wave instead.
                return playerLane < waveCount - 1
                    ? playerLane + 1
                    : Mathf.Max(0, playerLane - 1);
            }

            return Mathf.Clamp(playerLane, 0, waveCount - 1);
        }

        private void UpdateUnderwaterBehaviour(bool finalWave)
        {
            if (!finalWave)
            {
                if (underwaterRoutine != null)
                {
                    StopCoroutine(underwaterRoutine);
                    underwaterRoutine = null;
                }

                underwater = false;
                underwaterDepthOffset = Mathf.MoveTowards(
                    underwaterDepthOffset,
                    0f,
                    Time.deltaTime * 2.5f);

                nextUnderwaterAt = Time.unscaledTime + UnityEngine.Random.Range(7f, 12f);
                return;
            }

            if (!underwater && underwaterRoutine == null &&
                Time.unscaledTime >= nextUnderwaterAt)
            {
                underwaterRoutine = StartCoroutine(UnderwaterPassRoutine());
            }
        }

        private IEnumerator UnderwaterPassRoutine()
        {
            underwater = true;

            const float descendDuration = 0.85f;
            float elapsed = 0f;
            while (elapsed < descendDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / descendDuration));
                underwaterDepthOffset = Mathf.Lerp(0f, -1.35f, t);
                yield return null;
            }

            float submergedDuration = UnityEngine.Random.Range(3f, 5f);
            elapsed = 0f;
            while (elapsed < submergedDuration)
            {
                elapsed += Time.deltaTime;
                underwaterDepthOffset = -1.35f +
                                        Mathf.Sin(Time.time * 0.9f + phase) * 0.05f;
                yield return null;
            }

            const float riseDuration = 1.0f;
            elapsed = 0f;
            float startDepth = underwaterDepthOffset;
            while (elapsed < riseDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / riseDuration));
                underwaterDepthOffset = Mathf.Lerp(startDepth, 0f, t);
                yield return null;
            }

            underwaterDepthOffset = 0f;
            underwater = false;
            underwaterRoutine = null;
            nextUnderwaterAt = Time.unscaledTime + UnityEngine.Random.Range(8f, 15f);
        }

        private void SyncVisualsToPlayer(SurfDayProgressionDirector.Chapter chapter)
        {
            if (spriteRenderer == null || player == null)
                return;

            spriteRenderer.color = Color.white;
            ApplyWithinLaneSorting();
            transform.localScale = player.VisualLocalScale;
            transform.rotation = player.VisualRotation;

            bool hasAnimatorState = player.TryGetVisualAnimationSnapshot(
                out int stateHash,
                out float normalizedTime,
                out _,
                out bool flipX);

            spriteRenderer.flipX = flipX;

            Motion motion = ResolveMotion(stateHash, hasAnimatorState);
            currentMotion = motion;

            float predictionLead = chapter >= SurfDayProgressionDirector.Chapter.DangerousWater
                ? 0.10f
                : 0f;

            ApplyFrame(motion, normalizedTime + predictionLead);
        }

        private void ApplyWithinLaneSorting()
        {
            if (spriteRenderer == null || playerSpriteRenderer == null)
                return;

            spriteRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            spriteRenderer.sortingOrder = playerSpriteRenderer.sortingOrder;
        }

        private Motion ResolveMotion(int stateHash, bool hasAnimatorState)
        {
            if (player.IsDead || stateHash == DeathStateHash)
                return Motion.Death;

            if (player.IsVisualAirTrickActive)
            {
                if (stateHash == HandstandStateHash) return Motion.Handstand;
                if (stateHash == RotationStateHash) return Motion.Rotation;
                if (stateHash == FlipStateHash) return Motion.Flip;
            }

            if (player.IsVisualSpecialSkidding || stateHash == SurfJumpStateHash)
                return Motion.Skid;

            if (player.IsVisualObstacleJumpActive)
                return Motion.SurfJump;

            if (stateHash == WaveSwitchStateHash || stateHash == JumpStateHash)
                return Motion.Jump;

            if (stateHash == HandstandStateHash) return Motion.Handstand;
            if (stateHash == RotationStateHash) return Motion.Rotation;
            if (stateHash == FlipStateHash) return Motion.Flip;
            if (stateHash == MoveStateHash) return Motion.Move;
            if (stateHash == IdleStateHash || stateHash == ProneStateHash) return Motion.Idle;

            return hasAnimatorState ? currentMotion : Motion.Idle;
        }

        private void ApplyFrame(Motion motion, float normalizedTime)
        {
            if (!frames.TryGetValue(motion, out Sprite[] set) || set == null || set.Length == 0)
            {
                if (!frames.TryGetValue(Motion.Idle, out set) || set == null || set.Length == 0)
                    return;
            }

            bool loops = motion == Motion.Idle || motion == Motion.Move;
            float time01 = loops
                ? Mathf.Repeat(normalizedTime, 1f)
                : Mathf.Clamp01(normalizedTime);

            int index = loops
                ? Mathf.FloorToInt(time01 * set.Length) % set.Length
                : Mathf.Min(set.Length - 1, Mathf.FloorToInt(time01 * set.Length));

            spriteRenderer.sprite = set[Mathf.Clamp(index, 0, set.Length - 1)];
        }
    }

}