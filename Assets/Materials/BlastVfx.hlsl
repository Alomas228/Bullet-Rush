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
// Лежит отдельным файлом, а не в HLSLINCLUDE на уровне SubShader:
// HLSLINCLUDE в таком виде Unity 6 не парсит, и шейдер целиком
// выпадает из импорта.

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

struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    half4 color : COLOR;
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half4 color : COLOR;
};

Varyings Vert(Attributes input)
{
    Varyings output;

    output.positionHCS =
        TransformObjectToHClip(input.positionOS.xyz);

    output.uv = input.uv;
    output.color = input.color;

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
