Shader "PixelOcean/GPU Particle Water"
{
    Properties
    {
        _DeepWaterColor ("Deep Water", Color) = (0.01, 0.10, 0.32, 0.96)
        _MainWaterColor ("Main Water", Color) = (0.02, 0.38, 0.90, 0.96)
        _SurfaceWaterColor ("Surface Water", Color) = (0.10, 0.75, 1.00, 0.98)
        _FoamColor ("Foam", Color) = (0.95, 0.99, 1.00, 1.00)
        _ParticleSize ("Particle Size", Float) = 0.035
        _SurfaceBand ("Surface Band", Range(0,1)) = 0.38
        _ColourBrightness ("Colour Brightness", Range(0,2)) = 1
        _FoamRenderStrength ("Foam Strength", Range(0,2)) = 1
        _EdgeSoftness ("Edge Softness", Range(0,1)) = 0.28
        _FoamBottomSuppression ("Foam Bottom Suppression", Float) = 0.55
        _FoamSurfaceDensity ("Foam Surface Density", Range(0,1)) = 0.62
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
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

            struct Particle
            {
                float2 position;
                float2 velocity;
                float density;
                float foam;
            };

            StructuredBuffer<Particle> _Particles;
            float4 _DeepWaterColor;
            float4 _MainWaterColor;
            float4 _SurfaceWaterColor;
            float4 _FoamColor;
            float2 _TankMin;
            float2 _TankMax;
            float _ParticleSize;
            float _SurfaceBand;
            float _ColourBrightness;
            float _FoamRenderStrength;
            float _EdgeSoftness;
            float _FoamBottomSuppression;
            float _FoamSurfaceDensity;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float height01 : TEXCOORD1;
                float heightAboveBottom : TEXCOORD2;
                float speed01 : TEXCOORD3;
                float density : TEXCOORD4;
                float foam : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                const float2 corners[6] =
                {
                    float2(-1, -1), float2(-1, 1), float2(1, 1),
                    float2(-1, -1), float2(1, 1), float2(1, -1)
                };

                Particle particle = _Particles[input.instanceID];
                float2 corner = corners[input.vertexID];
                float3 worldPosition = float3(particle.position + corner * _ParticleSize, 0.0);

                Varyings output;
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.uv = corner * 0.5 + 0.5;
                output.height01 = saturate((particle.position.y - _TankMin.y) / max(_TankMax.y - _TankMin.y, 0.001));
                output.heightAboveBottom = particle.position.y - _TankMin.y;
                output.speed01 = saturate(length(particle.velocity) / 12.0);
                output.density = particle.density;
                output.foam = particle.foam;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centred = input.uv * 2.0 - 1.0;
                float distanceFromCentre = length(centred);
                clip(1.0 - distanceFromCentre);

                float softWidth = max(0.02, _EdgeSoftness);
                float edge = 1.0 - smoothstep(1.0 - softWidth, 1.0, distanceFromCentre);

                float deepToMain = smoothstep(0.05, 0.62, input.height01);
                float surfaceStart = saturate(1.0 - _SurfaceBand);
                float mainToSurface = smoothstep(surfaceStart, 1.0, input.height01);

                half3 colour = lerp(_DeepWaterColor.rgb, _MainWaterColor.rgb, deepToMain);
                colour = lerp(colour, _SurfaceWaterColor.rgb, mainToSurface);
                colour += input.speed01 * 0.12 + (1.0 - input.density) * input.speed01 * 0.14;
                colour *= _ColourBrightness;

                float bottomMask = smoothstep(_FoamBottomSuppression * 0.45, _FoamBottomSuppression, input.heightAboveBottom);
                float surfaceExposure = saturate((_FoamSurfaceDensity - input.density) / max(_FoamSurfaceDensity, 0.001));
                surfaceExposure *= surfaceExposure;
                float foamAmount = saturate(input.foam * _FoamRenderStrength) * bottomMask * surfaceExposure;
                foamAmount *= saturate(0.25 + input.speed01 * 1.25);

                colour = lerp(colour, _FoamColor.rgb, foamAmount);

                float baseAlpha = lerp(_DeepWaterColor.a, _MainWaterColor.a, deepToMain);
                baseAlpha = lerp(baseAlpha, _SurfaceWaterColor.a, mainToSurface);
                float alpha = lerp(baseAlpha, _FoamColor.a, foamAmount) * edge;

                return half4(saturate(colour), alpha);
            }
            ENDHLSL
        }
    }
}
