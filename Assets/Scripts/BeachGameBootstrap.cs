using UnityEngine;

namespace PixelOcean
{
    public static class BeachGameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Object.FindAnyObjectByType<PixelWaterGPU>() == null ||
                Object.FindAnyObjectByType<BeachGameController>() != null)
                return;

            GameObject root = new("Beach Game Prototype");
            root.AddComponent<BeachGameController>();
        }
    }
}
