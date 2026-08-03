using System;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class GodzillaLaneSpawner : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingLane = 2;
        [SerializeField, Min(0.05f)] private float scale = 0.62f;

        private static bool globalSpawned;
        private GameObject spawnedGodzilla;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            globalSpawned = false;
        }

        [ContextMenu("Spawn Godzilla Once")]
        public void SpawnGodzilla()
        {
            if (globalSpawned || spawnedGodzilla != null ||
                FindFirstObjectByType<GodzillaLaneSwimmer>() != null)
                return;

            Sprite[] movement = LoadOrdered("Godzilla/godzilla_move");
            Sprite[] attack = LoadOrdered("Godzilla/godzilla_attack");
            if (movement.Length == 0 || attack.Length == 0)
            {
                Debug.LogError(
                    "GodzillaLaneSpawner could not load the Godzilla movement and attack sheets from Resources/Godzilla.",
                    this);
                return;
            }

            spawnedGodzilla = new GameObject("Godzilla - Unique Inter-Wave Swimmer");
            spawnedGodzilla.transform.SetParent(transform, false);
            spawnedGodzilla.transform.localScale = Vector3.one * scale;

            SpriteRenderer renderer = spawnedGodzilla.AddComponent<SpriteRenderer>();
            renderer.sprite = movement[0];

            spawnedGodzilla.AddComponent<InterWaveRenderItem>();
            GodzillaSpriteAnimation animation = spawnedGodzilla.AddComponent<GodzillaSpriteAnimation>();
            animation.SetFrames(movement, attack);

            GodzillaLaneSwimmer swimmer = spawnedGodzilla.AddComponent<GodzillaLaneSwimmer>();
            swimmer.Initialise(startingLane);

            GameObject arenaHost = new GameObject("Reaper Boss Arena Prison");
            BossArenaPrison arena = arenaHost.AddComponent<BossArenaPrison>();
            arena.Configure(swimmer, BossArenaPrison.ArenaTheme.Reaper);
            globalSpawned = true;
        }

        private static Sprite[] LoadOrdered(string path)
        {
            return Resources.LoadAll<Sprite>(path)
                .OrderBy(sprite => FrameNumber(sprite.name))
                .ToArray();
        }

        private static int FrameNumber(string name)
        {
            int separator = name.LastIndexOf('_');
            return separator >= 0 && int.TryParse(name[(separator + 1)..], out int number)
                ? number
                : int.MaxValue;
        }
    }
}
