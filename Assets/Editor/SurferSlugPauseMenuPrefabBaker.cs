#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelOcean.Editor
{
    public static class SurferSlugPauseMenuPrefabBaker
    {
        private const string PrefabFolder = "Assets/Prefabs";
        private const string PrefabPath = PrefabFolder + "/SurferSlugPauseMenu.prefab";

        [MenuItem("Surfer Slug/UI/Save Live Pause Menu As Prefab", priority = 100)]
        private static void SaveLiveMenuAsPrefab()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Play Mode Required",
                    "Enter Play Mode, open the pause menu once, then use this command again.",
                    "OK");
                return;
            }

            SurferSlugPauseMenu menu = Object.FindFirstObjectByType<SurferSlugPauseMenu>();
            if (menu == null)
            {
                EditorUtility.DisplayDialog(
                    "Pause Menu Not Found",
                    "No live SurferSlugPauseMenu exists in the current scene.",
                    "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            PrefabUtility.SaveAsPrefabAsset(menu.gameObject, PrefabPath, out bool success);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!success)
            {
                EditorUtility.DisplayDialog(
                    "Prefab Save Failed",
                    "Unity could not save the live menu prefab.",
                    "OK");
                return;
            }

            Object prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            EditorUtility.DisplayDialog(
                "Pause Menu Prefab Saved",
                "Saved to:\n" + PrefabPath +
                "\n\nYou can now stop Play Mode and drag this prefab into future scenes. " +
                "The automatic bootstrap will detect it and will not create a duplicate.",
                "OK");
        }

        [MenuItem("Surfer Slug/UI/Save Live Pause Menu As Prefab", true)]
        private static bool ValidateSaveLiveMenuAsPrefab()
        {
            return !EditorApplication.isCompiling;
        }
    }
}
#endif
