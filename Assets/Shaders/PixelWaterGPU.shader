Shader "PixelOcean/GPU Particle Water"
{
    Properties
    {
        _DeepWaterColor ("Deep Water", Color) = (0.01, 0.10, 0.32, 0.96)
        _MainWaterColor ("Main Water", Color) = (0.02, 0.38, 0.90, 0.96)
        _SurfaceWaterColor ("Surface Water", Color) = (0.10, 0.75, 1.00, 0.98)
        _FoamColor ("Foam", Color) = (0.95, 0.99, 1.00, 1.00)
        _ShallowWaterColor ("Shallow Water", Color) = (0.26, 0.92, 0.82, 0.98)
        _ShoreStart ("Shore Start", Range(0,1)) = 0.68
        _ShallowZoneWidth ("Shallow Zone Width", Range(0.05,0.45)) = 0.24
        _ParticleSize ("Particle Size", Float) = 0.035
        _LayerDepthOffset ("Layer Depth Offset", Float) = 0
        _SurfaceBand ("Surface Band", Range(0,1)) = 0.38
        _ColourBrightness ("Colour Brightness", Range(0,2)) = 1
        _FoamRenderStrength ("Foam Strength", Range(0,2)) = 1
        _EdgeSoftness ("Edge Softness", Range(0,1)) = 0.28
        _FoamBottomSuppression ("Foam Bottom Suppression", Float) = 0.55
        _FoamSurfaceDensity ("Foam Surface Density", Range(0,1)) = 0.62
        [HideInInspector] _RenderBandEnabled ("Render Band Enabled", Float) = 0
        [HideInInspector] _RenderBandMinY ("Render Band Minimum Y", Float) = -10000
        [HideInInspector] _RenderBandMaxY ("Render Band Maximum Y", Float) = 10000
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
            float4 _ShallowWaterColor;
            float2 _TankMin;
            float2 _TankMax;
            float _ParticleSize;
            float _LayerDepthOffset;
            float _SurfaceBand;
            float _ColourBrightness;
            float _FoamRenderStrength;
            float _EdgeSoftness;
            float _FoamBottomSuppression;
            float _FoamSurfaceDensity;
            float _ShoreStart;
            float _ShallowZoneWidth;
            float _RenderBandEnabled;
            float _RenderBandMinY;
            float _RenderBandMaxY;

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
                float horizontal01 : TEXCOORD6;
                float particleWorldY : TEXCOORD7;
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
                // Independent delayed simulations render on consecutive
                // depth planes behind the master wave.
                float3 worldPosition = float3(
                    particle.position + corner * _ParticleSize,
                    _LayerDepthOffset);

                Varyings output;
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.uv = corner * 0.5 + 0.5;
                output.height01 = saturate((particle.position.y - _TankMin.y) / max(_TankMax.y - _TankMin.y, 0.001));
                output.heightAboveBottom = particle.position.y - _TankMin.y;
                output.speed01 = saturate(length(particle.velocity) / 12.0);
                output.density = particle.density;
                output.foam = particle.foam;
                output.horizontal01 = saturate((particle.position.x - _TankMin.x) / max(_TankMax.x - _TankMin.x, 0.001));
                output.particleWorldY = particle.position.y;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                if (_RenderBandEnabled > 0.5)
                {
                    clip(input.particleWorldY - _RenderBandMinY);
                    clip(_RenderBandMaxY - input.particleWorldY);
                }

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

                // Tropical shallows begin over the final third of the tank and widen gradually.
                float shoreMask = smoothstep(_ShoreStart - _ShallowZoneWidth, _ShoreStart + _ShallowZoneWidth, input.horizontal01);
                float shallowDepthMask = smoothstep(0.08, 0.72, input.height01);
                float shallowBlend = shoreMask * shallowDepthMask;
                colour = lerp(colour, _ShallowWaterColor.rgb, shallowBlend * 0.82);

                // Clear tropical water is brighter at the surface but remains dark at depth.
                colour += input.speed01 * 0.08 + (1.0 - input.density) * input.speed01 * 0.10;
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
