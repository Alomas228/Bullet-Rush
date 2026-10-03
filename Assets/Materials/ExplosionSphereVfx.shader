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

        // Per-instance цвет приходит из MaterialPropertyBlock через
        // SetVectorArray и читается как instanced-свойство, тем же
        // instanceID, что и матрица.
//
// Почему не часть структуры инстанса: RenderMeshInstanced
        // вытаскивает из пользовательской структуры только
        // objectToWorld, renderingLayerMask и prevObjectToWorld, а
        // остальное игнорирует - цвет остался бы на CPU.
//
// Почему не StructuredBuffer: он требует SM 4.5 в вершинном
        // шейдере, а "#pragma target 4.5" здесь отключает инстансинг
        // и шар уходил в неинстансную ветку.
//
// Почему не instanced-свойство с MPB фиксированной длины вслепую:
// у SetVectorArray длина массива не меняется после первой записи
// ("The array length can't be changed once it has been added to the
// block"), поэтому BlastBatchBuffer всегда отдаёт массив ровно из
// 511 элементов.
        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "VfxNoise.hlsl"
            #include "ExplosionSphereVfx.hlsl"

            UNITY_INSTANCING_BUFFER_START(BlastPerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BlastColor)
            UNITY_INSTANCING_BUFFER_END(BlastPerInstance)
        ENDHLSL

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
            // БЕЗ "#pragma target 4.5": StructuredBuffer в вершинном
            // шейдере требует SM 4.5, и эта строка ломала инстансинг.
            // Цвет берётся из instanced-свойства, SM 4.5 не нужен -
            // как и в BulletTracer.shader.
            //
            // Взрывы рисуются пачкой через Graphics.RenderMeshInstanced
            // (BlastBatcher), поэтому макросы инстансинга
            // обязательны: без них TransformObjectToWorld взял бы
            // матрицу нулевого инстанса и все шары слиплись бы в
            // одну точку.
            #pragma multi_compile_instancing

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
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
                // Instance ID нужен и матрице, и индексу в буфере
                // цветов. Без этой строки все шары получили бы
                // transform нулевого инстанса и слиплись в одну точку.
                UNITY_SETUP_INSTANCE_ID(input);

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

                // Цвет и стадия остывания - per-instance, из
                // MaterialPropertyBlock. Матрица и цвет читаются
                // одним и тем же instanceID, поэтому отдельного
                // индекса и сдвига нет вовсе.
                //
                // UNITY_INSTANCING_ENABLED - корректная проверка
                // варианта. UNITY_INSTANCING_ON - имя keyword'а, а
                // не макрос для ветвления.
                #if defined(UNITY_INSTANCING_ENABLED)
                    output.color = UNITY_ACCESS_INSTANCED_PROP(
                        BlastPerInstance, _BlastColor);
                #else
                    // ЭТОТ ШАР НИКОГДА НЕ ДОЛЖЕН ПОЯВИТЬСЯ: материал
                    // рисует только BlastBatcher через
                    // RenderMeshInstanced. Если видно синий - значит
                    // инстансинг не включился и все шары слиплись бы
                    // в одну точку. У сферы нет атрибута COLOR, так
                    // что input.color здесь был бы чёрным и
                    // диагностику сломал бы.
                    output.color = float4(0.0, 0.0, 1.0, 1.0);
                #endif

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
