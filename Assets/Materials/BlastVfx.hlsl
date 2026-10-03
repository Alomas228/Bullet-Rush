// Общий HLSL для «пузырных» слоёв взрыва: угли и дым.
//
// Один шейдер на оба слоя. Различает их не текстура и не
// отдельный файл, а несколько uniform-параметров:
//   _Additive   - светится (угли) или нет (дым);
//   _EdgePower  - мягкость края;
//   _NoiseAmount - насколько край шевелит шум.
//
// Огонь-шар живёт отдельно, в ExplosionSphereVfx: ему нужен
// настоящий объём (N·V, горячая кромка, Cull Off), а этот
// шейдер для плоских билбордов.
//
// Подключается внутри pass, а не через HLSLINCLUDE на уровне
// SubShader: этот же шейдер носит BurnFlameEffect, который
// рисуется обычным MeshRenderer без инстансинга, и путь через
// атрибут COLOR должен остаться рабочим.

#ifndef BULLET_RUSH_BLAST_INCLUDED
#define BULLET_RUSH_BLAST_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "VfxNoise.hlsl"

CBUFFER_START(UnityPerMaterial)

    half4 _Color;
    half _Intensity;
    half _EdgePower;
    half _CorePower;
    half _CoreGain;

    // 1 = аддитивное смешивание, 0 = альфа
    half _Additive;

    float _NoiseScale;
    float _NoiseAmount;

CBUFFER_END

// Per-instance цвет квада: стадия остывания углей и прозрачность
// дыма у каждого квада своя.
//
// Приходит из MaterialPropertyBlock через SetVectorArray и читается
// как обычное instanced-свойство, тем же instanceID, что и
// матрица.
//
// Почему не часть структуры инстанса: RenderMeshInstanced
// вытаскивает из пользовательской структуры только objectToWorld,
// renderingLayerMask и prevObjectToWorld, а остальное игнорирует -
// цвет остался бы на CPU.
//
// Почему не StructuredBuffer: он требует SM 4.5 в вершинном
// шейдере, а "#pragma target 4.5" здесь отключает инстансинг.
//
// Почему не instanced-свойство с MPB фиксированной длины вслепую:
// у SetVectorArray длина массива не меняется после первой записи
// ("The array length can't be changed once it has been added to the
// block"), поэтому BlastBatchBuffer всегда отдаёт массив ровно из
// 511 элементов.
//
// Без инстансинга (BurnFlameEffect, обычный MeshRenderer) путь
// прежний: цвет берётся из атрибута COLOR.
UNITY_INSTANCING_BUFFER_START(BlastPerInstance)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BlastColor)
UNITY_INSTANCING_BUFFER_END(BlastPerInstance)

struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half4 color : COLOR;
};

Varyings Vert(Attributes input)
{
    // Instance ID нужен и матрице квада, и per-instance цвету.
    // Без этой строки все угли и клубы дыма легли бы в одну точку.
    UNITY_SETUP_INSTANCE_ID(input);
    Varyings output;

    output.positionHCS =
        TransformObjectToHClip(input.positionOS.xyz);

    output.uv = input.uv;

    // UNITY_INSTANCING_ENABLED - корректная проверка варианта.
    // UNITY_INSTANCING_ON - имя keyword'а, а не макрос для ветвления.
    #if defined(UNITY_INSTANCING_ENABLED)
        output.color = UNITY_ACCESS_INSTANCED_PROP(
            BlastPerInstance, _BlastColor);
    #else
        output.color = input.color;
    #endif

    return output;
}

half4 Frag(Varyings input) : SV_Target
{
    // Центр квада в (0,0), край в 1 по осям.
    float2 p = input.uv * 2.0f - 1.0f;
    float d = saturate(length(p));

    if (_NoiseAmount > 0.001f)
    {
        float noise = VfxFlameNoise(
            input.uv,
            _NoiseScale,
            _Time.y
        );

        d = saturate(d + (noise - 0.5f) * _NoiseAmount);
    }

    float mask = pow(1.0f - d, _EdgePower);
    float core = pow(1.0f - d, _CorePower);

    half3 rgb = _Color.rgb * input.color.rgb;
    half alpha = _Color.a * input.color.a * mask;

    rgb *= (1.0h + _CoreGain * core) * _Intensity;

    if (_Additive > 0.5h)
    {
        // Аддитив: маска уходит в цвет, альфа не читается.
        rgb *= mask;
    }

    return half4(rgb, alpha);
}

#endif
