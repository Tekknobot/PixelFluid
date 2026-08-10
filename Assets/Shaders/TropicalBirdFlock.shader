Shader "PixelOcean/Tropical Bird Flock"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _TropicalA ("Tropical Shadow", Color) = (0.05,0.85,0.78,1)
        _TropicalB ("Tropical Highlight", Color) = (1,0.35,0.22,1)
        _PaletteStrength ("Palette Strength", Range(0,1)) = 0.8
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment TropicalSpriteFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "UnityCG.cginc"
            #include "UnitySprites.cginc"

            fixed4 _TropicalA;
            fixed4 _TropicalB;
            float _PaletteStrength;

            fixed4 TropicalSpriteFrag(v2f IN) : SV_Target
            {
                fixed4 source = SampleSpriteTexture(IN.texcoord) * IN.color;
                float luminance = dot(source.rgb, float3(0.299, 0.587, 0.114));
                float colouredFeather = smoothstep(0.07, 0.72, luminance) * source.a;
                float warmBand = saturate(source.r * 0.78 + source.g * 0.22);
                fixed3 tropical = lerp(_TropicalA.rgb, _TropicalB.rgb, warmBand);

                // Keep dark pixel outlines intact while recolouring the brighter
                // feather pixels into a per-flock two-colour tropical palette.
                source.rgb = lerp(
                    source.rgb,
                    tropical,
                    saturate(_PaletteStrength) * colouredFeather);
                source.rgb *= source.a;
                return source;
            }
            ENDCG
        }
    }

    Fallback "Sprites/Default"
}
