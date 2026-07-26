Shader "PixelOcean/GPU Particle Sand"
{
    Properties
    {
        _DrySandColor ("Dry Sand", Color) = (0.88, 0.77, 0.52, 1)
        _WetSandColor ("Wet Sand", Color) = (0.56, 0.43, 0.24, 1)
        _DeepSandColor ("Deep Sand", Color) = (0.25, 0.20, 0.11, 1)
        _GrainSize ("Grain Size", Float) = 0.028
        _ColourVariation ("Colour Variation", Range(0,1)) = 0.22
    }

    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct SandParticle
            {
                float2 position;
                float random;
                float depth;
            };

            StructuredBuffer<SandParticle> _SandParticles;
            float4 _DrySandColor;
            float4 _WetSandColor;
            float4 _DeepSandColor;
            float2 _TankMin;
            float2 _TankMax;
            float _GrainSize;
            float _ColourVariation;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float random : TEXCOORD1;
                float depth : TEXCOORD2;
                float height01 : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                const float2 corners[6] =
                {
                    float2(-1,-1), float2(-1,1), float2(1,1),
                    float2(-1,-1), float2(1,1), float2(1,-1)
                };

                SandParticle grain = _SandParticles[input.instanceID];
                float2 corner = corners[input.vertexID];
                float sizeVariation = lerp(0.78, 1.18, grain.random);
                float3 worldPosition = float3(grain.position + corner * _GrainSize * sizeVariation, 0.12);

                Varyings output;
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.uv = corner * 0.5 + 0.5;
                output.random = grain.random;
                output.depth = grain.depth;
                output.height01 = saturate((grain.position.y - _TankMin.y) / max(_TankMax.y - _TankMin.y, 0.001));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centred = input.uv * 2.0 - 1.0;
                float distanceFromCentre = length(centred);
                clip(1.0 - distanceFromCentre);
                float edge = 1.0 - smoothstep(0.68, 1.0, distanceFromCentre);

                half3 colour = lerp(_WetSandColor.rgb, _DrySandColor.rgb, smoothstep(0.20, 0.72, input.height01));
                colour = lerp(colour, _DeepSandColor.rgb, input.depth * 0.78);
                float variation = (input.random - 0.5) * 2.0 * _ColourVariation;
                colour *= 1.0 + variation;

                return half4(saturate(colour), edge);
            }
            ENDHLSL
        }
    }
}
