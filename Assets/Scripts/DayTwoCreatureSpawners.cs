using System.Collections;
using UnityEngine;

namespace PixelOcean
{
    [DisallowMultipleComponent]
    public sealed class BloodSharkLaneSpawner : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingLane = 2;
        [SerializeField] private bool spawnOnStart;
        [SerializeField, Min(0.05f)] private float scale = 1f;
        private GameObject spawned;
        private IEnumerator Start() { yield return null; yield return null; if (spawnOnStart) SpawnBloodShark(); }
        public void SpawnBloodShark(bool spawnAtSectionEdge = false)
        {
            if (spawned != null) return;
            Sprite[] frames = Resources.LoadAll<Sprite>("Day2/bloodshark_move");
            if (frames == null || frames.Length == 0) { Debug.LogError("Missing Resources/Day2/bloodshark_move", this); return; }
            System.Array.Sort(frames, (a,b) => a.name.CompareTo(b.name));
            spawned = new GameObject("Blood Shark - Day 2 Predator");
            spawned.transform.SetParent(transform, false);
            spawned.transform.localScale = Vector3.one * scale;
            SpriteRenderer renderer = spawned.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0];
            spawned.AddComponent<InterWaveRenderItem>();
            DayTwoPredatorAnimation animation = spawned.AddComponent<DayTwoPredatorAnimation>();
            animation.Configure("Day2/bloodshark_move", "Day2/bloodshark_attack", 11f, 17f, 1.75f);
            BloodSharkLaneSwimmer swimmer = spawned.AddComponent<BloodSharkLaneSwimmer>();
            swimmer.Initialise(startingLane, spawnAtSectionEdge);
        }
    }

    [DisallowMultipleComponent]
    public sealed class TransparentSquidLaneSpawner : MonoBehaviour
    {
        [SerializeField, Min(0)] private int startingLane = 4;
        [SerializeField] private bool spawnOnStart;
        [SerializeField, Min(0.05f)] private float scale = 1f;
        private GameObject spawned;
        private IEnumerator Start() { yield return null; yield return null; if (spawnOnStart) SpawnTransparentSquid(); }
        public void SpawnTransparentSquid(bool spawnAtSectionEdge = false)
        {
            if (spawned != null) return;
            Sprite[] frames = Resources.LoadAll<Sprite>("Day2/transparent_squid_move");
            if (frames == null || frames.Length == 0) { Debug.LogError("Missing Resources/Day2/transparent_squid_move", this); return; }
            System.Array.Sort(frames, (a,b) => a.name.CompareTo(b.name));
            spawned = new GameObject("Transparent Squid - Day 2 Ambusher");
            spawned.transform.SetParent(transform, false);
            spawned.transform.localScale = Vector3.one * scale;
            SpriteRenderer renderer = spawned.AddComponent<SpriteRenderer>(); renderer.sprite = frames[0]; renderer.color = new Color(1f,1f,1f,0.72f);
            spawned.AddComponent<InterWaveRenderItem>();
            DayTwoPredatorAnimation animation = spawned.AddComponent<DayTwoPredatorAnimation>();
            animation.Configure("Day2/transparent_squid_move", "Day2/transparent_squid_attack", 8f, 15f, 1.9f);
            TransparentSquidLaneSwimmer swimmer = spawned.AddComponent<TransparentSquidLaneSwimmer>();
            swimmer.Initialise(startingLane, spawnAtSectionEdge);
        }
    }
}
