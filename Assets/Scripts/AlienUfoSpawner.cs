using UnityEngine;

namespace PixelOcean
{
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    public sealed class AlienUfoSpawner : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float spawnDelay = 4f;
        private bool spawned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<AlienUfoSpawner>() != null) return;
            GameObject host = new GameObject("Alien UFO Spawner");
            host.AddComponent<AlienUfoSpawner>();
        }

        private void Update()
        {
            if (FindFirstObjectByType<SurfDayProgressionDirector>() != null) return;
            if (spawned) return;
            spawnDelay -= Time.deltaTime;
            if (spawnDelay > 0f || Camera.main == null) return;
            if (FindFirstObjectByType<TinyWaveSurfer>() == null) return;

            GameObject ufo = new GameObject("Alien UFO");
            ufo.AddComponent<SpriteRenderer>();
            ufo.AddComponent<AlienUfoController>();
            spawned = true;
        }
    }
}
