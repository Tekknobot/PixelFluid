using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class RubberDuckBossSpawner : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingLane = 2;
        [SerializeField, Min(0.05f)] private float scale = 0.78f;
        private GameObject spawnedBoss;


        [ContextMenu("Spawn Giant Rubber Duck Boss")]
        public void SpawnRubberDuckBoss()
        {
            if (spawnedBoss != null || FindFirstObjectByType<RubberDuckBossSwimmer>() != null)
                return;

            Sprite[] movement = LoadOrdered("RubberDuck/rubber_duck_move");
            Sprite[] attack = LoadOrdered("RubberDuck/rubber_duck_attack");
            if (movement.Length == 0 || attack.Length == 0)
            {
                Debug.LogError("RubberDuckBossSpawner could not load Resources/RubberDuck sprite sheets.", this);
                return;
            }

            spawnedBoss = new GameObject("Giant Rubber Duck - Day 2 Boss");
            spawnedBoss.transform.SetParent(transform, false);
            spawnedBoss.transform.localScale = Vector3.one * scale;
            SpriteRenderer renderer = spawnedBoss.AddComponent<SpriteRenderer>();
            renderer.sprite = movement[0];
            spawnedBoss.AddComponent<InterWaveRenderItem>();
            RubberDuckBossAnimation animation = spawnedBoss.AddComponent<RubberDuckBossAnimation>();
            animation.SetFrames(movement, attack);
            RubberDuckBossSwimmer swimmer = spawnedBoss.AddComponent<RubberDuckBossSwimmer>();
            swimmer.Initialise(startingLane);

            GameObject arenaHost = new GameObject("Rubber Duck Boss Arena Prison");
            BossArenaPrison arena = arenaHost.AddComponent<BossArenaPrison>();
            arena.Configure(swimmer, BossArenaPrison.ArenaTheme.RubberDuck);
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
