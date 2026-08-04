using System.Collections;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class RubberDuckBossSpawner : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingLane = 2;
        [SerializeField, Min(0.05f)] private float scale = 0.78f;
        [SerializeField, Min(0.1f)] private float worldReadyTimeout = 3f;
        private GameObject spawnedBoss;
        private Coroutine spawnRoutine;


        [ContextMenu("Spawn Giant Rubber Duck Boss")]
        public void SpawnRubberDuckBoss()
        {
            if (spawnRoutine != null)
                return;

            spawnRoutine = StartCoroutine(SpawnBossWhenWorldReady());
        }

        private IEnumerator SpawnBossWhenWorldReady()
        {
            float timeout = Mathf.Max(0.1f, worldReadyTimeout);

            while (timeout > 0f)
            {
                TinyWaveSurfer player = FindFirstObjectByType<TinyWaveSurfer>();
                PixelWaterGPU[] water = FindObjectsByType<PixelWaterGPU>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);

                if (player != null && water.Length >= 2 && Camera.main != null)
                    break;

                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            RubberDuckBossSwimmer existingBoss =
                FindFirstObjectByType<RubberDuckBossSwimmer>();

            if (existingBoss != null)
            {
                spawnedBoss = existingBoss.gameObject;
                EnsureArena(existingBoss);
                spawnRoutine = null;
                yield break;
            }

            Sprite[] movement = LoadOrdered("RubberDuck/rubber_duck_move");
            Sprite[] attack = LoadOrdered("RubberDuck/rubber_duck_attack");
            if (movement.Length == 0 || attack.Length == 0)
            {
                Debug.LogError(
                    "RubberDuckBossSpawner could not load Resources/RubberDuck sprite sheets.",
                    this);
                spawnRoutine = null;
                yield break;
            }

            spawnedBoss = new GameObject("Giant Rubber Duck - Day 2 Boss");

            // Keep the boss independent from the temporary spawner host. Developer
            // cleanup and progression can destroy the spawner without taking the
            // active boss with it.
            spawnedBoss.transform.SetParent(null, true);
            spawnedBoss.transform.localScale = Vector3.one * scale;

            SpriteRenderer renderer = spawnedBoss.AddComponent<SpriteRenderer>();
            renderer.sprite = movement[0];

            spawnedBoss.AddComponent<InterWaveRenderItem>();

            RubberDuckBossAnimation animation =
                spawnedBoss.AddComponent<RubberDuckBossAnimation>();
            animation.SetFrames(movement, attack);

            RubberDuckBossSwimmer swimmer =
                spawnedBoss.AddComponent<RubberDuckBossSwimmer>();
            swimmer.Initialise(startingLane);

            // Let Initialise finish its off-screen placement and Rigidbody setup
            // before the arena captures and relocates the boss.
            yield return null;

            if (swimmer != null)
                EnsureArena(swimmer);

            spawnRoutine = null;
        }

        private static void EnsureArena(RubberDuckBossSwimmer swimmer)
        {
            if (swimmer == null)
                return;

            BossArenaPrison activeArena = BossArenaPrison.Active;
            if (activeArena != null)
                return;

            GameObject arenaHost =
                new GameObject("Rubber Duck Boss Arena Prison");

            BossArenaPrison arena =
                arenaHost.AddComponent<BossArenaPrison>();

            arena.Configure(
                swimmer,
                BossArenaPrison.ArenaTheme.RubberDuck);
        }

        private static Sprite[] LoadOrdered(string path) => Resources.LoadAll<Sprite>(path)
            .OrderBy(sprite =>
            {
                int separator = sprite.name.LastIndexOf('_');
                return separator >= 0 && int.TryParse(sprite.name[(separator + 1)..], out int number)
                    ? number : int.MaxValue;
            }).ToArray();
    }
}
