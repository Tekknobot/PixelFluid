using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace PixelOcean
{
    /// <summary>
    /// Central font policy for Surfer Slug. Pixelify Sans Regular is used for
    /// supporting copy, Medium for HUD text, SemiBold for controls/buttons,
    /// and Bold for headings, scores, alerts, and speech bubbles.
    /// </summary>
    public static class PixelFontLibrary
    {
        private const string FontFolder = "Fonts/";
        private static Font regular;
        private static Font medium;
        private static Font semiBold;
        private static Font bold;
        private static TMP_FontAsset tmpRegular;
        private static TMP_FontAsset tmpMedium;
        private static TMP_FontAsset tmpSemiBold;
        private static TMP_FontAsset tmpBold;

        public static Font Regular => regular != null ? regular : regular = Load("PixeloidSans-Bold");
        public static Font Medium => medium != null ? medium : medium = Load("PixeloidSans-Bold");
        public static Font SemiBold => semiBold != null ? semiBold : semiBold = Load("PixeloidSans-Bold");
        public static Font Bold => bold != null ? bold : bold = Load("PixeloidSans-Bold");

        public static TMP_FontAsset TmpRegular => tmpRegular != null
            ? tmpRegular
            : tmpRegular = LoadTmpVariant("PixeloidSans-Bold SDF", "PixeloidSans-Bold SDF");
        public static TMP_FontAsset TmpMedium => tmpMedium != null
            ? tmpMedium
            : tmpMedium = LoadTmpVariant("PixeloidSans-Bold SDF", "PixeloidSans-Bold SDF");
        public static TMP_FontAsset TmpSemiBold => tmpSemiBold != null
            ? tmpSemiBold
            : tmpSemiBold = LoadTmpVariant("PixeloidSans-Bold SDF", "PixeloidSans-Bold SDF");
        public static TMP_FontAsset TmpBold => tmpBold != null
            ? tmpBold
            : tmpBold = LoadTmpVariant("PixeloidSans-Bold SDF", "PixeloidSans-Bold SDF");

        private static Font Load(string name)
        {
            Font loaded = Resources.Load<Font>(FontFolder + name);
            if (loaded == null)
                Debug.LogWarning($"Surfer Slug could not load {FontFolder}{name}.ttf. Unity may still be importing the new font assets.");
            return loaded;
        }

        private static TMP_FontAsset LoadTmp(string name)
        {
            TMP_FontAsset loaded = Resources.Load<TMP_FontAsset>(FontFolder + name);
            if (loaded == null)
                Debug.LogWarning($"Surfer Slug could not load {FontFolder}{name}.asset. Make sure the generated TMP font asset is inside Assets/Resources/Fonts.");
            return loaded;
        }

        private static TMP_FontAsset LoadTmpVariant(string preferredName, string fallbackName)
        {
            TMP_FontAsset preferred = Resources.Load<TMP_FontAsset>(FontFolder + preferredName);
            if (preferred != null)
                return preferred;

            return LoadTmp(fallbackName);
        }

        public static Font PickLegacy(bool heading, bool emphasized)
        {
            if (heading) return Bold;
            return emphasized ? SemiBold : Medium;
        }

        public static TMP_FontAsset PickTmp(bool heading, bool emphasized)
        {
            if (heading) return TmpBold;
            return emphasized ? TmpSemiBold : TmpMedium;
        }

        public static void Apply(Text text, bool heading = false, bool emphasized = false)
        {
            if (text == null) return;
            Font selected = PickLegacy(heading, emphasized);
            if (selected != null) text.font = selected;
            text.fontStyle = FontStyle.Normal; // Weight comes from the actual font file.
        }

        public static void Apply(TextMesh text, bool heading = false, bool emphasized = false)
        {
            if (text == null) return;
            Font selected = PickLegacy(heading, emphasized);
            if (selected == null) return;
            text.font = selected;
            text.fontStyle = FontStyle.Normal;
            MeshRenderer renderer = text.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sharedMaterial = selected.material;
        }

        public static void Apply(TMP_Text text, bool heading = false, bool emphasized = false)
        {
            if (text == null) return;
            TMP_FontAsset selected = PickTmp(heading, emphasized);
            if (selected != null) text.font = selected;
            text.fontStyle &= ~(FontStyles.Bold | FontStyles.Italic);
        }
    }

    /// <summary>Applies the project font policy to serialized UI and late-created scene text.</summary>
    [DefaultExecutionOrder(-9000)]
    public sealed class PixelFontSceneApplicator : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Object.FindFirstObjectByType<PixelFontSceneApplicator>() != null) return;
            GameObject host = new("Surfer Slug Font Applicator");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<PixelFontSceneApplicator>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(ApplyAfterFrame());
        }

        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(ApplyAfterFrame());

        private IEnumerator ApplyAfterFrame()
        {
            yield return null;
            ApplySceneFonts();
        }

        private static void ApplySceneFonts()
        {
            foreach (Text text in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string value = text.text ?? string.Empty;
                string objectName = text.gameObject.name.ToLowerInvariant();
                bool heading = text.fontSize >= 30 || objectName.Contains("title") || objectName.Contains("heading");
                bool emphasized = objectName.Contains("button") || objectName.Contains("label") || value.Length <= 18;
                PixelFontLibrary.Apply(text, heading, emphasized);
            }

            foreach (TextMesh text in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string objectName = text.gameObject.name.ToLowerInvariant();
                bool heading = objectName.Contains("speech") || objectName.Contains("saved") || text.fontSize >= 48;
                PixelFontLibrary.Apply(text, heading, !heading);
            }

            foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string objectName = text.gameObject.name.ToLowerInvariant();
                bool heading = text.fontSize >= 32 || objectName.Contains("title") || objectName.Contains("heading");
                bool emphasized = objectName.Contains("button") || objectName.Contains("label") || (text.text?.Length ?? 0) <= 18;
                PixelFontLibrary.Apply(text, heading, emphasized);
            }
        }
    }
}
