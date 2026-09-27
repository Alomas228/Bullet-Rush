Shader "Custom/Bullet Rush VFX Explosion Sphere"
{
    Properties
    {
        [HDR] _Color ("Fire Color", Color) = (1, 0.5, 0.1, 0.8)
        _Intensity ("Intensity", Range(0, 8)) = 4.5
        _BodyPower ("Body Falloff", Range(0.2, 4)) = 1.5
        _BodyGain ("Body Gain", Range(0, 2)) = 1
        _RimPower ("Rim Sharpness", Range(0.5, 8)) = 2.5
        _RimGain ("Rim Gain", Range(0, 3)) = 1.1
        _CorePower ("Core Tightness", Range(1, 16)) = 7
        _CoreGain ("Core Gain", Range(0, 4)) = 1.5
        _CoreSharp ("Core Whiteness", Range(0, 6)) = 2.5
        _NoiseScale ("Noise Scale", Range(0.5, 8)) = 2.4
        _NoiseAmount ("Noise Amount", Range(0, 0.6)) = 0.2
        _NoiseSpeed ("Noise Speed", Range(0, 3)) = 1.1
        _Bulge ("Surface Bulge", Range(0, 0.8)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off
            ColorMask RGB

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "VfxNoise.hlsl"
            #include "ExplosionSphereVfx.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                float _BodyPower;
                float _BodyGain;
                float _RimPower;
                float _RimGain;
                float _CorePower;
                float _CoreGain;
                float _CoreSharp;
                float _NoiseScale;
                float _NoiseAmount;
                float _NoiseSpeed;
                float _Bulge;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS  : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                half4  color       : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Сфера подрагивает по шуму: из идеального шара
                // получается кипящий пузырь. Стоит 150 вершин и
                // ни одного лишнего draw call, а статичная сфера
                // сразу читается как геометрическая фигура.
                float bulge = VfxValueNoise(
                    input.positionOS.xz * 3.4f +
                    float2(_Time.y * 0.7f, -_Time.y * 0.5f)
                );

                float3 positionOS =
                    input.positionOS.xyz *
                    (1.0f + (bulge - 0.5f) * _Bulge);

                output.positionHCS =
                    TransformObjectToHClip(positionOS);
                output.positionOS = input.positionOS.xyz;
                output.positionWS =
                    TransformObjectToWorld(positionOS);
                output.color = input.color;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return ExplosionSphereFragment(
                    input.positionOS,
                    input.positionWS,
                    input.color,
                    _Color,
                    _BodyPower,
                    _BodyGain,
                    _RimPower,
                    _RimGain,
                    _CorePower,
                    _CoreGain,
                    _CoreSharp,
                    _NoiseScale,
                    _NoiseAmount,
                    _Intensity,
                    _Time.y * _NoiseSpeed
                );
            }
            ENDHLSL
        }
    }

    Fallback Off
}
