// ============================================================
// Общий HLSL для светящихся шаров снарядов (тело пули).
//
// Зачем отдельный шейдер, а не Custom/Bullet Rush VFX Additive:
//   тот считает форму из UV квада - спад по длине для трассера
//   и радиальный спад для вспышки. У сферы UV equirectangular,
//   и любая из этих формул на ней даёт ромб или крест, а не шар.
//
// Здесь форма настоящая: нормаль из меша + френель по касанию
// взгляда. Центр - ровный цвет, кромка светлее: шар выглядит
// раскалённым без единой текстуры и без освещения.
//
// Стоимость на GPU:
//   - 0 текстурных сэмплов, 0 освещения, 0 теней, 0 тумана;
//   - CBUFFER_START(UnityPerMaterial) => совместим с SRP Batcher;
//   - Additive (One One) + ZWrite Off => сортировка прозрачных
//     не нужна, пуля не закрывает собой цель под ней.
// ============================================================

#ifndef BULLET_RUSH_GLOW_BALL_INCLUDED
#define BULLET_RUSH_GLOW_BALL_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)

    half4 _BaseColor;
    half4 _RimColor;
    half _RimPower;
    half _RimStrength;
    half _Intensity;

CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
};

struct Varyings
{
    float4 positionHCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
};

Varyings Vert(Attributes input)
{
    Varyings output;

    VertexPositionInputs positions =
        GetVertexPositionInputs(input.positionOS.xyz);

    VertexNormalInputs normals =
        GetVertexNormalInputs(input.normalOS);

    output.positionHCS = positions.positionCS;
    output.positionWS = positions.positionWS;
    output.normalWS = normals.normalWS;

    return output;
}

half4 Frag(Varyings input) : SV_Target
{
    float3 viewDirWS =
        GetWorldSpaceNormalizeViewDir(input.positionWS);

    float3 normalWS =
        normalize(input.normalWS);

    // Френель: 0 в центре шара, 1 на кромке.
    float facing =
        saturate(dot(normalWS, viewDirWS));

    float rim =
        pow(1.0 - facing, (float)_RimPower);

    half3 color =
        _BaseColor.rgb +
        _RimColor.rgb * rim * _RimStrength;

    color *= (half)_Intensity;

    return half4(color, 1.0);
}

#endif
