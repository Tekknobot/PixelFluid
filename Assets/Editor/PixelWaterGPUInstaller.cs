#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PixelOcean.Editor
{
    public static class PixelWaterGPUInstaller
    {
        private const string ComputePath = "Assets/Compute/PixelWaterGPU.compute";
        private const string ShaderPath = "Assets/Shaders/PixelWaterGPU.shader";
        private const string MaterialPath = "Assets/Materials/PixelWaterGPU.mat";

        [MenuItem("Tools/Pixel Ocean/Install GPU Version 2")]
        public static void Install()
        {
            ComputeShader compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

            if (compute == null || shader == null)
            {
                Debug.LogError("GPU water assets could not be found. Let Unity finish importing, then try again.");
                return;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            foreach (PixelWaterSimulation cpuSimulation in Object.FindObjectsByType<PixelWaterSimulation>(FindObjectsSortMode.None))
                cpuSimulation.gameObject.SetActive(false);

            PixelWaterGPU gpuWater = Object.FindFirstObjectByType<PixelWaterGPU>();
            if (gpuWater == null)
            {
                GameObject gameObject = new("Pixel Water GPU V2");
                gpuWater = gameObject.AddComponent<PixelWaterGPU>();
                Undo.RegisterCreatedObjectUndo(gameObject, "Install Pixel Water GPU V2");
            }

            SerializedObject serialized = new(gpuWater);
            serialized.FindProperty("simulationShader").objectReferenceValue = compute;
            serialized.FindProperty("renderingMaterial").objectReferenceValue = material;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = gpuWater.gameObject;
            EditorSceneManager.MarkSceneDirty(gpuWater.gameObject.scene);
            AssetDatabase.SaveAssets();

            Debug.Log("Pixel Ocean GPU Version 2 installed. Press Play.");
        }
    }
}
#endif
