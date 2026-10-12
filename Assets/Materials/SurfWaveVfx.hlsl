// Общий HLSL гребня волны прибоя (Пляж).
//
// Волна рисуется обычным MeshRenderer на растянутом диске, поэтому
// здесь нет инстансинга: материал один, цвет приходит через
// MaterialPropertyBlock (_Color).
//
// Текстура здесь процедурная (пена/полосы), генерируется в
// шейдере; _WaveTex оставлен как точка входа для своей картинки -
// по умолчанию белый, поэтому на вид не влияет.
//
// ВАЖНО про пропорции: диск растянут в ленту (напр. 69 x 6), а его
// UV всегда 0..1 по обеим осям. Чтобы рисунок не "сплющивало",
// пена считается в мировых единицах через _WaveSize (width, depth):
//   q = p * _WaveSize.xy * 0.5
// Тогда период пены одинаков по X и Z - квадрат остаётся квадратом.

#ifndef BULLET_RUSH_SURF_WAVE_INCLUDED
#define BULLET_RUSH_SURF_WAVE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _Color;
    float4 _WaveSize;
    float _Intensity;
    float _EdgePower;
    float _RingInner;
    float _RingOuter;
    float _FoamScale;
    float _FoamSpeed;
CBUFFER_END

TEXTURE2D(_WaveTex);
SAMPLER(sampler_WaveTex);

struct Attributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

Varyings Vert(Attributes input)
{
    Varyings output;

    output.positionHCS =
        TransformObjectToHClip(input.positionOS.xyz);

    output.uv = input.uv;

    return output;
}

half4 Frag(Varyings input) : SV_Target
{
    // Центр диска в (0,0), край в 1 по осям.
    float2 p = input.uv * 2.0f - 1.0f;

    float r = length(p);

    // Форма ленты: мягкая заливка к центру и светящийся бордюр.
    float fill = pow(saturate(1.0f - r), _EdgePower);

    float ring =
        smoothstep(_RingInner, _RingOuter, r) *
        (1.0f - smoothstep(_RingOuter, 1.0f, r));

    // Мировые (пропорциональные) координаты внутри ленты.
    float2 size = max(_WaveSize.xy, 0.001f);
    float2 q = p * size * 0.5f;

    // Пена бежит вдоль длинной стороны ленты, какая бы она ни была.
    bool longX = size.x >= size.y;
    float along = longX ? q.x : q.y;
    float across = longX ? q.y : q.x;

    float t = _Time.y * _FoamSpeed;

    // Гребни пены вдоль ленты + поперечная рябь. Всё в мировых
    // единицах, поэтому лента любой длины выглядит одинаково.
    float foamA =
        0.5f + 0.5f * sin(along * _FoamScale +
                          sin(across * 0.9f) * 2.0f + t);

    float foamB =
        0.5f + 0.5f * sin(along * _FoamScale * 0.53f -
                          across * 0.7f + t * 1.3f + 1.7f);

    float foam = saturate(foamA * 0.6f + foamB * 0.55f);

    float ripple =
        0.5f + 0.5f * sin(across * 1.1f - t * 1.1f +
                         sin(along * 0.5f) * 1.5f);

    // Своя картинка (белый по умолчанию - ни на что не влияет).
    float tex = SAMPLE_TEXTURE2D(
        _WaveTex, sampler_WaveTex, input.uv).r;

    float band = saturate(fill * 0.35f + ring * 0.9f);

    band *= lerp(0.6f, 1.15f, foam);
    band *= lerp(0.85f, 1.0f, ripple);
    band *= tex;

    float3 rgb = _Color.rgb * (_Intensity * band + 0.08f);

    float alpha = saturate(band * _Color.a * _Intensity);

    return half4(rgb, alpha);
}

#endif
