Shader "Custom/Procedural Ground Lit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.40, 0.41, 0.42, 1)
        _TileSize ("Tile Size", Float) = 4
        _Variation ("Color Variation", Range(0, 0.05)) = 0.003
        _Smoothness ("Smoothness", Range(0, 1)) = 0.15
        _Metallic ("Metallic", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // ============================================================
        // FORWARD PASS
        // ============================================================

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            // Основной свет
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN

            // Дополнительные источники света
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)

                float4 _BaseColor;
                float _TileSize;
                float _Variation;
                float _Smoothness;
                float _Metallic;

            CBUFFER_END


            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);

                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(input.normalOS);

                output.positionHCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;

                return output;
            }


            // Псевдослучайное значение
            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);

                return frac(p.x * p.y);
            }


            half4 Frag(Varyings input) : SV_Target
            {
                // ====================================================
                // ПРОЦЕДУРНЫЙ ЦВЕТ
                // ====================================================

                float2 worldPos = input.positionWS.xz;

                float2 cell =
                    floor(worldPos / _TileSize);

                float randomValue =
                    Hash(cell);

                // Очень слабая вариация
                float variation =
                    (randomValue - 0.5) * _Variation;

                float3 albedo =
                    _BaseColor.rgb + variation;


                // ====================================================
                // NORMAL
                // ====================================================

                float3 normalWS =
                    normalize(input.normalWS);


                // ====================================================
                // ОСНОВНОЙ LIGHT
                // ====================================================

                Light mainLight =
                    GetMainLight(
                        TransformWorldToShadowCoord(
                            input.positionWS
                        )
                    );

                float NdotL =
                    saturate(
                        dot(
                            normalWS,
                            mainLight.direction
                        )
                    );


                // ====================================================
                // DIFFUSE
                // ====================================================

                float3 diffuse =
                    albedo *
                    mainLight.color *
                    NdotL *
                    mainLight.shadowAttenuation;


                // ====================================================
                // AMBIENT
                // ====================================================

                float3 ambient =
                    albedo * 0.35;


                // ====================================================
                // ИТОГ
                // ====================================================

                float3 finalColor =
                    ambient + diffuse;


                return half4(finalColor, 1.0);
            }

            ENDHLSL
        }


        // ============================================================
        // SHADOW CASTER
        // ============================================================

        Pass
        {
            Name "ShadowCaster"

            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM

            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            ENDHLSL
        }
    }
}