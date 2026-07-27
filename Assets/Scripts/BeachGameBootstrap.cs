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

            HeartLaneSpawner heartSpawner =
                Object.FindAnyObjectByType<HeartLaneSpawner>();

            if (heartSpawner == null)
            {
                heartSpawner = water.gameObject.AddComponent<HeartLaneSpawner>();
                Debug.Log("Installed animated heart in the middle inter-wave lane.", water);
            }
        }
    }
}
