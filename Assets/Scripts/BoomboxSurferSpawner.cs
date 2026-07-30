using System.Collections;
using System.Linq;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class BoomboxSurferSpawner : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingLane = 1;
        [SerializeField, Min(0.05f)] private float scale = 1f;
        private static bool spawned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => spawned = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<BoomboxSurferSpawner>() != null)
                return;
            new GameObject("Boombox Surfer Spawner").AddComponent<BoomboxSurferSpawner>();
        }

        private IEnumerator Start()
        {
            if (FindFirstObjectByType<SurfDayProgressionDirector>() != null)
                yield break;

            float deadline = Time.realtimeSinceStartup + 12f;
            while (EndlessWaveSections.LayersNearest(0f).Count < 2 && Time.realtimeSinceStartup < deadline)
                yield return null;
            SpawnOnce();
        }

        [ContextMenu("Spawn Boombox Surfer Once")]
        public void SpawnOnce()
        {
            if (spawned || FindFirstObjectByType<BoomboxSurferSwimmer>() != null)
                return;

            Sprite[] frames = Resources.LoadAll<Sprite>("Boombox/boombox")
                .OrderBy(sprite => FrameNumber(sprite.name)).ToArray();
            AudioClip music = Resources.Load<AudioClip>("Audio/Music/Death Surfer");
            if (frames.Length == 0 || music == null)
            {
                Debug.LogError("Boombox surfer could not load its sprite frames or Death Surfer music.", this);
                return;
            }

            GameObject swimmer = new("Boombox Surfer - Death Surfer Radio");
            swimmer.transform.SetParent(transform, false);
            swimmer.transform.localScale = Vector3.one * scale;

            SpriteRenderer renderer = swimmer.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            swimmer.AddComponent<Rigidbody2D>();
            swimmer.AddComponent<InterWaveRenderItem>();
            swimmer.AddComponent<AudioSource>();
            swimmer.AddComponent<BoomboxSurferAnimation>().SetFrames(frames);
            swimmer.AddComponent<BoomboxSurferSwimmer>().Initialise(startingLane, music);
            spawned = true;
        }

        private static int FrameNumber(string name)
        {
            int separator = name.LastIndexOf('_');
            return separator >= 0 && int.TryParse(name[(separator + 1)..], out int number) ? number : int.MaxValue;
        }
    }
}
