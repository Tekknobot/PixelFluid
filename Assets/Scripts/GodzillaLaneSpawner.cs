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

        private GameObject spawnedGodzilla;

        [ContextMenu("Spawn Godzilla Once")]
        public void SpawnGodzilla()
        {
            if (spawnedGodzilla != null)
                return;

            GodzillaLaneSwimmer existingBoss =
                BossSpawnAuthority.FindExistingBoss<GodzillaLaneSwimmer>();

            if (existingBoss != null)
            {
                spawnedGodzilla = existingBoss.gameObject;
                EnsureArena(existingBoss);
                return;
            }

            // A duck, another Reaper, or a delayed Continue spawn already owns
            // the one allowed story-boss slot. Do not start a second encounter.
            if (!BossSpawnAuthority.TryReserveSpawn())
                return;

            Sprite[] movement = LoadOrdered("Godzilla/godzilla_move");
            Sprite[] attack = LoadOrdered("Godzilla/godzilla_attack");
            if (movement.Length == 0 || attack.Length == 0)
            {
                Debug.LogError(
                    "GodzillaLaneSpawner could not load the Godzilla movement and attack sheets from Resources/Godzilla.",
                    this);
                BossSpawnAuthority.ReleaseReservation();
                return;
            }

            spawnedGodzilla = new GameObject("Godzilla - Unique Inter-Wave Swimmer");
            // Keep the active boss independent from the temporary spawner host.
            spawnedGodzilla.transform.SetParent(null, true);
            spawnedGodzilla.transform.localScale = Vector3.one * scale;

            SpriteRenderer renderer = spawnedGodzilla.AddComponent<SpriteRenderer>();
            renderer.sprite = movement[0];

            // Keep the Reaper invisible at its ordinary off-screen spawn point.
            // BossArenaPrison enables it only after relocating it inside the arena,
            // then fades every boss renderer from zero alpha.
            renderer.enabled = false;

            spawnedGodzilla.AddComponent<InterWaveRenderItem>();
            GodzillaSpriteAnimation animation = spawnedGodzilla.AddComponent<GodzillaSpriteAnimation>();
            animation.SetFrames(movement, attack);

            GodzillaLaneSwimmer swimmer = spawnedGodzilla.AddComponent<GodzillaLaneSwimmer>();
            swimmer.Initialise(startingLane);

            if (!BossSpawnAuthority.RegisterBoss(swimmer))
            {
                spawnedGodzilla = null;
                return;
            }

            EnsureArena(swimmer);
        }

        private static void EnsureArena(GodzillaLaneSwimmer swimmer)
        {
            if (swimmer == null)
                return;

            BossArenaPrison arena = BossArenaPrison.Active;
            if (arena == null)
                arena = FindFirstObjectByType<BossArenaPrison>();

            if (arena == null)
            {
                GameObject arenaHost = new GameObject("Reaper Boss Arena Prison");
                arena = arenaHost.AddComponent<BossArenaPrison>();
            }

            if (!arena.ControlsBoss(swimmer))
                arena.Configure(swimmer, BossArenaPrison.ArenaTheme.Reaper);
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
