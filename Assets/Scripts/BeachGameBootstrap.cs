using UnityEngine;

namespace PixelOcean
{
    public static class BeachGameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            PixelWaterGPU water = Object.FindAnyObjectByType<PixelWaterGPU>();
            if (water == null)
                return;

            if (Object.FindAnyObjectByType<BeachGameController>() == null)
            {
                GameObject controllerRoot = new("Beach Game Prototype");
                controllerRoot.AddComponent<BeachGameController>();
            }

            // The old RandomInterWaveItemSpawner is deliberately not installed.
            // This project now spawns only the user's animated Shark prefab.
            SharkLaneSpawner sharkSpawner =
                Object.FindAnyObjectByType<SharkLaneSpawner>();

            if (sharkSpawner == null)
            {
                sharkSpawner = water.gameObject.AddComponent<SharkLaneSpawner>();
                Debug.Log("Installed single animated inter-wave shark spawner.", water);
            }

            GiantSquidLaneSpawner squidSpawner =
                Object.FindAnyObjectByType<GiantSquidLaneSpawner>();

            if (squidSpawner == null)
            {
                squidSpawner = water.gameObject.AddComponent<GiantSquidLaneSpawner>();
                Debug.Log("Installed giant squid inter-wave predator spawner.", water);
            }

            HeartLaneSpawner heartSpawner =
                Object.FindAnyObjectByType<HeartLaneSpawner>();

            if (heartSpawner == null)
            {
                heartSpawner = water.gameObject.AddComponent<HeartLaneSpawner>();
                Debug.Log("Installed animated heart in the middle inter-wave lane.", water);
            }

            StrugglingSwimmerSpawner swimmerSpawner =
                Object.FindAnyObjectByType<StrugglingSwimmerSpawner>();

            if (swimmerSpawner == null)
            {
                swimmerSpawner = water.gameObject.AddComponent<StrugglingSwimmerSpawner>();
                Debug.Log("Installed struggling swimmer rescue spawner.", water);
            }
        }
    }
}
