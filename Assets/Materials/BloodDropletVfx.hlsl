// Общий HLSL для капель крови.
//
// Лежит отдельным файлом по той же причине, что и
// BulletTracerVfx.hlsl: HLSLINCLUDE на уровне SubShader в таком
// виде Unity 6 не парсит, и шейдер молча выпадает из импорта.
//
// Include подключается дважды, по одному разу в каждый Pass
// (Forward и ForwardOnly). Активен всегда только один Pass,
// поэтому на GPU это не удваивает работу.

#ifndef BULLET_RUSH_BLOOD_INCLUDED
#define BULLET_RUSH_BLOOD_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)

    half4 _Color;
    half _EdgePower;
    half _CorePower;

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
    // Центр квада в (0,0), края в ±1.
    float2 p = input.uv * 2.0 - 1.0;

    // Форма капли считается прямо из UV: мягкий круг с
    // плотной серединой. Текстура на всю кровь не нужна -
    // капли всё равно меньше 5 пикселей.
    float d = saturate(length(p));
    float mask = pow(1.0 - d, _EdgePower);

    // Середина чуть темнее края: мокрый тёмный центр и
    // чуть более светлый ободок, иначе капля выглядит
    // плоским кружком.
    float core = pow(1.0 - d, _CorePower);

    half3 rgb =
        _Color.rgb * input.color.rgb * (0.75h + 0.5h * core);

    half alpha =
        _Color.a * input.color.a * mask;

    return half4(rgb, alpha);
}

#endif
