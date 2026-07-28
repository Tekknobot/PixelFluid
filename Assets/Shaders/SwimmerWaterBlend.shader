Shader "PixelOcean/SwimmerWaterBlend"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Sprite Tint", Color) = (1,1,1,1)
        _DeepColor ("Deep Water", Color) = (0.0,0.16,0.42,1)
        _SurfaceColor ("Surface Water", Color) = (0.25,0.78,0.74,1)
        _BlendStrength ("Water Blend", Range(0,1)) = 0.46
        _OriginalColor ("Original Colour", Range(0,1)) = 0.58
        _WaterShimmer ("Water Shimmer", Range(0,0.25)) = 0.06
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _DeepColor;
                float4 _SurfaceColor;
                float _BlendStrength;
                float _OriginalColor;
                float _WaterShimmer;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half luminance = dot(tex.rgb, half3(0.299h, 0.587h, 0.114h));
                half3 waterRamp = lerp(_DeepColor.rgb, _SurfaceColor.rgb, saturate(luminance * 1.15h));
                half shimmer = sin((input.positionWS.x + input.positionWS.y) * 18.0h + _Time.y * 5.0h) * _WaterShimmer;
                waterRamp += shimmer;
                half3 retained = lerp(waterRamp, tex.rgb, _OriginalColor);
                half3 finalRgb = lerp(tex.rgb, retained, _BlendStrength);
                return half4(finalRgb * input.color.rgb, tex.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
