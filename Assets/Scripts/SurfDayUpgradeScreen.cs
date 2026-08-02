using System.Collections;
using UnityEngine;

namespace PixelOcean
{
    [DefaultExecutionOrder(-11850)]
    public sealed class SurfDayUpgradeScreen : MonoBehaviour
    {
        public static SurfDayUpgradeScreen Instance { get; private set; }
        private bool visible;
        private bool chosen;
        private GUIStyle titleStyle, buttonStyle, detailStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<SurfDayUpgradeScreen>() != null) return;
            GameObject host = new("Surf Day Upgrade Screen");
            DontDestroyOnLoad(host);
            host.AddComponent<SurfDayUpgradeScreen>();
        }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }
        public IEnumerator ShowAndWait()
        {
            visible = true; chosen = false;
            float oldScale = Time.timeScale; Time.timeScale = 0f;
            while (!chosen) yield return null;
            visible = false; Time.timeScale = oldScale;
        }
        private void Apply(int index)
        {
            TinyWaveSurfer player = null;
            foreach (TinyWaveSurfer s in FindObjectsByType<TinyWaveSurfer>(FindObjectsSortMode.None))
                if (s != null && s.IsPlayerControlled) { player = s; break; }
            if (SurfAbilityProgression.Instance != null)
                SurfAbilityProgression.Instance.AddUpgrade(index);
            else if (player != null)
                player.ApplyDayUpgrade(index);
            chosen = true;
        }
        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 36, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            buttonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter, fontSize = 22, wordWrap = true };
            detailStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 17, wordWrap = true, normal = { textColor = new Color(0.9f,0.9f,0.9f,1f) } };
        }
        private void OnGUI()
        {
            if (!visible) return;
            EnsureStyles();
            float w = Mathf.Min(900f, Screen.width - 40f), h = 440f;
            Rect panel = new((Screen.width-w)*.5f,(Screen.height-h)*.5f,w,h);
            Color old=GUI.color; GUI.color=new Color(0f,0f,0f,.94f); GUI.Box(panel, GUIContent.none); GUI.color=old;
            GUI.Label(new Rect(panel.x+20,panel.y+20,panel.width-40,60),"CHOOSE ONE UPGRADE",titleStyle);
            string[] names={"HIGHER LAUNCH","FASTER WATER SLASH","STRONGER SKID"};
            string[] details={"Jump height +10%","Water Slash cooldown -15%","Charged skid speed +15%"};
            float gap=18f, bw=(panel.width-80f-gap*2f)/3f;
            for(int i=0;i<3;i++)
            {
                float x=panel.x+40f+i*(bw+gap);
                if(GUI.Button(new Rect(x,panel.y+115f,bw,190f),names[i],buttonStyle)) Apply(i);
                GUI.Label(new Rect(x+8,panel.y+315f,bw-16,60f),details[i],detailStyle);
            }
        }
    }
}
